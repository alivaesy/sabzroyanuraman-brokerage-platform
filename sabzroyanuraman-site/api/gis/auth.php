<?php
declare(strict_types=1); require_once __DIR__.'/auth-common.php'; gis_auth_method(['GET','POST','DELETE']); $action=(string)($_GET['action']??'');
if(($_SERVER['REQUEST_METHOD']??'')==='GET'&&$action==='csrf'){$token=bin2hex(random_bytes(32));gis_auth_cookie('gis_csrf',$token,time()+86400);gis_auth_json(200,['ok'=>true,'csrf_token'=>$token]);}
if(in_array(($_SERVER['REQUEST_METHOD']??''),['POST','PATCH'],true)){$length=(int)($_SERVER['CONTENT_LENGTH']??0);if($length>2100000)gis_auth_json(413,['ok'=>false,'error'=>'request_body_too_large']);}
$pdo=gis_auth_db();
if(($_SERVER['REQUEST_METHOD']??'')==='GET'){
 if($action!=='me')gis_auth_json(400,['ok'=>false,'error'=>'invalid_action']);
 $user=gis_auth_current_user(false);$q=$pdo->prepare("SELECT o.id,o.name,o.slug,m.role,EXISTS(SELECT 1 FROM public.gis_subscriptions s WHERE s.organization_id=o.id AND s.status='active' AND (s.starts_at IS NULL OR s.starts_at<=now()) AND (s.expires_at IS NULL OR s.expires_at>now())) AS subscribed FROM public.gis_organization_members m JOIN public.gis_organizations o ON o.id=m.organization_id WHERE m.user_id=:u AND m.status='active' AND o.status='active' ORDER BY o.name");$q->execute(['u'=>$user['id']]);
 gis_auth_json(200,['ok'=>true,'user'=>['id'=>$user['id'],'username'=>$user['username'],'email'=>$user['email'],'display_name'=>$user['display_name'],'is_platform_admin'=>$user['is_platform_admin']],'organizations'=>$q->fetchAll()]);
}
gis_auth_csrf_check();
if(($_SERVER['REQUEST_METHOD']??'')==='POST'&&$action==='login'){
 $contentType=strtolower(trim(explode(';',(string)($_SERVER['CONTENT_TYPE']??''))[0]));if($contentType!=='application/json')gis_auth_json(415,['ok'=>false,'error'=>'application_json_required']);$contentLength=filter_var($_SERVER['CONTENT_LENGTH']??0,FILTER_VALIDATE_INT);if($contentLength!==false&&$contentLength>2100000)gis_auth_json(413,['ok'=>false,'error'=>'request_body_too_large']);$rawBody=(string)file_get_contents('php://input');if(strlen($rawBody)>2100000)gis_auth_json(413,['ok'=>false,'error'=>'request_body_too_large']);$b=json_decode($rawBody,true);if(!is_array($b))gis_auth_json(400,['ok'=>false,'error'=>'invalid_json']);$name=strtolower(trim((string)($b['username']??'')));$pass=(string)($b['password']??'');
 if(!preg_match('/^[a-z0-9][a-z0-9._-]{2,63}$/',$name)||$pass===''||strlen($pass)>1024)gis_auth_json(401,['ok'=>false,'error'=>'invalid_credentials']);
 $identityHash=hash('sha256',strtolower((string)($_SERVER['REMOTE_ADDR']??'unknown')).'|'.$name);// Old login-attempt rows are retained for a bounded server-side cleanup job; the API role has no DELETE privilege.$q=$pdo->prepare("SELECT count(*) FROM public.gis_login_attempts WHERE identity_hash=:h AND succeeded=false AND attempted_at>now()-interval '15 minutes'");$q->execute(['h'=>$identityHash]);if((int)$q->fetchColumn()>=8)gis_auth_json(429,['ok'=>false,'error'=>'login_rate_limited']);
 $q=$pdo->prepare("SELECT id,password_hash,status FROM public.gis_users WHERE username=:u LIMIT 1");$q->execute(['u'=>$name]);$r=$q->fetch();$valid=$r&&$r['status']==='active'&&password_verify($pass,(string)$r['password_hash']);
 $q=$pdo->prepare("INSERT INTO public.gis_login_attempts(identity_hash,succeeded) VALUES(:h,:ok)");$q->execute(['h'=>$identityHash,'ok'=>$valid?'true':'false']);
 if(!$valid)gis_auth_json(401,['ok'=>false,'error'=>'invalid_credentials']);

 $raw=bin2hex(random_bytes(32));$pdo->beginTransaction();try{$pdo->prepare("UPDATE public.gis_sessions SET revoked_at=now() WHERE user_id=:u AND revoked_at IS NULL")->execute(['u'=>$r['id']]);$pdo->prepare("INSERT INTO public.gis_sessions(user_id,token_hash,expires_at,user_agent) VALUES(:u,:h,now()+interval '12 hours',:ua)")->execute(['u'=>$r['id'],'h'=>hash('sha256',$raw),'ua'=>substr((string)($_SERVER['HTTP_USER_AGENT']??''),0,1000)]);$pdo->prepare("UPDATE public.gis_users SET last_login_at=now(),updated_at=now() WHERE id=:u")->execute(['u'=>$r['id']]);$pdo->commit();}catch(Throwable $e){if($pdo->inTransaction())$pdo->rollBack();error_log('[gis-auth] login failed: '.get_class($e));gis_auth_json(500,['ok'=>false,'error'=>'login_failed']);}
 gis_auth_cookie('gis_session',$raw,time()+43200);gis_auth_json(200,['ok'=>true,'message'=>'login_success']);
}
if(($_SERVER['REQUEST_METHOD']??'')==='DELETE'&&$action==='logout'){$raw=(string)($_COOKIE['gis_session']??'');if(preg_match('/^[a-f0-9]{64}$/',$raw))$pdo->prepare("UPDATE public.gis_sessions SET revoked_at=now() WHERE token_hash=:h AND revoked_at IS NULL")->execute(['h'=>hash('sha256',$raw)]);gis_auth_cookie('gis_session','',time()-3600);gis_auth_cookie('gis_csrf','',time()-3600);gis_auth_json(200,['ok'=>true,'message'=>'logout_success']);}
gis_auth_json(400,['ok'=>false,'error'=>'invalid_action']);
