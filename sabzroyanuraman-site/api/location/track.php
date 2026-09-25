<?php
declare(strict_types=1);

/*
 * Consent-based GPS track ingestion endpoint.
 * Data directory is outside the public web root when deployed as
 * public_html/api/location/track.php.
 */
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store, private');
header('X-Content-Type-Options: nosniff');
header('Referrer-Policy: no-referrer');

function gps_reply(int $status, array $body): never {
    http_response_code($status);
    echo json_encode($body, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}
if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'POST') {
    header('Allow: POST');
    gps_reply(405, ['ok'=>false,'error'=>'POST required']);
}
$origin = $_SERVER['HTTP_ORIGIN'] ?? '';
$host = strtolower((string)($_SERVER['HTTP_HOST'] ?? ''));
if ($origin !== '') {
    $originHost = strtolower((string)parse_url($origin, PHP_URL_HOST));
    if ($originHost === '' || $originHost !== preg_replace('/:\\d+$/', '', $host)) {
        gps_reply(403, ['ok'=>false,'error'=>'Origin rejected']);
    }
}
$contentType = strtolower((string)($_SERVER['CONTENT_TYPE'] ?? ''));
if (strpos($contentType, 'application/json') !== 0) {
    gps_reply(415, ['ok'=>false,'error'=>'JSON required']);
}
$raw = file_get_contents('php://input');
if ($raw === false || strlen($raw) > 12000) gps_reply(413, ['ok'=>false,'error'=>'Payload too large']);
$data = json_decode($raw, true);
if (!is_array($data)) gps_reply(400, ['ok'=>false,'error'=>'Invalid JSON']);
$action = (string)($data['action'] ?? '');
$sid = (string)($data['session_id'] ?? '');
if (!preg_match('/^[a-zA-Z0-9-]{12,80}$/', $sid)) gps_reply(400, ['ok'=>false,'error'=>'Invalid session id']);

$home = dirname(__DIR__, 3);
$dataDir = $home . '/private-data/gps-tracks';
if (!is_dir($dataDir) && !mkdir($dataDir, 0700, true) && !is_dir($dataDir)) {
    error_log('GPS track: unable to create private data directory');
    gps_reply(500, ['ok'=>false,'error'=>'Storage unavailable']);
}
@chmod($dataDir, 0700);
$dbPath = $dataDir . '/tracks.sqlite';
try {
    $db = new PDO('sqlite:' . $dbPath, null, null, [PDO::ATTR_ERRMODE=>PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE=>PDO::FETCH_ASSOC]);
    $db->exec('PRAGMA journal_mode=WAL');
    $db->exec('PRAGMA busy_timeout=5000');
    $db->exec("CREATE TABLE IF NOT EXISTS tracks (session_id TEXT PRIMARY KEY, started_at TEXT NOT NULL, stopped_at TEXT, last_seen_at TEXT, point_count INTEGER NOT NULL DEFAULT 0)");
    $db->exec("CREATE TABLE IF NOT EXISTS track_points (id INTEGER PRIMARY KEY AUTOINCREMENT, session_id TEXT NOT NULL, latitude REAL NOT NULL, longitude REAL NOT NULL, accuracy REAL, speed REAL, heading REAL, altitude REAL, recorded_at TEXT NOT NULL, received_at TEXT NOT NULL, FOREIGN KEY(session_id) REFERENCES tracks(session_id))");
    $db->exec('CREATE INDEX IF NOT EXISTS idx_track_points_session ON track_points(session_id, id)');
    @chmod($dbPath, 0600);
} catch (Throwable $e) {
    error_log('GPS track DB initialization failed: ' . $e->getMessage());
    gps_reply(500, ['ok'=>false,'error'=>'Storage initialization failed']);
}
$now = gmdate('c');
try {
    if ($action === 'start') {
        $started = (string)($data['started_at'] ?? $now);
        if (strlen($started) > 40) gps_reply(400, ['ok'=>false,'error'=>'Invalid start time']);
        $q = $db->prepare('INSERT OR IGNORE INTO tracks(session_id,started_at,last_seen_at) VALUES(:sid,:started,:now)');
        $q->execute([':sid'=>$sid,':started'=>$started,':now'=>$now]);
        gps_reply(200, ['ok'=>true]);
    }
    $q = $db->prepare('SELECT session_id,stopped_at FROM tracks WHERE session_id=:sid');
    $q->execute([':sid'=>$sid]);
    $track = $q->fetch();
    if (!$track) gps_reply(404, ['ok'=>false,'error'=>'Tracking session not found']);
    if ($track['stopped_at'] !== null) gps_reply(409, ['ok'=>false,'error'=>'Tracking session is closed']);
    if ($action === 'stop') {
        $q = $db->prepare('UPDATE tracks SET stopped_at=:stopped,last_seen_at=:now WHERE session_id=:sid AND stopped_at IS NULL');
        $q->execute([':stopped'=>substr((string)($data['stopped_at'] ?? $now),0,40),':now'=>$now,':sid'=>$sid]);
        gps_reply(200, ['ok'=>true]);
    }
    if ($action !== 'point') gps_reply(400, ['ok'=>false,'error'=>'Unknown action']);
    $lat = filter_var($data['latitude'] ?? null, FILTER_VALIDATE_FLOAT);
    $lon = filter_var($data['longitude'] ?? null, FILTER_VALIDATE_FLOAT);
    $accuracy = isset($data['accuracy']) ? filter_var($data['accuracy'], FILTER_VALIDATE_FLOAT) : null;
    $speed = isset($data['speed']) && is_numeric($data['speed']) ? (float)$data['speed'] : null;
    $heading = isset($data['heading']) && is_numeric($data['heading']) ? (float)$data['heading'] : null;
    $altitude = isset($data['altitude']) && is_numeric($data['altitude']) ? (float)$data['altitude'] : null;
    if ($lat === false || $lon === false || $lat < -90 || $lat > 90 || $lon < -180 || $lon > 180) gps_reply(400, ['ok'=>false,'error'=>'Invalid coordinates']);
    if ($accuracy !== null && ($accuracy === false || $accuracy < 0 || $accuracy > 100000)) $accuracy = null;
    if ($speed !== null && ($speed < 0 || $speed > 150)) $speed = null;
    if ($heading !== null && ($heading < 0 || $heading > 360)) $heading = null;
    if ($altitude !== null && ($altitude < -12000 || $altitude > 100000)) $altitude = null;
    $recorded = substr((string)($data['recorded_at'] ?? $now),0,40);
    $db->beginTransaction();
    $q=$db->prepare('INSERT INTO track_points(session_id,latitude,longitude,accuracy,speed,heading,altitude,recorded_at,received_at) VALUES(:sid,:lat,:lon,:acc,:speed,:heading,:alt,:recorded,:received)');
    $q->execute([':sid'=>$sid,':lat'=>$lat,':lon'=>$lon,':acc'=>$accuracy,':speed'=>$speed,':heading'=>$heading,':alt'=>$altitude,':recorded'=>$recorded,':received'=>$now]);
    $q=$db->prepare('UPDATE tracks SET point_count=point_count+1,last_seen_at=:now WHERE session_id=:sid');
    $q->execute([':now'=>$now,':sid'=>$sid]);
    $db->commit();
    gps_reply(200, ['ok'=>true]);
} catch (Throwable $e) {
    if (isset($db) && $db->inTransaction()) $db->rollBack();
    error_log('GPS track request failed: ' . $e->getMessage());
    gps_reply(500, ['ok'=>false,'error'=>'Unable to store tracking data']);
}
