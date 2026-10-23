<?php
declare(strict_types=1);

/**
 * Minimal, allow-list based PostGIS GeoJSON API.
 * Configure credentials and layer definitions through server environment variables.
 * Never put database credentials or GIS_API_TOKEN in browser code or this repository.
 */
header('Content-Type: application/json; charset=utf-8');
header('X-Content-Type-Options: nosniff');
header('Cache-Control: no-store');
header('Vary: Origin');

function respond(int $status, array $body): never {
  http_response_code($status);
  echo json_encode($body, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES | JSON_INVALID_UTF8_SUBSTITUTE);
  exit;
}
function envv(string $key, string $fallback = ''): string {
  $value = getenv($key);
  return $value === false ? $fallback : trim($value);
}
function ident(string $value): string {
  if (!preg_match('/^[A-Za-z_][A-Za-z0-9_]*$/', $value)) respond(500, ['error'=>'Invalid identifier in server layer configuration']);
  return '"' . $value . '"';
}
function configLayers(): array {
  $raw = envv('GIS_LAYERS_JSON', '{}');
  $layers = json_decode($raw, true);
  if (!is_array($layers)) respond(500, ['error'=>'GIS_LAYERS_JSON is invalid']);
  return $layers;
}
function db(): PDO {
  static $pdo = null;
  if ($pdo instanceof PDO) return $pdo;
  $dsn = envv('GIS_PG_DSN');
  $user = envv('GIS_PG_USER');
  $pass = envv('GIS_PG_PASSWORD');
  if ($dsn === '' || $user === '') respond(503, ['error'=>'PostGIS API is not configured']);
  try {
    $pdo = new PDO($dsn, $user, $pass, [
      PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
      PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
      PDO::ATTR_EMULATE_PREPARES => false,
    ]);
    return $pdo;
  } catch (Throwable $e) {
    error_log('GIS database connection failed: '.$e->getMessage());
    respond(503, ['error'=>'Database unavailable']);
  }
}
function authenticate(): string {
  $expected = envv('GIS_API_TOKEN');
  if ($expected === '') respond(503, ['error'=>'GIS API authentication is not configured']);
  $header = $_SERVER['HTTP_AUTHORIZATION'] ?? '';
  if (!preg_match('/^Bearer\s+(.+)$/i', $header, $m) || !hash_equals($expected, trim($m[1]))) {
    header('WWW-Authenticate: Bearer');
    respond(401, ['error'=>'Authentication required']);
  }
  return envv('GIS_AUDIT_ACTOR', 'api-user');
}
$origin = $_SERVER['HTTP_ORIGIN'] ?? '';
$allowed = array_filter(array_map('trim', explode(',', envv('GIS_ALLOWED_ORIGINS'))));
if ($origin !== '' && in_array($origin, $allowed, true)) {
  header('Access-Control-Allow-Origin: '.$origin);
  header('Access-Control-Allow-Headers: Authorization, Content-Type, If-Match');
  header('Access-Control-Allow-Methods: GET, POST, OPTIONS');
}
if (($_SERVER['REQUEST_METHOD'] ?? 'GET') === 'OPTIONS') { http_response_code(204); exit; }
$actor = authenticate();
$method = $_SERVER['REQUEST_METHOD'] ?? 'GET';
$action = $_GET['action'] ?? 'health';
$layers = configLayers();

if ($action === 'health' && $method === 'GET') {
  try { db()->query('SELECT PostGIS_Version()')->fetchColumn(); respond(200, ['ok'=>true,'service'=>'postgis-gis-api']); }
  catch (Throwable $e) { error_log('GIS health check failed: '.$e->getMessage()); respond(503, ['error'=>'PostGIS unavailable']); }
}
if ($action === 'layers' && $method === 'GET') {
  $result = [];
  foreach ($layers as $name=>$cfg) {
    if (!is_array($cfg)) continue;
    $result[] = ['name'=>(string)$name,'title'=>(string)($cfg['title'] ?? $name),'geometryType'=>(string)($cfg['geometry_type'] ?? 'Unknown'),'editable'=>(bool)($cfg['editable'] ?? false)];
  }
  respond(200, ['layers'=>$result]);
}
$name = (string)($_GET['layer'] ?? '');
if ($name === '' || !isset($layers[$name]) || !is_array($layers[$name])) respond(404, ['error'=>'Unknown layer']);
$cfg = $layers[$name];
$table = ident((string)($cfg['table'] ?? ''));
$schema = ident((string)($cfg['schema'] ?? 'public'));
$idCol = ident((string)($cfg['id_column'] ?? 'id'));
$geomCol = ident((string)($cfg['geometry_column'] ?? 'geom'));
$srid = (int)($cfg['srid'] ?? 4326);
if ($srid < 1 || $srid > 999999) respond(500, ['error'=>'Invalid SRID in layer configuration']);
$qualified = $schema.'.'.$table;
$properties = array_values(array_filter((array)($cfg['properties'] ?? []), fn($p)=>is_string($p) && preg_match('/^[A-Za-z_][A-Za-z0-9_]*$/',$p)));
$propertySql = implode(', ', array_map('ident', $properties));
$geomType = strtoupper((string)($cfg['geometry_type'] ?? 'GEOMETRY'));

if ($action === 'features' && $method === 'GET') {
  $limit = max(1, min(2000, (int)($_GET['limit'] ?? 500)));
  $offset = max(0, min(10000000, (int)($_GET['offset'] ?? 0)));
  $where = [];
  $params = [];
  if (isset($_GET['bbox'])) {
    $parts = array_map('floatval', explode(',', (string)$_GET['bbox']));
    if (count($parts)!==4 || !($parts[0] < $parts[2]) || !($parts[1] < $parts[3]) || abs($parts[0])>180 || abs($parts[2])>180 || abs($parts[1])>90 || abs($parts[3])>90) respond(400, ['error'=>'bbox must be minLon,minLat,maxLon,maxLat in EPSG:4326']);
    $where[] = 'ST_Intersects('.$geomCol.', ST_Transform(ST_MakeEnvelope(:xmin,:ymin,:xmax,:ymax,4326), :srid))';
    $params += [':xmin'=>$parts[0],':ymin'=>$parts[1],':xmax'=>$parts[2],':ymax'=>$parts[3],':srid'=>$srid];
  }
  $sqlWhere = $where ? ' WHERE '.implode(' AND ',$where) : '';
  $count = db()->prepare('SELECT COUNT(*) FROM '.$qualified.$sqlWhere);
  foreach ($params as $k=>$v) $count->bindValue($k,$v);
  $count->execute(); $total=(int)$count->fetchColumn();
  $selectProps = $propertySql === '' ? "'{}'::jsonb" : 'to_jsonb(t) - '.$geomCol;
  $sql = 'SELECT '.$idCol.'::text AS __id, ST_AsGeoJSON(ST_Transform('.$geomCol.',4326),7)::json AS __geometry, '.$selectProps.' AS __properties FROM '.$qualified.' t'.$sqlWhere.' ORDER BY '.$idCol.' LIMIT :lim OFFSET :off';
  $stmt=db()->prepare($sql);
  foreach($params as $k=>$v) $stmt->bindValue($k,$v);
  $stmt->bindValue(':lim',$limit,PDO::PARAM_INT);$stmt->bindValue(':off',$offset,PDO::PARAM_INT);$stmt->execute();
  $features=[];
  foreach($stmt->fetchAll() as $row) {
    $props=json_decode((string)$row['__properties'],true); if(!is_array($props)) $props=[];
    if($properties) $props=array_intersect_key($props,array_flip($properties));
    $props['_server_id']=$row['__id'];
    $geometry=json_decode((string)$row['__geometry'],true);
    $features[]=['type'=>'Feature','id'=>$row['__id'],'geometry'=>$geometry,'properties'=>$props];
  }
  respond(200,['type'=>'FeatureCollection','features'=>$features,'numberReturned'=>count($features),'numberMatched'=>$total,'limit'=>$limit,'offset'=>$offset,'nextOffset'=>($offset+count($features)<$total)?$offset+count($features):null]);
}
if ($action === 'edits' && $method === 'POST') {
  if (!(bool)($cfg['editable'] ?? false)) respond(403,['error'=>'This layer is read-only']);
  $body=json_decode(file_get_contents('php://input') ?: '',true);
  if (!is_array($body) || !is_array($body['edits'] ?? null) || count($body['edits'])<1 || count($body['edits'])>100) respond(400,['error'=>'Send 1–100 edits in an edits array']);
  $allowedProps=array_flip($properties);
  $pdo=db();
  try {
    $pdo->beginTransaction();
    $pdo->exec("CREATE TABLE IF NOT EXISTS public.gis_edit_audit (audit_id bigserial PRIMARY KEY, layer_name text NOT NULL, feature_id text, operation text NOT NULL, actor text NOT NULL, changed_at timestamptz NOT NULL DEFAULT now(), before_data jsonb, after_data jsonb)");
    $results=[];
    foreach($body['edits'] as $edit) {
      if(!is_array($edit)) throw new RuntimeException('Invalid edit item');
      $op=strtolower((string)($edit['operation'] ?? ''));
      $id=(string)($edit['id'] ?? '');
      if(!in_array($op,['insert','update','delete'],true)) throw new RuntimeException('Operation must be insert, update, or delete');
      $before=null;$after=null;
      if($op!=='insert') {
        if($id==='') throw new RuntimeException('Feature id is required');
        $q=$pdo->prepare('SELECT to_jsonb(t) AS row_data FROM '.$qualified.' t WHERE '.$idCol.'::text=:id FOR UPDATE');
        $q->execute([':id'=>$id]);$before=$q->fetchColumn();
        if($before===false) throw new RuntimeException('Feature not found: '.$id);
        $beforeData=json_decode((string)$before,true);
        if(isset($edit['expected_updated_at']) && isset($beforeData['updated_at']) && (string)$edit['expected_updated_at']!==(string)$beforeData['updated_at']) throw new RuntimeException('Edit conflict: feature changed on server; reload before retrying');
      }
      if($op==='delete') {
        $q=$pdo->prepare('DELETE FROM '.$qualified.' WHERE '.$idCol.'::text=:id');$q->execute([':id'=>$id]);
      } else {
        $props=$edit['properties'] ?? [];
        if(!is_array($props)) throw new RuntimeException('properties must be an object');
        $clean=[];
        foreach($props as $key=>$val) { if(!is_string($key)||!isset($allowedProps[$key])) throw new RuntimeException('Property is not editable: '.(string)$key); $clean[$key]=$val; }
        if($op==='insert') {
          $geom=$edit['geometry'] ?? null;if(!is_array($geom)) throw new RuntimeException('GeoJSON geometry is required');
          $cols=[];$vals=[];$bind=[];
          foreach($clean as $key=>$val){$cols[]=ident($key);$ph=':p'.count($bind);$vals[]=$ph;$bind[$ph]=is_array($val)?json_encode($val):$val;}
          $cols[]=$geomCol;$vals[]='ST_Transform(ST_SetSRID(ST_GeomFromGeoJSON(:geom),4326),'.$srid.')';$bind[':geom']=json_encode($geom);
          $q=$pdo->prepare('INSERT INTO '.$qualified.' ('.implode(',',$cols).') VALUES ('.implode(',',$vals).') RETURNING '.$idCol.'::text');$q->execute($bind);$id=(string)$q->fetchColumn();
        } else {
          $sets=[];$bind=[':id'=>$id];
          foreach($clean as $key=>$val){$ph=':p'.count($bind);$sets[]=ident($key).'='.$ph;$bind[$ph]=is_array($val)?json_encode($val):$val;}
          if(isset($edit['geometry'])) {if(!is_array($edit['geometry'])) throw new RuntimeException('Invalid GeoJSON geometry');$sets[]=$geomCol.'=ST_Transform(ST_SetSRID(ST_GeomFromGeoJSON(:geom),4326),'.$srid.')';$bind[':geom']=json_encode($edit['geometry']);}
          if(!$sets) throw new RuntimeException('Update contains no changes');
          $q=$pdo->prepare('UPDATE '.$qualified.' SET '.implode(',',$sets).' WHERE '.$idCol.'::text=:id RETURNING to_jsonb('.$table.')');$q->execute($bind);$after=$q->fetchColumn();
        }
      }
      if($op==='insert' || $op==='update') {
        if($op==='insert') {$q=$pdo->prepare('SELECT to_jsonb(t) FROM '.$qualified.' t WHERE '.$idCol.'::text=:id');$q->execute([':id'=>$id]);$after=$q->fetchColumn();}
        $results[]=['operation'=>$op,'id'=>$id];
      } else $results[]=['operation'=>$op,'id'=>$id];
      $audit=$pdo->prepare('INSERT INTO public.gis_edit_audit(layer_name,feature_id,operation,actor,before_data,after_data) VALUES(:layer,:id,:op,:actor,CAST(:before AS jsonb),CAST(:after AS jsonb))');
      $audit->execute([':layer'=>$name,':id'=>$id,':op'=>$op,':actor'=>$actor,':before'=>$before===false?null:$before,':after'=>$after===false?null:$after]);
    }
    $pdo->commit();
    respond(200,['ok'=>true,'results'=>$results]);
  } catch(Throwable $e) {
    if($pdo->inTransaction()) $pdo->rollBack();
    error_log('GIS edit transaction failed: '.$e->getMessage());
    $status=str_starts_with($e->getMessage(),'Edit conflict:')?409:400;
    respond($status,['error'=>$e->getMessage()]);
  }
}
respond(404,['error'=>'Unknown action or unsupported HTTP method']);
