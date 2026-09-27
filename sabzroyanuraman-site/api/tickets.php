<?php
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store');
header('X-Content-Type-Options: nosniff');

function respond(int $status, array $payload): never {
    http_response_code($status);
    echo json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    respond(405, ['ok'=>false,'error'=>'فقط درخواست POST مجاز است.']);
}

$origin = $_SERVER['HTTP_ORIGIN'] ?? '';
if ($origin !== '') {
    $originHost = parse_url($origin, PHP_URL_HOST);
    $requestHost = $_SERVER['HTTP_HOST'] ?? '';
    $requestHost = preg_replace('/:\\d+$/', '', $requestHost);
    if (!$originHost || strcasecmp((string)$originHost, (string)$requestHost) !== 0) {
        respond(403, ['ok'=>false,'error'=>'مبدأ درخواست مجاز نیست.']);
    }
}

$raw = file_get_contents('php://input');
$data = json_decode($raw ?: '', true);
if (!is_array($data)) {
    respond(400, ['ok'=>false,'error'=>'داده ارسالی نامعتبر است.']);
}

$action = (string)($data['action'] ?? '');
if (!in_array($action, ['create','get','reply'], true)) {
    respond(400, ['ok'=>false,'error'=>'عملیات درخواست نامعتبر است.']);
}

$dataDir = '/home/sabzroyanuraman/private-data/tickets';
if (!is_dir($dataDir) && !mkdir($dataDir, 0700, true) && !is_dir($dataDir)) {
    respond(500, ['ok'=>false,'error'=>'فضای ذخیره‌سازی تیکت قابل ایجاد نیست.']);
}

$dbPath = $dataDir . '/tickets.sqlite';

try {
    $db = new PDO('sqlite:' . $dbPath, null, null, [
        PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
        PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC
    ]);
    $db->exec('PRAGMA journal_mode=WAL');
    $db->exec('PRAGMA busy_timeout=5000');
    $db->exec('PRAGMA foreign_keys=ON');
    $db->exec('CREATE TABLE IF NOT EXISTS tickets (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        ticket_number TEXT NOT NULL UNIQUE,
        access_token_hash TEXT NOT NULL,
        name TEXT NOT NULL,
        phone TEXT NOT NULL,
        email TEXT NULL,
        category TEXT NOT NULL,
        subject TEXT NOT NULL,
        status TEXT NOT NULL DEFAULT "open",
        created_at TEXT NOT NULL,
        updated_at TEXT NOT NULL
    )');
    $db->exec('CREATE TABLE IF NOT EXISTS ticket_messages (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        ticket_id INTEGER NOT NULL,
        author_type TEXT NOT NULL,
        body TEXT NOT NULL,
        created_at TEXT NOT NULL,
        FOREIGN KEY(ticket_id) REFERENCES tickets(id) ON DELETE CASCADE
    )');
    $db->exec('CREATE INDEX IF NOT EXISTS idx_ticket_messages_ticket ON ticket_messages(ticket_id, id)');
    $db->exec('CREATE TABLE IF NOT EXISTS rate_limits (
        key TEXT PRIMARY KEY,
        window_started INTEGER NOT NULL,
        count INTEGER NOT NULL
    )');
} catch (Throwable $e) {
    respond(500, ['ok'=>false,'error'=>'خطا در اتصال به سامانه تیکت.']);
}

$ip = $_SERVER['REMOTE_ADDR'] ?? 'unknown';
$rateKey = hash('sha256', $ip . '|' . gmdate('Y-m-d-H') . '|' . $action);
$limit = $action === 'create' ? 10 : ($action === 'reply' ? 30 : 60);
$nowEpoch = time();
try {
    $stmt = $db->prepare('SELECT window_started, count FROM rate_limits WHERE key = ?');
    $stmt->execute([$rateKey]);
    $rate = $stmt->fetch();
    if ($rate && $nowEpoch - (int)$rate['window_started'] < 3600 && (int)$rate['count'] >= $limit) {
        respond(429, ['ok'=>false,'error'=>'تعداد درخواست‌ها بیش از حد مجاز است. کمی بعد دوباره تلاش کنید.']);
    }
    if (!$rate || $nowEpoch - (int)$rate['window_started'] >= 3600) {
        $stmt = $db->prepare('INSERT OR REPLACE INTO rate_limits(key,window_started,count) VALUES(?,?,1)');
        $stmt->execute([$rateKey,$nowEpoch]);
    } else {
        $stmt = $db->prepare('UPDATE rate_limits SET count=count+1 WHERE key=?');
        $stmt->execute([$rateKey]);
    }
} catch (Throwable $e) {
    // Rate limiting must not prevent the ticket system from operating.
}

function clean_text($value, int $max): string {
    $value = trim((string)$value);
    if ($value === '') return '';
    if (function_exists('mb_substr')) return mb_substr($value, 0, $max);
    return substr($value, 0, $max);
}

function ticket_number(PDO $db): string {
    do {
        $number = 'SR-' . gmdate('Ymd') . '-' . strtoupper(bin2hex(random_bytes(3)));
        $stmt = $db->prepare('SELECT 1 FROM tickets WHERE ticket_number=? LIMIT 1');
        $stmt->execute([$number]);
    } while ($stmt->fetchColumn());
    return $number;
}

function ticket_payload(PDO $db, array $ticket): array {
    $stmt = $db->prepare('SELECT author_type, body, created_at FROM ticket_messages WHERE ticket_id=? ORDER BY id ASC');
    $stmt->execute([(int)$ticket['id']]);
    return [
        'ticket_number'=>$ticket['ticket_number'],
        'category'=>$ticket['category'],
        'subject'=>$ticket['subject'],
        'status'=>$ticket['status'],
        'created_at'=>$ticket['created_at'],
        'updated_at'=>$ticket['updated_at'],
        'messages'=>$stmt->fetchAll()
    ];
}

$token = clean_text($data['access_token'] ?? '', 100);

if ($action === 'create') {
    $name = clean_text($data['name'] ?? '', 120);
    $phone = clean_text($data['phone'] ?? '', 30);
    $email = clean_text($data['email'] ?? '', 190);
    $category = clean_text($data['category'] ?? 'عمومی', 80);
    $subject = clean_text($data['subject'] ?? '', 180);
    $body = clean_text($data['message'] ?? '', 5000);

    if ($name === '' || $phone === '' || $subject === '' || $body === '') {
        respond(422, ['ok'=>false,'error'=>'نام، شماره موبایل، عنوان و شرح درخواست الزامی است.']);
    }
    if ($email !== '' && !filter_var($email, FILTER_VALIDATE_EMAIL)) {
        respond(422, ['ok'=>false,'error'=>'ایمیل واردشده معتبر نیست.']);
    }

    $ticketNumber = ticket_number($db);
    $accessToken = bin2hex(random_bytes(16));
    $tokenHash = hash('sha256', $accessToken);
    $now = gmdate('c');

    try {
        $db->beginTransaction();
        $stmt = $db->prepare('INSERT INTO tickets(ticket_number,access_token_hash,name,phone,email,category,subject,status,created_at,updated_at) VALUES(?,?,?,?,?,?,?,?,?,?)');
        $stmt->execute([$ticketNumber,$tokenHash,$name,$phone,$email !== '' ? $email : null,$category,$subject,'open',$now,$now]);
        $ticketId = (int)$db->lastInsertId();
        $stmt = $db->prepare('INSERT INTO ticket_messages(ticket_id,author_type,body,created_at) VALUES(?,?,?,?)');
        $stmt->execute([$ticketId,'user',$body,$now]);
        $db->commit();
    } catch (Throwable $e) {
        if ($db->inTransaction()) $db->rollBack();
        respond(500, ['ok'=>false,'error'=>'ثبت تیکت انجام نشد.']);
    }

    respond(201, [
        'ok'=>true,
        'ticket_number'=>$ticketNumber,
        'access_token'=>$accessToken
    ]);
}

if ($token === '' || strlen($token) < 20) {
    respond(422, ['ok'=>false,'error'=>'کد دسترسی تیکت الزامی است.']);
}

$number = strtoupper(clean_text($data['ticket_number'] ?? '', 40));
if (!preg_match('/^SR-\\d{8}-[A-F0-9]{6}$/', $number)) {
    respond(422, ['ok'=>false,'error'=>'شماره تیکت معتبر نیست.']);
}

$stmt = $db->prepare('SELECT * FROM tickets WHERE ticket_number=? LIMIT 1');
$stmt->execute([$number]);
$ticket = $stmt->fetch();

if (!$ticket || !hash_equals((string)$ticket['access_token_hash'], hash('sha256',$token))) {
    respond(404, ['ok'=>false,'error'=>'تیکت یا کد دسترسی صحیح نیست.']);
}

if ($action === 'get') {
    respond(200, ['ok'=>true,'ticket'=>ticket_payload($db,$ticket)]);
}

$body = clean_text($data['message'] ?? '', 5000);
if ($body === '') {
    respond(422, ['ok'=>false,'error'=>'متن پیام نمی‌تواند خالی باشد.']);
}
if ($ticket['status'] === 'closed') {
    respond(409, ['ok'=>false,'error'=>'این تیکت بسته شده است. برای درخواست جدید یک تیکت تازه ثبت کنید.']);
}

$now = gmdate('c');
try {
    $db->beginTransaction();
    $stmt = $db->prepare('INSERT INTO ticket_messages(ticket_id,author_type,body,created_at) VALUES(?,?,?,?)');
    $stmt->execute([(int)$ticket['id'],'user',$body,$now]);
    $stmt = $db->prepare('UPDATE tickets SET status="open",updated_at=? WHERE id=?');
    $stmt->execute([$now,(int)$ticket['id']]);
    $db->commit();
} catch (Throwable $e) {
    if ($db->inTransaction()) $db->rollBack();
    respond(500, ['ok'=>false,'error'=>'ارسال پیام انجام نشد.']);
}

$stmt = $db->prepare('SELECT * FROM tickets WHERE id=?');
$stmt->execute([(int)$ticket['id']]);
$updatedTicket = $stmt->fetch();

respond(200, ['ok'=>true,'ticket'=>ticket_payload($db,$updatedTicket)]);
