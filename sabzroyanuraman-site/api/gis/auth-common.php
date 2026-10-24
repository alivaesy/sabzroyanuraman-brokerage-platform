<?php
declare(strict_types=1);
if (!defined('GIS_AUTH_API_BOOTSTRAPPED')) define('GIS_AUTH_API_BOOTSTRAPPED', true);
function gis_auth_json(int $status, array $payload): never {
 http_response_code($status); header('Content-Type: application/json; charset=utf-8'); header('Cache-Control: no-store, max-age=0');
 header('X-Content-Type-Options: nosniff'); header('Referrer-Policy: same-origin');
 echo json_encode($payload, JSON_UNESCAPED_SLASHES|JSON_UNESCAPED_UNICODE); exit;
}
function gis_auth_db(): PDO {
 static $pdo=null; if($pdo instanceof PDO)return $pdo;
 $file='/home/sabzroyanuraman/private/gis-auth-db.php';
 if(!is_file($file)||!is_readable($file))gis_auth_json(503,['ok'=>false,'error'=>'auth_service_not_configured']);
 $c=require $file; foreach(['host','port','database','username','password'] as $k) if(!is_array($c)||!isset($c[$k])||!is_scalar($c[$k])||(string)$c[$k]==='')gis_auth_json(503,['ok'=>false,'error'=>'auth_service_not_configured']);
 try{$pdo=new PDO(sprintf('pgsql:host=%s;port=%d;dbname=%s;connect_timeout=3',(string)$c['host'],(int)$c['port'],(string)$c['database']),(string)$c['username'],(string)$c['password'],[PDO::ATTR_ERRMODE=>PDO::ERRMODE_EXCEPTION,PDO::ATTR_DEFAULT_FETCH_MODE=>PDO::FETCH_ASSOC,PDO::ATTR_EMULATE_PREPARES=>false,PDO::ATTR_TIMEOUT=>4]);return $pdo;}
 catch(Throwable $e){error_log('[gis-auth] DB connect failed: '.get_class($e));gis_auth_json(503,['ok'=>false,'error'=>'auth_database_unavailable']);}
}
function gis_auth_method(array $allowed):void{$m=strtoupper((string)($_SERVER['REQUEST_METHOD']??'GET'));if(!in_array($m,$allowed,true)){header('Allow: '.implode(', ',$allowed));gis_auth_json(405,['ok'=>false,'error'=>'method_not_allowed']);}}
function gis_auth_cookie(string $name,string $value,int $expires):void{setcookie($name,$value,['expires'=>$expires,'path'=>'/api/gis/','secure'=>(!empty($_SERVER['HTTPS'])&&strtolower((string)$_SERVER['HTTPS'])!=='off'),'httponly'=>$name==='gis_session','samesite'=>'Strict']);}
function gis_auth_csrf_check():void{
 $origin=(string)($_SERVER['HTTP_ORIGIN']??'');$host=strtolower((string)($_SERVER['HTTP_HOST']??''));
 if($origin!==''){$p=parse_url($origin);$hp=parse_url('http://'.$host);$oh=strtolower((string)($p['host']??''));$hh=strtolower((string)($hp['host']??''));$os=strtolower((string)($p['scheme']??''));$https=(!empty($_SERVER['HTTPS'])&&strtolower((string)$_SERVER['HTTPS'])!=='off');$expectedScheme=$https?'https':'http';
 if(!is_array($p)||!in_array($os,['http','https'],true)||$oh===''||$oh!==$hh||$os!==$expectedScheme||isset($p['user'])||isset($p['pass']))gis_auth_json(403,['ok'=>false,'error'=>'origin_rejected']);}
 $c=(string)($_COOKIE['gis_csrf']??'');$h=(string)($_SERVER['HTTP_X_CSRF_TOKEN']??'');
 if($c===''||$h===''||!hash_equals($c,$h))gis_auth_json(403,['ok'=>false,'error'=>'csrf_failed']);
}
function gis_auth_current_user(bool $requireSubscription=false):array{
 $raw=(string)($_COOKIE['gis_session']??'');if(!preg_match('/^[a-f0-9]{64}$/',$raw))gis_auth_json(401,['ok'=>false,'error'=>'authentication_required']);
 $pdo=gis_auth_db();$q=$pdo->prepare("SELECT u.id,u.username,u.email,u.display_name,u.status,u.is_platform_admin,s.id AS session_id FROM public.gis_sessions s JOIN public.gis_users u ON u.id=s.user_id WHERE s.token_hash=:h AND s.revoked_at IS NULL AND s.expires_at>now() LIMIT 1");$q->execute(['h'=>hash('sha256',$raw)]);$u=$q->fetch();
 if(!$u||$u['status']!=='active')gis_auth_json(401,['ok'=>false,'error'=>'authentication_required']);
 if($requireSubscription){$q=$pdo->prepare("SELECT 1 FROM public.gis_subscriptions s WHERE s.status='active' AND (s.starts_at IS NULL OR s.starts_at<=now()) AND (s.expires_at IS NULL OR s.expires_at>now()) AND (s.user_id=:uid OR s.organization_id IN (SELECT m.organization_id FROM public.gis_organization_members m JOIN public.gis_organizations o ON o.id=m.organization_id WHERE m.user_id=:uid2 AND m.status='active' AND o.status='active')) LIMIT 1");$q->execute(['uid'=>(int)$u['id'],'uid2'=>(int)$u['id']]);if(!$q->fetchColumn())gis_auth_json(403,['ok'=>false,'error'=>'active_subscription_required']);}
 $u['id']=(int)$u['id'];$u['is_platform_admin']=(bool)$u['is_platform_admin'];return $u;
}
function gis_auth_require_org_subscription(PDO $pdo,array $user,int $org):void{
 $q=$pdo->prepare("SELECT 1 FROM public.gis_subscriptions s WHERE s.organization_id=:org AND s.status='active' AND (s.starts_at IS NULL OR s.starts_at<=now()) AND (s.expires_at IS NULL OR s.expires_at>now()) LIMIT 1");
 $q->execute(['org'=>$org]);
 if(!$q->fetchColumn())gis_auth_json(403,['ok'=>false,'error'=>'active_subscription_required_for_organization']);
}
function gis_auth_require_org(PDO $pdo,array $user,int $org,string $minRole='viewer'):array{
 $rank=['viewer'=>1,'member'=>1,'editor'=>2,'admin'=>3,'owner'=>4];$q=$pdo->prepare("SELECT m.role,m.status,o.status AS organization_status FROM public.gis_organization_members m JOIN public.gis_organizations o ON o.id=m.organization_id WHERE m.organization_id=:org AND m.user_id=:uid LIMIT 1");$q->execute(['org'=>$org,'uid'=>(int)$user['id']]);$m=$q->fetch();
 if(!$m||$m['status']!=='active'||$m['organization_status']!=='active'||!isset($rank[$m['role']])||$rank[$m['role']]<($rank[$minRole]??99))gis_auth_json(403,['ok'=>false,'error'=>'organization_access_denied']);return $m;
}
