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

function raster_u16(string $b,int $o,bool $le): int {
 if($o<0||$o+2>strlen($b))gis_auth_json(415,['ok'=>false,'error'=>'invalid_geotiff_structure']);
 $v=unpack($le?'v':'n',substr($b,$o,2));return (int)$v[1];
}
function raster_u32(string $b,int $o,bool $le): int {
 if($o<0||$o+4>strlen($b))gis_auth_json(415,['ok'=>false,'error'=>'invalid_geotiff_structure']);
 $v=unpack($le?'V':'N',substr($b,$o,4));return (int)$v[1];
}
function raster_validate_geotiff(string $path,array $metadata): void {
 $size=filesize($path);if($size===false||$size<14)gis_auth_json(415,['ok'=>false,'error'=>'invalid_geotiff_structure']);
 $h=file_get_contents($path,false,null,0,8);
 if(strlen($h)!==8)gis_auth_json(415,['ok'=>false,'error'=>'invalid_geotiff_structure']);
 $le=substr($h,0,2)==='II';
 if(!$le&&substr($h,0,2)!=='MM')gis_auth_json(415,['ok'=>false,'error'=>'invalid_geotiff_structure']);
 // Classic TIFF only; BigTIFF is rejected until its 64-bit IFD parser is available.
 if(raster_u16($h,2,$le)!==42)gis_auth_json(415,['ok'=>false,'error'=>'unsupported_or_invalid_tiff_variant']);
 $ifd=raster_u32($h,4,$le);if($ifd<8||$ifd+2>$size)gis_auth_json(415,['ok'=>false,'error'=>'invalid_geotiff_ifd']);
 $count=raster_u16(file_get_contents($path,false,null,$ifd,2)?:'',0,$le);
 if($count<1||$count>4096||$ifd+2+($count*12)+4>$size)gis_auth_json(415,['ok'=>false,'error'=>'invalid_geotiff_ifd']);
 $fp=fopen($path,'rb');if(!$fp)gis_auth_json(415,['ok'=>false,'error'=>'invalid_geotiff_structure']);
 $entries=[];$typeSize=[1=>1,2=>1,3=>2,4=>4,5=>8,6=>1,7=>1,8=>2,9=>4,10=>8,11=>4,12=>8];
 for($i=0;$i<$count;$i++){
  fseek($fp,$ifd+2+$i*12);$e=fread($fp,12);if(strlen($e)!==12){fclose($fp);gis_auth_json(415,['ok'=>false,'error'=>'invalid_geotiff_ifd']);}
  $tag=raster_u16($e,0,$le);$type=raster_u16($e,2,$le);$n=raster_u32($e,4,$le);
  if(!isset($typeSize[$type])||$n<1||$n>100000000){fclose($fp);gis_auth_json(415,['ok'=>false,'error'=>'invalid_geotiff_tag']);}
  $bytes=$typeSize[$type]*$n;$pos=$bytes<=4?($ifd+2+$i*12+8):raster_u32($e,8,$le);
  if($pos<0||$pos+$bytes>$size){fclose($fp);gis_auth_json(415,['ok'=>false,'error'=>'invalid_geotiff_tag_offset']);}
  $entries[$tag]=['type'=>$type,'count'=>$n,'pos'=>$pos,'bytes'=>$bytes];
 }
 $values=function(int $tag)use($entries,$fp,$le):array{
  if(!isset($entries[$tag]))return [];
  $e=$entries[$tag];fseek($fp,$e['pos']);$raw=fread($fp,$e['bytes']);if(strlen($raw)!==$e['bytes'])return [];
  $out=[];$step=$e['type']===3?2:($e['type']===4?4:0);
  if($step===0)return [];
  for($j=0;$j<$e['count'];$j++)$out[]=$step===2?raster_u16($raw,$j*2,$le):raster_u32($raw,$j*4,$le);
  return $out;
 };
 $w=$values(256)[0]??0;$hgt=$values(257)[0]??0;
 $offsets=$values(273);$lengths=$values(279);
 if(!$offsets||!$lengths){$offsets=$values(324);$lengths=$values(325);}
 $hasTransform=isset($entries[34264])&&$entries[34264]['count']>=16;
 $hasTieScale=isset($entries[33922])&&$entries[33922]['count']>=6&&isset($entries[33550])&&$entries[33550]['count']>=3;
 $geoKeys=$values(34735);$tiffEpsg=0;
 if(count($geoKeys)>=4){
  $keyCount=min((int)$geoKeys[3],256);
  for($k=0;$k<$keyCount;$k++){
   $base=4+$k*4;if(!isset($geoKeys[$base+3]))break;
   if(in_array($geoKeys[$base], [2048,3072], true)&&$geoKeys[$base+1]===0&&$geoKeys[$base+2]===1){$tiffEpsg=(int)$geoKeys[$base+3];break;}
  }
 }
 if($w<1||$hgt<1||$w>1000000||$hgt>1000000||$w*$hgt>100000000||
    $w!==$metadata['width']||$hgt!==$metadata['height']||$tiffEpsg!==(int)$metadata['crs']['epsg']||
    !$hasKeys||(!$hasTransform&&!$hasTieScale)||!$offsets||count($offsets)!==count($lengths)){
  fclose($fp);gis_auth_json(415,['ok'=>false,'error'=>'geotiff_metadata_or_image_structure_invalid']);
 }
 foreach($offsets as $i=>$offset)if($lengths[$i]<1||$offset<8||$offset+$lengths[$i]>$size){
  fclose($fp);gis_auth_json(415,['ok'=>false,'error'=>'geotiff_image_block_out_of_bounds']);
 }
 fclose($fp);
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
  $quotaLock=fopen($dir.'/.quota.lock','c');
  if($quotaLock===false||!flock($quotaLock,LOCK_SH)){if(is_resource($quotaLock))fclose($quotaLock);gis_auth_json(503,['ok'=>false,'error'=>'raster_storage_busy']);}
  [, $legacyFile] = raster_paths($root,(int)$org,(int)$project,$asset);
  $metaFile=raster_paths($root,(int)$org,(int)$project,$asset)[2];
  $meta=is_file($metaFile)?json_decode((string)file_get_contents($metaFile),true):null;
  $stored=is_array($meta)&&isset($meta['storage_file'])?(string)$meta['storage_file']:basename($legacyFile);
  if($stored!==basename($stored)||!preg_match('/^[a-zA-Z0-9._-]+\.tif$/',$stored)){flock($quotaLock,LOCK_UN);fclose($quotaLock);gis_auth_json(500,['ok'=>false,'error'=>'invalid_raster_storage_reference']);}
  $file=dirname($legacyFile).'/'.$stored;
  $handle=(!is_link($file)&&is_file($file))?fopen($file,'rb'):false;
  if($handle===false){flock($quotaLock,LOCK_UN);fclose($quotaLock);gis_auth_json(404,['ok'=>false,'error'=>'raster_asset_not_found']);}
  $fileStat=fstat($handle);flock($quotaLock,LOCK_UN);fclose($quotaLock);
  $name=is_array($meta)&&isset($meta['file_name'])?(string)$meta['file_name']:($asset.'.tif');
  $name=preg_replace('/[^a-zA-Z0-9._ -]/','_',basename($name))?:($asset.'.tif');
  header('Content-Type: image/tiff');header('Content-Length: '.(string)$fileStat['size']);header('Content-Disposition: attachment; filename="'.$name.'"');header('Cache-Control: private, no-store');fpassthru($handle);fclose($handle);exit;
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
 $metadata=raster_metadata((string)($_POST['metadata']??''));
 raster_validate_geotiff($tmp,$metadata);
 $fileName=preg_replace('/[^a-zA-Z0-9._ -]/','_',basename((string)($_POST['file_name']??'raster.tif')))?:'raster.tif';
 if(!is_dir($dir)&&!mkdir($dir,0700,true)&&!is_dir($dir))gis_auth_json(503,['ok'=>false,'error'=>'raster_storage_unavailable']);
 if(!is_writable($dir))gis_auth_json(503,['ok'=>false,'error'=>'raster_storage_unavailable']);
 $quotaLock=fopen($dir.'/.quota.lock','c');
 if($quotaLock===false||!flock($quotaLock,LOCK_EX)){if(is_resource($quotaLock))fclose($quotaLock);gis_auth_json(503,['ok'=>false,'error'=>'raster_storage_busy']);}
 $storedBytes=0;foreach(glob($dir.'/*.tif')?:[] as $existingFile){if(is_file($existingFile)&&!is_link($existingFile))$storedBytes+=(int)filesize($existingFile);}
 $oldMeta=is_file($metaFile)?json_decode((string)file_get_contents($metaFile),true):null;
 $oldStored=is_array($oldMeta)&&isset($oldMeta['storage_file'])?(string)$oldMeta['storage_file']:basename($file);
 $oldPath=$dir.'/'.$oldStored;
 $previousBytes=is_file($oldPath)&&!is_link($oldPath)?(int)filesize($oldPath):0;
 if($storedBytes-$previousBytes+$size>1073741824){flock($quotaLock,LOCK_UN);fclose($quotaLock);gis_auth_json(413,['ok'=>false,'error'=>'project_raster_storage_quota_exceeded']);}
 $stage=$dir.'/.upload-'.bin2hex(random_bytes(10)).'.tmp';
 if(!move_uploaded_file($tmp,$stage)){flock($quotaLock,LOCK_UN);fclose($quotaLock);gis_auth_json(500,['ok'=>false,'error'=>'raster_upload_failed']);}
 $hash=hash_file('sha256',$stage);$storedName=$asset.'.'.$hash.'.tif';$storedPath=$dir.'/'.$storedName;
 $meta=['asset_id'=>$asset,'file_name'=>$fileName,'storage_file'=>$storedName,'size'=>$size,'metadata'=>$metadata,'sha256'=>$hash,'uploaded_at'=>gmdate('c')];
 $metaTmp=$metaFile.'.'.bin2hex(random_bytes(6)).'.tmp';
 $encoded=json_encode($meta,JSON_UNESCAPED_SLASHES|JSON_UNESCAPED_UNICODE);
 $ok=$encoded!==false&&file_put_contents($metaTmp,$encoded,LOCK_EX)!==false;
 if($ok&&!is_file($storedPath))$ok=rename($stage,$storedPath);
 else @unlink($stage);
 // The metadata pointer is the commit record: its atomic rename switches the
 // asset to the immutable content-addressed TIFF. A failed commit leaves the
 // previous metadata and raster untouched.
 if($ok)$ok=rename($metaTmp,$metaFile);
 if(!$ok){@unlink($stage);@unlink($metaTmp);if(is_file($storedPath)&&(!is_array($oldMeta)||($oldMeta['storage_file']??'')!==$storedName))@unlink($storedPath);flock($quotaLock,LOCK_UN);fclose($quotaLock);gis_auth_json(500,['ok'=>false,'error'=>'raster_storage_failed']);}
 if($oldStored!==$storedName&&$oldStored===basename($oldStored)&&preg_match('/^[a-zA-Z0-9._-]+\.tif$/',$oldStored))@unlink($oldPath);
 flock($quotaLock,LOCK_UN);fclose($quotaLock);
 raster_audit($pdo,$user,(int)$org,(int)$project,'upload_raster_asset',$asset);
 gis_auth_json(200,['ok'=>true,'asset'=>$meta]);
}
if($action==='delete'){
 if(!is_dir($dir)){raster_audit($pdo,$user,(int)$org,(int)$project,'delete_raster_asset',$asset);gis_auth_json(200,['ok'=>true,'deleted'=>true]);}
 $quotaLock=fopen($dir.'/.quota.lock','c');
 if($quotaLock===false||!flock($quotaLock,LOCK_EX)){if(is_resource($quotaLock))fclose($quotaLock);gis_auth_json(503,['ok'=>false,'error'=>'raster_storage_busy']);}
 $deleteMeta=is_file($metaFile)?json_decode((string)file_get_contents($metaFile),true):null;
 $deleteStored=is_array($deleteMeta)&&isset($deleteMeta['storage_file'])?(string)$deleteMeta['storage_file']:basename($file);
 // Remove the metadata pointer first while holding the project lock, then its
 // immutable blob. Readers either finish opening the old blob or see not-found.
 if(is_file($metaFile)&&!unlink($metaFile)){flock($quotaLock,LOCK_UN);fclose($quotaLock);gis_auth_json(500,['ok'=>false,'error'=>'raster_delete_failed']);}
 if($deleteStored===basename($deleteStored)&&preg_match('/^[a-zA-Z0-9._-]+\.tif$/',$deleteStored))@unlink($dir.'/'.$deleteStored);
 flock($quotaLock,LOCK_UN);fclose($quotaLock);
 raster_audit($pdo,$user,(int)$org,(int)$project,'delete_raster_asset',$asset);
 gis_auth_json(200,['ok'=>true,'deleted'=>true]);
}
gis_auth_json(400,['ok'=>false,'error'=>'invalid_action']);
