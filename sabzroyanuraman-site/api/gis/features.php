<?php
declare(strict_types=1);

header('Content-Type: application/geo+json; charset=utf-8');
header('Cache-Control: no-store, max-age=0');
header('X-Content-Type-Options: nosniff');
header('Referrer-Policy: same-origin');

function gis_features_respond(int $status, array $payload): never {
    http_response_code($status);
    echo json_encode($payload, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
    exit;
}

function gis_identifier(string $value): string {
    if (!preg_match('/^[a-zA-Z_][a-zA-Z0-9_]{0,62}$/', $value)) {
        throw new RuntimeException('Invalid configured identifier.');
    }
    return '"' . $value . '"';
}

if (($_SERVER['REQUEST_METHOD'] ?? '') !== 'GET') {
    header('Allow: GET');
    gis_features_respond(405, ['ok' => false, 'error' => 'method_not_allowed']);
}

$layerId = (string)($_GET['layer'] ?? '');
if (!preg_match('/^[a-zA-Z0-9_-]{1,64}$/', $layerId)) {
    gis_features_respond(400, ['ok' => false, 'error' => 'invalid_layer']);
}

// Required WGS84 bbox: minLon,minLat,maxLon,maxLat.
$bboxText = (string)($_GET['bbox'] ?? '');
$bboxParts = explode(',', $bboxText);
if (count($bboxParts) !== 4) {
    gis_features_respond(400, ['ok' => false, 'error' => 'bbox_required']);
}
$bbox = [];
foreach ($bboxParts as $part) {
    if ($part === '' || !is_numeric($part) || !is_finite((float)$part)) {
        gis_features_respond(400, ['ok' => false, 'error' => 'invalid_bbox']);
    }
    $bbox[] = (float)$part;
}
[$minLon, $minLat, $maxLon, $maxLat] = $bbox;
if ($minLon < -180 || $maxLon > 180 || $minLat < -90 || $maxLat > 90 ||
    $minLon >= $maxLon || $minLat >= $maxLat) {
    gis_features_respond(400, ['ok' => false, 'error' => 'invalid_bbox']);
}
if (($maxLon - $minLon) * ($maxLat - $minLat) > 10000) {
    gis_features_respond(400, ['ok' => false, 'error' => 'bbox_too_large']);
}

$limit = filter_var($_GET['limit'] ?? '500', FILTER_VALIDATE_INT);
if ($limit === false || $limit < 1 || $limit > 1000) {
    gis_features_respond(400, ['ok' => false, 'error' => 'limit_must_be_1_to_1000']);
}

$configFile = '/home/sabzroyanuraman/private/gis-db.php';
if (!is_file($configFile) || !is_readable($configFile)) {
    gis_features_respond(503, ['ok' => false, 'error' => 'gis_service_not_configured']);
}

try {
    $config = require $configFile;
    if (!is_array($config) || !isset($config['layers']) || !is_array($config['layers'])) {
        gis_features_respond(503, ['ok' => false, 'error' => 'gis_layers_not_configured']);
    }

    $layer = null;
    foreach ($config['layers'] as $candidate) {
        if (is_array($candidate) && isset($candidate['id']) && (string)$candidate['id'] === $layerId) {
            $layer = $candidate;
            break;
        }
    }
    if (!$layer) {
        gis_features_respond(404, ['ok' => false, 'error' => 'layer_not_found']);
    }

    $table = gis_identifier((string)($layer['table'] ?? ''));
    $geom = gis_identifier((string)($layer['geometry_column'] ?? ''));
    $properties = $layer['properties'] ?? [];
    if (!is_array($properties) || count($properties) > 40) {
        throw new RuntimeException('Invalid configured property list.');
    }

    $propertySql = [];
    foreach ($properties as $outputName => $columnName) {
        $outputName = (string)$outputName;
        if (!preg_match('/^[a-zA-Z_][a-zA-Z0-9_]{0,62}$/', $outputName) || !is_string($columnName)) {
            throw new RuntimeException('Invalid configured property mapping.');
        }
        $propertySql[] = "'" . str_replace("'", "''", $outputName) . "', t." . gis_identifier($columnName);
    }
    $jsonProperties = $propertySql ? 'json_build_object(' . implode(', ', $propertySql) . ')' : "'{}'::json";

    $pdo = new PDO(
        sprintf('pgsql:host=%s;port=%d;dbname=%s;connect_timeout=3',
            (string)$config['host'], (int)$config['port'], (string)$config['database']),
        (string)$config['username'], (string)$config['password'],
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
         PDO::ATTR_EMULATE_PREPARES => false, PDO::ATTR_TIMEOUT => 4]
    );

    $meta = $pdo->prepare(
        'SELECT srid FROM public.geometry_columns
         WHERE f_table_schema = :schema AND f_table_name = :table AND f_geometry_column = :geom LIMIT 1'
    );
    $meta->execute([
        'schema' => 'public',
        'table' => (string)$layer['table'],
        'geom' => (string)$layer['geometry_column']
    ]);
    $srid = $meta->fetchColumn();
    if ($srid === false || (int)$srid <= 0) {
        gis_features_respond(404, ['ok' => false, 'error' => 'layer_not_found']);
    }

    $sql = 'SELECT ST_AsGeoJSON(ST_Transform(t.' . $geom . ', 4326), 6)::json AS geometry, ' .
        $jsonProperties . ' AS properties FROM public.' . $table . ' t WHERE t.' . $geom .
        ' && ST_Transform(ST_MakeEnvelope(:min_lon, :min_lat, :max_lon, :max_lat, 4326), ' . (int)$srid . ')' .
        ' AND ST_Intersects(t.' . $geom .
        ', ST_Transform(ST_MakeEnvelope(:min_lon2, :min_lat2, :max_lon2, :max_lat2, 4326), ' . (int)$srid . ')' .
        ' LIMIT ' . (int)$limit;

    $stmt = $pdo->prepare($sql);
    $stmt->execute([
        'min_lon' => $minLon, 'min_lat' => $minLat, 'max_lon' => $maxLon, 'max_lat' => $maxLat,
        'min_lon2' => $minLon, 'min_lat2' => $minLat, 'max_lon2' => $maxLon, 'max_lat2' => $maxLat
    ]);

    $features = [];
    while ($row = $stmt->fetch()) {
        $geometry = is_string($row['geometry']) ? json_decode($row['geometry'], true) : $row['geometry'];
        $props = is_string($row['properties']) ? json_decode($row['properties'], true) : $row['properties'];
        $features[] = ['type' => 'Feature', 'geometry' => $geometry, 'properties' => $props ?? new stdClass()];
    }

    echo json_encode([
        'type' => 'FeatureCollection',
        'features' => $features,
        'numberReturned' => count($features),
        'limit' => $limit
    ], JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE | JSON_INVALID_UTF8_SUBSTITUTE);
} catch (Throwable $e) {
    error_log('[gis-api] GeoJSON request failed: ' . get_class($e));
    gis_features_respond(503, ['ok' => false, 'error' => 'gis_query_failed']);
}
