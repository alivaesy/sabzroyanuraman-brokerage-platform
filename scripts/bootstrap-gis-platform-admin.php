<?php
declare(strict_types=1);

/**
 * One-time CLI bootstrap for the first Sabz Royan GIS platform administrator.
 *
 * Keep this script outside the web document root and remove it after use.
 * Configure /home/sabzroyanuraman/private/gis-bootstrap-db.php with a temporary
 * database role allowed to create the initial administrator, organization,
 * membership, subscription and audit record. Never commit that config.
 */
if (PHP_SAPI !== 'cli') {
    http_response_code(404);
    exit;
}
if (!defined('STDIN') || !defined('STDOUT')) {
    fwrite(STDERR, "Run this script from an interactive PHP CLI.\\n");
    exit(2);
}

/** Read a non-secret value from the CLI without requiring PHP's readline extension. */
function promptLine(string $prompt): string
{
    fwrite(STDOUT, $prompt);
    $line = fgets(STDIN);
    return $line === false ? '' : rtrim($line, "\\r\\n");
}

/**
 * Read a secret from an interactive Unix terminal without echoing it.
 * Fail closed when terminal echo cannot be disabled; never fall back to visible input.
 */
function readSecret(string $prompt): string
{
    if (PHP_SAPI !== 'cli' || !defined('STDIN') || !function_exists('exec')) {
        fwrite(STDERR, "Secure hidden password input is unavailable in this environment.\n");
        exit(2);
    }
    $tty = @fopen('/dev/tty', 'r+');
    if ($tty === false) {
        fwrite(STDERR, "No interactive terminal is available; refusing visible password input.\n");
        exit(2);
    }
    // stty is silent on success; verify its exit status.
    $status = 1;
    @exec('stty -echo < /dev/tty 2>/dev/null', $unused, $status);
    if ($status !== 0) {
        fclose($tty);
        fwrite(STDERR, "Could not disable terminal echo; refusing visible password input.\n");
        exit(2);
    }
    fwrite(STDOUT, $prompt);
    try {
        $line = fgets($tty);
        if ($line === false) {
            throw new RuntimeException('Unable to read terminal input.');
        }
        return rtrim($line, "\r\n");
    } finally {
        @exec('stty echo < /dev/tty 2>/dev/null');
        fwrite(STDOUT, "\n");
        fclose($tty);
    }
}

$configFile = '/home/sabzroyanuraman/private/gis-bootstrap-db.php';
if (!is_file($configFile) || !is_readable($configFile)) {
    fwrite(STDERR, "Missing private bootstrap DB config: {$configFile}
");
    exit(2);
}
$config = require $configFile;
foreach (['host', 'port', 'database', 'username', 'password'] as $key) {
    if (!is_array($config) || !isset($config[$key]) || !is_scalar($config[$key]) || (string)$config[$key] === '') {
        fwrite(STDERR, "Invalid bootstrap DB config.
");
        exit(2);
    }
}
$username = strtolower(trim((string)promptLine('Initial admin username (3-64 chars): ')));
if (!preg_match('/^[a-z0-9][a-z0-9._-]{2,63}$/', $username)) {
    fwrite(STDERR, "Invalid username.
");
    exit(2);
}
$displayName = trim((string)promptLine('Display name: '));
$email = trim((string)promptLine('Email (optional): '));
if (strlen($displayName) > 200 || ($email !== '' && !filter_var($email, FILTER_VALIDATE_EMAIL))) {
    fwrite(STDERR, "Invalid display name or email.
");
    exit(2);
}
$orgName = trim((string)promptLine('Initial organization name: '));
$slug = strtolower(trim((string)promptLine('Organization slug (a-z, 0-9, hyphens): ')));
if (strlen($orgName) < 2 || strlen($orgName) > 200 || !preg_match('/^[a-z0-9][a-z0-9-]{1,62}$/', $slug)) {
    fwrite(STDERR, "Invalid organization name or slug.
");
    exit(2);
}
$daysRaw = trim((string)promptLine('Initial subscription days [30]: '));
$days = $daysRaw === '' ? 30 : filter_var($daysRaw, FILTER_VALIDATE_INT);
if ($days === false || $days < 1 || $days > 3650) {
    fwrite(STDERR, "Subscription days must be 1-3650.
");
    exit(2);
}
$pass = readSecret('Password: ');
if (strlen($pass) < 12 || strlen($pass) > 1024) {
    fwrite(STDERR, "Password must be 12-1024 characters.
");
    exit(2);
}
$confirm = readSecret('Confirm password: ');
if (!hash_equals($pass, $confirm)) {
    fwrite(STDERR, "Passwords do not match.
");
    exit(2);
}

try {
    $pdo = new PDO(
        sprintf('pgsql:host=%s;port=%d;dbname=%s;connect_timeout=3', (string)$config['host'], (int)$config['port'], (string)$config['database']),
        (string)$config['username'],
        (string)$config['password'],
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC, PDO::ATTR_EMULATE_PREPARES => false, PDO::ATTR_TIMEOUT => 4]
    );
    $pdo->beginTransaction();
    // Serialize concurrent first-admin bootstrap attempts on this database.
    $pdo->query('SELECT pg_advisory_xact_lock(731904281)');
    $existingAdmin = (int)$pdo->query("SELECT count(*) FROM public.gis_users WHERE is_platform_admin = true")->fetchColumn();
    if ($existingAdmin !== 0) {
        $pdo->rollBack();
        fwrite(STDERR, "Bootstrap refused: a platform administrator already exists.
");
        exit(3);
    }
    $q = $pdo->prepare("INSERT INTO public.gis_users(username,email,password_hash,display_name,status,is_platform_admin) VALUES(:u,:e,:p,:d,'active',true) RETURNING id");
    $q->execute(['u' => $username, 'e' => $email !== '' ? $email : null, 'p' => password_hash($pass, PASSWORD_DEFAULT), 'd' => $displayName]);
    $userId = (int)$q->fetchColumn();

    $q = $pdo->prepare("INSERT INTO public.gis_organizations(name,slug,status) VALUES(:n,:s,'active') RETURNING id");
    $q->execute(['n' => $orgName, 's' => $slug]);
    $orgId = (int)$q->fetchColumn();

    $q = $pdo->prepare("INSERT INTO public.gis_organization_members(organization_id,user_id,role,status) VALUES(:o,:u,'owner','active')");
    $q->execute(['o' => $orgId, 'u' => $userId]);

    $q = $pdo->prepare("INSERT INTO public.gis_subscriptions(organization_id,plan_code,requested_days,status,starts_at,expires_at,max_users,activated_by,activation_note) VALUES(:o,'bootstrap',:days,'active',now(),now()+make_interval(days => :days2),1,:u,'Initial one-time platform bootstrap') RETURNING id");
    $q->execute(['o' => $orgId, 'days' => $days, 'days2' => $days, 'u' => $userId]);
    $subscriptionId = (int)$q->fetchColumn();

    $q = $pdo->prepare("INSERT INTO public.gis_admin_audit_log(actor_user_id,organization_id,action,target_type,target_id,details) VALUES(:u,:o,'bootstrap_first_admin','organization',:id,CAST(:d AS jsonb))");
    $q->execute(['u' => $userId, 'o' => $orgId, 'id' => (string)$orgId, 'd' => json_encode(['subscription_id' => $subscriptionId, 'username' => $username], JSON_UNESCAPED_SLASHES)]);
    $pdo->commit();
    fwrite(STDOUT, "Bootstrap complete. User ID: {$userId}; organization ID: {$orgId}; subscription ID: {$subscriptionId}.\n");
    fwrite(STDOUT, "Now remove the temporary bootstrap DB config and this script from the server.\n");
} catch (Throwable $e) {
    if (isset($pdo) && $pdo instanceof PDO && $pdo->inTransaction()) {
        $pdo->rollBack();
    }
    error_log('[gis-bootstrap] failed: ' . get_class($e));
    fwrite(STDERR, "Bootstrap failed; transaction rolled back. Check DB schema and temporary DB-role grants.\n");
    exit(1);
}
