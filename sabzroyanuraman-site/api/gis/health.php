<?php
declare(strict_types=1);

/**
 * Read-only PostGIS connectivity check.
 * Database credentials must live outside the public document root.
 */

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store, max-age=0');
header('X-Content-Type-Options: nosniff');
header('Referrer-Policy: same-origin');

function gis_health_respond(int $status, array $payload): never
{
    http_response_code($status);
    echo json_encode($payload, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
    exit;
}

if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'GET') {
    header('Allow: GET');
    gis_health_respond(405, ['ok' => false, 'error' => 'method_not_allowed']);
}

// Keep this file and the database configuration outside the repository.
$configFile = '/home/sabzroyanuraman/private/gis-db.php';
if (!is_file($configFile) || !is_readable($configFile)) {
    gis_health_respond(503, ['ok' => false, 'error' => 'gis_service_not_configured']);
}

try {
    $config = require $configFile;
    if (!is_array($config)) {
        throw new RuntimeException('Invalid GIS database configuration.');
    }

    foreach (['host', 'port', 'database', 'username', 'password'] as $key) {
        if (!isset($config[$key]) || !is_scalar($config[$key]) || (string)$config[$key] === '') {
            throw new RuntimeException('Missing GIS database configuration.');
        }
    }

    $dsn = sprintf(
        'pgsql:host=%s;port=%d;dbname=%s;connect_timeout=3',
        (string)$config['host'],
        (int)$config['port'],
        (string)$config['database']
    );

    $pdo = new PDO($dsn, (string)$config['username'], (string)$config['password'], [
        PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
        PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
        PDO::ATTR_EMULATE_PREPARES => false,
        PDO::ATTR_TIMEOUT => 4,
    ]);

    // Verify the database and PostGIS without exposing names, versions, or errors.
    $pdo->query('SELECT postgis_version()')->fetchColumn();
    gis_health_respond(200, ['ok' => true, 'service' => 'gis-api']);
} catch (Throwable $e) {
    // Do not return exception details or credentials to the browser.
    error_log('[gis-api] Health check failed: ' . get_class($e));
    gis_health_respond(503, ['ok' => false, 'error' => 'gis_database_unavailable']);
}
