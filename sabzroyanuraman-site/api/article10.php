<?php
declare(strict_types=1);
header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate');
header('X-Content-Type-Options: nosniff');
header('Referrer-Policy: same-origin');

function out(int $code,array $data): never { http_response_code($code); echo json_encode($data,JSON_UNESCAPED_UNICODE|JSON_UNESCAPED_SLASHES); exit; }
function clean($v,int $max=5000): string { $s=trim((string)$v); if(function_exists('mb_substr')) return mb_substr($s,0,$max,'UTF-8'); return substr($s,0,$max); }
function valid_origin(): void {
 $origin=$_SERVER['HTTP_ORIGIN']??''; if($origin==='')return;
 $oh=parse_url($origin,PHP_URL_HOST); $rh=preg_replace('/:\\d+$/','',$_SERVER['HTTP_HOST']??'');
 if(!$oh||strcasecmp((string)$oh,(string)$rh)!==0)out(403,['ok'=>false,'error'=>'مبدأ درخواست مجاز نیست.']);
}
function db(): PDO {
 $dir='/home/sabzroyanuraman/private-data/article10';
 if(!is_dir($dir)&&!mkdir($dir,0700,true)&&!is_dir($dir))out(500,['ok'=>false,'error'=>'فضای خصوصی پرونده‌ها در دسترس نیست.']);
 $pdo=new PDO('sqlite:'.$dir.'/applications.sqlite',null,null,[PDO::ATTR_ERRMODE=>PDO::ERRMODE_EXCEPTION,PDO::ATTR_DEFAULT_FETCH_MODE=>PDO::FETCH_ASSOC]);
 $pdo->exec('PRAGMA journal_mode=WAL');$pdo->exec('PRAGMA busy_timeout=5000');
 $pdo->exec('CREATE TABLE IF NOT EXISTS applications(id TEXT PRIMARY KEY,reference_number TEXT UNIQUE,token_hash TEXT NOT NULL,data_json TEXT NOT NULL,status TEXT NOT NULL,created_at TEXT NOT NULL,updated_at TEXT NOT NULL,submitted_at TEXT)');
 $pdo->exec('CREATE TABLE IF NOT EXISTS files(id INTEGER PRIMARY KEY AUTOINCREMENT,application_id TEXT NOT NULL,category TEXT NOT NULL,original_name TEXT NOT NULL,stored_name TEXT NOT NULL,mime TEXT NOT NULL,size INTEGER NOT NULL,created_at TEXT NOT NULL,FOREIGN KEY(application_id) REFERENCES applications(id) ON DELETE CASCADE)');
 $pdo->exec('CREATE TABLE IF NOT EXISTS rate_limits(k TEXT PRIMARY KEY,started INTEGER NOT NULL,n INTEGER NOT NULL)');
 return $pdo;
}
function authenticate(PDO $db,string $id,string $token): array {
 if(!preg_match('/^[a-f0-9]{32}$/',$id)||strlen($token)<32)out(422,['ok'=>false,'error'=>'شناسه یا کلید دسترسی نامعتبر است.']);
 $q=$db->prepare('SELECT * FROM applications WHERE id=?');$q->execute([$id]);$row=$q->fetch();
 if(!$row||!hash_equals($row['token_hash'],hash('sha256',$token)))out(404,['ok'=>false,'error'=>'پرونده یا کلید دسترسی یافت نشد.']);
 return $row;
}
valid_origin();
if(($_SERVER['REQUEST_METHOD']??'')!=='POST'){header('Allow: POST');out(405,['ok'=>false,'error'=>'فقط POST مجاز است.']);}
$db=db();
$ip=$_SERVER['REMOTE_ADDR']??'unknown';$rk=hash('sha256',$ip.'|'.gmdate('YmdH'));
$q=$db->prepare('SELECT * FROM rate_limits WHERE k=?');$q->execute([$rk]);$rate=$q->fetch();
if($rate&&(int)$rate['started']>time()-3600&&(int)$rate['n']>=40)out(429,['ok'=>false,'error'=>'تعداد درخواست‌ها بیش از حد مجاز است.']);
if(!$rate||(int)$rate['started']<=time()-3600){$q=$db->prepare('INSERT OR REPLACE INTO rate_limits(k,started,n) VALUES(?,?,1)');$q->execute([$rk,time()]);}
else{$q=$db->prepare('UPDATE rate_limits SET n=n+1 WHERE k=?');$q->execute([$rk]);}

if(isset($_POST['action'])&&$_POST['action']==='upload'){
 $id=clean($_POST['application_id']??'',32);$token=clean($_POST['access_token']??'',100);$row=authenticate($db,$id,$token);
 if($row['status']==='submitted')out(409,['ok'=>false,'error'=>'پرونده ثبت نهایی شده و امکان افزودن فایل ندارد.']);
 if(!isset($_FILES['file'])||$_FILES['file']['error']!==UPLOAD_ERR_OK)out(422,['ok'=>false,'error'=>'فایل دریافت نشد.']);
 $f=$_FILES['file'];if($f['size']<1||$f['size']>10*1024*1024)out(413,['ok'=>false,'error'=>'حجم فایل باید حداکثر ۱۰ مگابایت باشد.']);
 $name=clean(basename((string)$f['name']),180);$ext=strtolower(pathinfo($name,PATHINFO_EXTENSION));
 $allowed=['pdf'=>'application/pdf','jpg'=>'image/jpeg','jpeg'=>'image/jpeg','png'=>'image/png'];
 if(!isset($allowed[$ext]))out(415,['ok'=>false,'error'=>'فقط PDF، JPG و PNG مجاز است.']);
 $mime=(new finfo(FILEINFO_MIME_TYPE))->file($f['tmp_name']);if($mime!==$allowed[$ext])out(415,['ok'=>false,'error'=>'نوع واقعی فایل با پسوند آن مطابقت ندارد.']);
 $cat=clean($_POST['category']??'evidence',20);if(!in_array($cat,['evidence','possession','map'],true))out(422,['ok'=>false,'error'=>'دسته فایل نامعتبر است.']);
 $dir='/home/sabzroyanuraman/private-data/article10/'.$id;
 if(!is_dir($dir)&&!mkdir($dir,0700,true)&&!is_dir($dir))out(500,['ok'=>false,'error'=>'پوشه امن فایل ایجاد نشد.']);
 $stored=bin2hex(random_bytes(18)).'.'.$ext;$dest=$dir.'/'.$stored;
 if(!move_uploaded_file($f['tmp_name'],$dest))out(500,['ok'=>false,'error'=>'ذخیره فایل انجام نشد.']);
 $q=$db->prepare('INSERT INTO files(application_id,category,original_name,stored_name,mime,size,created_at) VALUES(?,?,?,?,?,?,?)');
 $q->execute([$id,$cat,$name,$stored,$mime,(int)$f['size'],gmdate('c')]);
 out(201,['ok'=>true,'file'=>['id'=>(int)$db->lastInsertId(),'category'=>$cat,'original_name'=>$name,'size'=>(int)$f['size']]]);
}

$raw=file_get_contents('php://input');$payload=json_decode($raw?:'',true);
if(!is_array($payload))out(400,['ok'=>false,'error'=>'JSON ارسالی نامعتبر است.']);
$action=clean($payload['action']??'',20);$id=clean($payload['application_id']??'',32);$token=clean($payload['access_token']??'',100);$data=$payload['data']??[];
if(!is_array($data))out(422,['ok'=>false,'error'=>'ساختار اطلاعات نامعتبر است.']);
$now=gmdate('c');
if($action==='save'&&$id===''&&$token===''){
 $id=bin2hex(random_bytes(16));$token=bin2hex(random_bytes(32));$json=json_encode($data,JSON_UNESCAPED_UNICODE|JSON_UNESCAPED_SLASHES);
 if(!is_string($json)||strlen($json)>150000)out(413,['ok'=>false,'error'=>'حجم اطلاعات بیش از حد مجاز است.']);
 $ref='SR-A10-'.gmdate('Ymd').'-'.strtoupper(bin2hex(random_bytes(3)));
 $q=$db->prepare('INSERT INTO applications(id,reference_number,token_hash,data_json,status,created_at,updated_at) VALUES(?,?,?,?,?,?,?)');
 $q->execute([$id,$ref,hash('sha256',$token),$json,'draft',$now,$now]);
 out(201,['ok'=>true,'application_id'=>$id,'access_token'=>$token,'reference_number'=>$ref,'status'=>'draft']);
}
$row=authenticate($db,$id,$token);
if($action==='get'){
 $q=$db->prepare('SELECT id,category,original_name,size,created_at FROM files WHERE application_id=? ORDER BY id');$q->execute([$id]);
 out(200,['ok'=>true,'application'=>['reference_number'=>$row['reference_number'],'status'=>$row['status'],'data'=>json_decode($row['data_json'],true)],'files'=>$q->fetchAll()]);
}
if($row['status']==='submitted')out(409,['ok'=>false,'error'=>'این پرونده قبلاً ثبت نهایی شده است.']);
if(!in_array($action,['save','submit'],true))out(400,['ok'=>false,'error'=>'عملیات نامعتبر است.']);
$json=json_encode($data,JSON_UNESCAPED_UNICODE|JSON_UNESCAPED_SLASHES);
if(!is_string($json)||strlen($json)>150000)out(413,['ok'=>false,'error'=>'حجم اطلاعات بیش از حد مجاز است.']);
if($action==='submit'){
 $required=['firstName','lastName','nationalId','mobile','address','claimRight','ownershipSubject','claimShare','province','registrationUnit','propertyType','area','propertyAddress','longitude','latitude'];
 foreach($required as $key){if(trim((string)($data[$key]??''))==='')out(422,['ok'=>false,'error'=>'فیلدهای ضروری پرونده کامل نیستند: '.$key]);}
 if(empty($data['truthConfirm']))out(422,['ok'=>false,'error'=>'تأیید صحت اطلاعات الزامی است.']);
 $q=$db->prepare('SELECT COUNT(*) FROM files WHERE application_id=? AND category='evidence'');$q->execute([$id]);
 if((int)$q->fetchColumn()<1)out(422,['ok'=>false,'error'=>'حداقل یک مستند در بخش مستندات ادعا باید بارگذاری شود.']);
 $q=$db->prepare('UPDATE applications SET data_json=?,status=?,updated_at=?,submitted_at=? WHERE id=?');
 $q->execute([$json,'submitted',$now,$now,$id]);
 out(200,['ok'=>true,'reference_number'=>$row['reference_number'],'status'=>'submitted','status_label'=>'ثبت‌شده در کارگزاری؛ در انتظار بررسی کارشناسی']);
}
$q=$db->prepare('UPDATE applications SET data_json=?,updated_at=? WHERE id=?');$q->execute([$json,$now,$id]);
out(200,['ok'=>true,'application_id'=>$id,'reference_number'=>$row['reference_number'],'status'=>'draft']);
