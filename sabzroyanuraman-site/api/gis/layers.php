<?php
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store, max-age=0');
header('X-Content-Type-Options: nosniff');
header('Referrer-Policy: same-origin');

function gis_layers_respond(int $status, array $payload): never {
    http_response_code($status);
    echo json_encode($payload, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
    exit;
}

if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'GET') {
    header('Allow: GET');
    gis_layers_respond(405, ['ok' => false, 'error' => 'method_not_allowed']);
}

$configFile = '/home/sabzroyanuraman/private/gis-db.php';
if (!is_file($configFile) || !is_readable($configFile)) {
    gis_layers_respond(503, ['ok' => false, 'error' => 'gis_service_not_configured']);
}

try {
    $config = require $configFile;
    if (!is_array($config) || !isset($config['layers']) || !is_array($config['layers'])) {
        gis_layers_respond(503, ['ok' => false, 'error' => 'gis_layers_not_configured']);
    }

    $pdo = new PDO(
        sprintf('pgsql:host=%s;port=%d;dbname=%s;connect_timeout=3',
            (string)$config['host'], (int)$config['port'], (string)$config['database']),
        (string)$config['username'], (string)$config['password'],
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
         PDO::ATTR_EMULATE_PREPARES => false, PDO::ATTR_TIMEOUT => 4]
    );

    $result = [];
    foreach ($config['layers'] as $layer) {
        if (!is_array($layer) || !isset($layer['id'], $layer['table'], $layer['geometry_column'])) {
            continue;
        }
        $id = (string)$layer['id'];
        $table = (string)$layer['table'];
        $geom = (string)$layer['geometry_column'];
        if (!preg_match('/^[a-zA-Z0-9_-]{1,64}$/', $id) ||
            !preg_match('/^[a-zA-Z_][a-zA-Z0-9_]{0,62}$/', $table) ||
            !preg_match('/^[a-zA-Z_][a-zA-Z0-9_]{0,62}$/', $geom)) {
            continue;
        }

        $check = $pdo->prepare(
            'SELECT f_geometry_column, type, srid FROM public.geometry_columns
             WHERE f_table_schema = :schema AND f_table_name = :table AND f_geometry_column = :geom LIMIT 1'
        );
        $check->execute(['schema' => 'public', 'table' => $table, 'geom' => $geom]);
        $meta = $check->fetch();
        if (!$meta) {
            continue;
        }
        $result[] = [
            'id' => $id,
            'title' => isset($layer['title']) ? (string)$layer['title'] : $id,
            'geometryType' => (string)$meta['type'],
            'srid' => (int)$meta['srid']
        ];
    }

    gis_layers_respond(200, ['ok' => true, 'layers' => $result]);
} catch (Throwable $e) {
    error_log('[gis-api] Layer catalog failed: ' . get_class($e));
    gis_layers_respond(503, ['ok' => false, 'error' => 'gis_database_unavailable']);
}
