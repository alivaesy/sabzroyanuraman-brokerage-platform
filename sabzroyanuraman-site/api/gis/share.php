<?php
declare(strict_types=1);
require_once __DIR__.'/auth-common.php';
gis_auth_method(['GET','POST']);
$method=(string)$_SERVER['REQUEST_METHOD'];
$pdo=gis_auth_db();

/* Public, read-only retrieval. Tokens are random 256-bit values and are revocable. */
if($method==='GET'){
  $token=(string)($_GET['token']??'');
  if(!preg_match('/^[a-f0-9]{64}$/',$token))gis_auth_json(404,['ok'=>false,'error'=>'share_not_found']);
  try{
    $q=$pdo->prepare("SELECT id,name,description,project_data,updated_at FROM public.gis_projects WHERE share_token=:t AND share_enabled=true AND status='active' LIMIT 1");
    $q->execute(['t'=>$token]);$p=$q->fetch();
    if(!$p)gis_auth_json(404,['ok'=>false,'error'=>'share_not_found']);
    $data=$p['project_data'];
    if(is_string($data)){try{$data=json_decode($data,true,512,JSON_THROW_ON_ERROR);}catch(Throwable $e){gis_auth_json(500,['ok'=>false,'error'=>'shared_project_invalid']);}}
    if(!is_array($data))gis_auth_json(500,['ok'=>false,'error'=>'shared_project_invalid']);
    header('Cache-Control: public, max-age=60, stale-while-revalidate=120');
    gis_auth_json(200,['ok'=>true,'project'=>['id'=>(int)$p['id'],'name'=>$p['name'],'description'=>$p['description'],'updated_at'=>$p['updated_at'],'project_data'=>$data]]);
  }catch(PDOException $e){error_log('[gis-share] public read failed: '.get_class($e));gis_auth_json(503,['ok'=>false,'error'=>'share_service_unavailable']);}
}

gis_auth_csrf_check();
$user=gis_auth_current_user(true);
$contentType=strtolower(trim(explode(';',(string)($_SERVER['CONTENT_TYPE']??''))[0]));
if($contentType!=='application/json')gis_auth_json(415,['ok'=>false,'error'=>'application_json_required']);
$raw=(string)file_get_contents('php://input');
if(strlen($raw)>20000)gis_auth_json(413,['ok'=>false,'error'=>'request_body_too_large']);
$b=json_decode($raw,true);
if(!is_array($b))gis_auth_json(400,['ok'=>false,'error'=>'invalid_json']);
$id=filter_var($b['id']??null,FILTER_VALIDATE_INT);
$org=filter_var($b['organization_id']??null,FILTER_VALIDATE_INT);
$action=(string)($b['action']??'');
if(!$id||$id<1||!$org||$org<1||!in_array($action,['create','revoke'],true))gis_auth_json(400,['ok'=>false,'error'=>'project_and_organization_required']);
gis_auth_require_org($pdo,$user,(int)$org,'editor');
gis_auth_require_org_subscription($pdo,$user,(int)$org);
try{
  if($action==='create'){
    $token=bin2hex(random_bytes(32));
    $q=$pdo->prepare("UPDATE public.gis_projects SET share_token=:t,share_enabled=true,updated_at=now() WHERE id=:id AND organization_id=:o AND status='active' RETURNING id");
    $q->execute(['t'=>$token,'id'=>$id,'o'=>$org]);
    if(!$q->fetch())gis_auth_json(404,['ok'=>false,'error'=>'project_not_found']);
    $url='https://sabzroyanuraman.com/web-gis.html?share='.$token;
    gis_auth_json(200,['ok'=>true,'token'=>$token,'url'=>$url]);
  }
  $q=$pdo->prepare("UPDATE public.gis_projects SET share_enabled=false,share_token=NULL,updated_at=now() WHERE id=:id AND organization_id=:o AND status='active' RETURNING id");
  $q->execute(['id'=>$id,'o'=>$org]);
  if(!$q->fetch())gis_auth_json(404,['ok'=>false,'error'=>'project_not_found']);
  gis_auth_json(200,['ok'=>true,'revoked'=>true]);
}catch(Throwable $e){error_log('[gis-share] share update failed: '.get_class($e));gis_auth_json(500,['ok'=>false,'error'=>'share_update_failed']);}
