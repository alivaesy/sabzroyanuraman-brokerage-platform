<?php
declare(strict_types=1);
require_once __DIR__ . '/auth-common.php';

gis_auth_method(['GET','POST']);
$method=(string)$_SERVER['REQUEST_METHOD'];
$pdo=gis_auth_db();
$user=gis_auth_current_user(true);
$root='/home/sabzroyanuraman/private/gis-raster-assets';
const MAX_RASTER_BYTES=104857600; // 100 MiB per upload

function raster_asset_id(string $id): string {
 if(!preg_match('/^[a-zA-Z0-9][a-zA-Z0-9._-]{0,95}$/',$id))gis_auth_json(400,['ok'=>false,'error'=>'invalid_asset_id']);
 return $id;
}
function raster_project(PDO $pdo,array $user,int $org,int $project,string $role): void {
 gis_auth_require_org($pdo,$user,$org,$role);
 gis_auth_require_org_subscription($pdo,$user,$org);
 $q=$pdo->prepare("SELECT 1 FROM public.gis_projects WHERE id=:p AND organization_id=:o AND status='active' LIMIT 1");
 $q->execute(['p'=>$project,'o'=>$org]);
 if(!$q->fetchColumn())gis_auth_json(404,['ok'=>false,'error'=>'project_not_found']);
}
function raster_paths(string $root,int $org,int $project,string $asset): array {
 $dir=$root.'/org-'.$org.'/project-'.$project;
 return [$dir,$dir.'/'.$asset.'.tif',$dir.'/'.$asset.'.json'];
}
function raster_audit(PDO $pdo,array $user,int $org,int $project,string $action,string $asset): void {
 $q=$pdo->prepare("INSERT INTO public.gis_admin_audit_log(actor_user_id,organization_id,action,target_type,target_id,details) VALUES(:u,:o,:a,'raster_asset',:id,CAST(:d AS jsonb))");
 $q->execute(['u'=>$user['id'],'o'=>$org,'a'=>$action,'id'=>$asset,'d'=>json_encode(['project_id'=>$project,'asset_id'=>$asset],JSON_UNESCAPED_SLASHES)]);
}
function raster_metadata(string $raw): array {
 if(strlen($raw)>32768)gis_auth_json(413,['ok'=>false,'error'=>'metadata_too_large']);
 try{$m=json_decode($raw,true,32,JSON_THROW_ON_ERROR);}catch(Throwable $e){gis_auth_json(400,['ok'=>false,'error'=>'invalid_raster_metadata']);}
 if(!is_array($m)||!isset($m['width'],$m['height'],$m['geoTransform'],$m['crs']['epsg'])||
    !is_int($m['width'])||!is_int($m['height'])||$m['width']<1||$m['height']<1||
    $m['width']*$m['height']>100000000||!is_array($m['geoTransform'])||count($m['geoTransform'])!==6||
    !is_numeric($m['crs']['epsg'])||(int)$m['crs']['epsg']<1)gis_auth_json(400,['ok'=>false,'error'=>'invalid_raster_metadata']);
 foreach($m['geoTransform'] as $v)if(!is_numeric($v)||!is_finite((float)$v))gis_auth_json(400,['ok'=>false,'error'=>'invalid_raster_metadata']);
 return $m;
}
if($method==='POST'&&(int)($_SERVER['CONTENT_LENGTH']??0)>MAX_RASTER_BYTES+65536)gis_auth_json(413,['ok'=>false,'error'=>'raster_upload_too_large']);
if($method==='GET'){
 $org=filter_var($_GET['organization_id']??null,FILTER_VALIDATE_INT);
 $project=filter_var($_GET['project_id']??null,FILTER_VALIDATE_INT);
 if(!$org||$org<1||!$project||$project<1)gis_auth_json(400,['ok'=>false,'error'=>'project_and_organization_required']);
 raster_project($pdo,$user,(int)$org,(int)$project,'viewer');
 $action=(string)($_GET['action']??'list');
 [$dir] = raster_paths($root,(int)$org,(int)$project,'placeholder');
 if($action==='list'){
  $items=[];
  if(is_dir($dir))foreach(glob($dir.'/*.json')?:[] as $file){
   $meta=json_decode((string)file_get_contents($file),true);
   if(!is_array($meta)||!isset($meta['asset_id'],$meta['file_name']))continue;
   $items[]=$meta;
  }
  usort($items,fn($a,$b)=>strcmp((string)($a['asset_id']??''),(string)($b['asset_id']??'')));
  gis_auth_json(200,['ok'=>true,'assets'=>$items]);
 }
 if($action==='download'){
  $asset=raster_asset_id((string)($_GET['asset_id']??''));
  [, $file] = raster_paths($root,(int)$org,(int)$project,$asset);
  if(!is_file($file)||!is_readable($file))gis_auth_json(404,['ok'=>false,'error'=>'raster_asset_not_found']);
  $name=$asset.'.tif';
  $metaFile=raster_paths($root,(int)$org,(int)$project,$asset)[2];
  if(is_file($metaFile)){$meta=json_decode((string)file_get_contents($metaFile),true);if(is_array($meta)&&isset($meta['file_name']))$name=(string)$meta['file_name'];}
  $name=preg_replace('/[^a-zA-Z0-9._ -]/','_',basename($name))?:($asset.'.tif');
  header('Content-Type: image/tiff');header('Content-Length: '.(string)filesize($file));header('Content-Disposition: attachment; filename="'.$name.'"');header('Cache-Control: private, no-store');readfile($file);exit;
 }
 gis_auth_json(400,['ok'=>false,'error'=>'invalid_action']);
}
gis_auth_csrf_check();
$org=filter_var($_POST['organization_id']??null,FILTER_VALIDATE_INT);
$project=filter_var($_POST['project_id']??null,FILTER_VALIDATE_INT);
if(!$org||$org<1||!$project||$project<1)gis_auth_json(400,['ok'=>false,'error'=>'project_and_organization_required']);
raster_project($pdo,$user,(int)$org,(int)$project,'editor');
$action=(string)($_POST['action']??'');
$asset=raster_asset_id((string)($_POST['asset_id']??''));
[$dir,$file,$metaFile]=raster_paths($root,(int)$org,(int)$project,$asset);
if($action==='upload'){
 if(!isset($_FILES['file'])||!is_array($_FILES['file'])||($_FILES['file']['error']??UPLOAD_ERR_NO_FILE)!==UPLOAD_ERR_OK)gis_auth_json(400,['ok'=>false,'error'=>'raster_upload_missing']);
 $tmp=(string)$_FILES['file']['tmp_name'];$size=(int)($_FILES['file']['size']??0);
 if($size<8||$size>MAX_RASTER_BYTES||!is_uploaded_file($tmp))gis_auth_json(413,['ok'=>false,'error'=>'raster_size_or_upload_invalid']);
 $head=file_get_contents($tmp,false,null,0,4);
 if($head!=="II*\0"&&$head!=="MM\0*"&&$head!=="II+\0"&&$head!=="MM\0+")gis_auth_json(415,['ok'=>false,'error'=>'geotiff_required']);
 $metadata=raster_metadata((string)($_POST['metadata']??''));
 $fileName=preg_replace('/[^a-zA-Z0-9._ -]/','_',basename((string)($_POST['file_name']??'raster.tif')))?:'raster.tif';
 if(!is_dir($dir)&&!mkdir($dir,0700,true)&&!is_dir($dir))gis_auth_json(503,['ok'=>false,'error'=>'raster_storage_unavailable']);
 if(!is_writable($dir))gis_auth_json(503,['ok'=>false,'error'=>'raster_storage_unavailable']);
 $storedBytes=0;foreach(glob($dir.'/*.tif')?:[] as $existingFile)$storedBytes+=(int)filesize($existingFile);
 if($storedBytes+$size>1073741824)gis_auth_json(413,['ok'=>false,'error'=>'project_raster_storage_quota_exceeded']);
 $tmpTarget=$file.'.'.bin2hex(random_bytes(8)).'.tmp';
 if(!move_uploaded_file($tmp,$tmpTarget))gis_auth_json(500,['ok'=>false,'error'=>'raster_upload_failed']);
 $meta=['asset_id'=>$asset,'file_name'=>$fileName,'size'=>$size,'metadata'=>$metadata,'sha256'=>hash_file('sha256',$tmpTarget),'uploaded_at'=>gmdate('c')];
 $metaTmp=$metaFile.'.'.bin2hex(random_bytes(6)).'.tmp';
 if(file_put_contents($metaTmp,json_encode($meta,JSON_UNESCAPED_SLASHES|JSON_UNESCAPED_UNICODE),LOCK_EX)===false||!rename($tmpTarget,$file)||!rename($metaTmp,$metaFile)){@unlink($tmpTarget);@unlink($metaTmp);gis_auth_json(500,['ok'=>false,'error'=>'raster_storage_failed']);}
 raster_audit($pdo,$user,(int)$org,(int)$project,'upload_raster_asset',$asset);
 gis_auth_json(200,['ok'=>true,'asset'=>$meta]);
}
if($action==='delete'){
 if(is_file($file))@unlink($file);if(is_file($metaFile))@unlink($metaFile);
 raster_audit($pdo,$user,(int)$org,(int)$project,'delete_raster_asset',$asset);
 gis_auth_json(200,['ok'=>true,'deleted'=>true]);
}
gis_auth_json(400,['ok'=>false,'error'=>'invalid_action']);
