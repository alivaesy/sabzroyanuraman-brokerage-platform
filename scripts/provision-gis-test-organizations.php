<?php
declare(strict_types=1);

/**
 * Idempotently provisions the two permanent GIS test organizations and two
 * independent owner accounts. CLI only; keep outside public_html.
 *
 * Config: /home/sabzroyanuraman/private/gis-admin-db.php
 * Existing users/passwords and organization IDs are preserved. No schema
 * migration, destructive SQL, or password reset is performed.
 */
if (PHP_SAPI !== 'cli' || !defined('STDIN') || !defined('STDOUT')) {
    fwrite(STDERR, "CLI only.\n");
    exit(2);
}
function ask(string $prompt): string {
    fwrite(STDOUT, $prompt);
    $v = fgets(STDIN);
    return $v === false ? '' : trim($v);
}
function secret(string $prompt): string {
    $tty = @fopen('/dev/tty', 'r+');
    if (!$tty) { fwrite(STDERR, "Interactive terminal required; refusing visible password input.\n"); exit(2); }
    $status = 1;
    @exec('stty -echo < /dev/tty 2>/dev/null', $unused, $status);
    if ($status !== 0) { fclose($tty); fwrite(STDERR, "Could not hide password input.\n"); exit(2); }
    fwrite(STDOUT, $prompt);
    try { $v = fgets($tty); if ($v === false) throw new RuntimeException('Password input failed'); return rtrim($v, "\r\n"); }
    finally { @exec('stty echo < /dev/tty 2>/dev/null'); fwrite(STDOUT, "\n"); fclose($tty); }
}
$cfgPath = '/home/sabzroyanuraman/private/gis-admin-db.php';
if (!is_file($cfgPath) || !is_readable($cfgPath)) { fwrite(STDERR, "Missing private DB config: {$cfgPath}\n"); exit(2); }
$cfg = require $cfgPath;
foreach (['host','port','database','username','password'] as $k) {
    if (!is_array($cfg) || !isset($cfg[$k]) || !is_scalar($cfg[$k]) || (string)$cfg[$k] === '') { fwrite(STDERR, "Invalid private DB config.\n"); exit(2); }
}
$orgs = [
    ['name' => 'kadaster_Sabtasnad', 'slug' => 'kadaster-sabtasnad', 'username' => 'kadaster_sabtasnad', 'display' => 'Kadaster Sabtasnad GIS'],
    ['name' => 'test', 'slug' => 'test', 'username' => 'test_gis', 'display' => 'GIS Test Account'],
];
try {
    $pdo = new PDO(
        sprintf('pgsql:host=%s;port=%d;dbname=%s;connect_timeout=4', (string)$cfg['host'], (int)$cfg['port'], (string)$cfg['database']),
        (string)$cfg['username'], (string)$cfg['password'],
        [PDO::ATTR_ERRMODE=>PDO::ERRMODE_EXCEPTION, PDO::ATTR_DEFAULT_FETCH_MODE=>PDO::FETCH_ASSOC, PDO::ATTR_EMULATE_PREPARES=>false, PDO::ATTR_TIMEOUT=>5]
    );
    $pdo->beginTransaction();
    $pdo->query('SELECT pg_advisory_xact_lock(731904282)');
    foreach ($orgs as $o) {
        $q = $pdo->prepare('SELECT id,name,status FROM public.gis_organizations WHERE slug=:s FOR UPDATE');
        $q->execute(['s'=>$o['slug']]); $existingOrg = $q->fetch();
        if ($existingOrg) {
            if ($existingOrg['name'] !== $o['name']) throw new RuntimeException("Slug {$o['slug']} already exists with a different name; no changes made.");
            if ($existingOrg['status'] !== 'active') throw new RuntimeException("Organization {$o['name']} exists but is not active; manual review required.");
            $orgId = (int)$existingOrg['id'];
        } else {
            $q = $pdo->prepare("INSERT INTO public.gis_organizations(name,slug,status) VALUES(:n,:s,'active') RETURNING id");
            $q->execute(['n'=>$o['name'],'s'=>$o['slug']]); $orgId = (int)$q->fetchColumn();
        }
        $q = $pdo->prepare('SELECT id,status FROM public.gis_users WHERE username=:u FOR UPDATE');
        $q->execute(['u'=>$o['username']]); $user = $q->fetch();
        if ($user) {
            if ($user['status'] !== 'active') throw new RuntimeException("Account {$o['username']} exists but is not active; manual review required.");
            $userId = (int)$user['id'];
        } else {
            $password = secret("Set password for {$o['username']} (12+ chars): ");
            if (strlen($password) < 12 || strlen($password) > 1024) throw new RuntimeException('Password must be 12-1024 characters.');
            $confirm = secret("Confirm password for {$o['username']}: ");
            if (!hash_equals($password, $confirm)) throw new RuntimeException('Passwords do not match.');
            $q = $pdo->prepare("INSERT INTO public.gis_users(username,password_hash,display_name,status,is_platform_admin) VALUES(:u,:p,:d,'active',false) RETURNING id");
            $q->execute(['u'=>$o['username'],'p'=>password_hash($password,PASSWORD_DEFAULT),'d'=>$o['display']]);
            $userId = (int)$q->fetchColumn();
        }
        $q = $pdo->prepare("INSERT INTO public.gis_organization_members(organization_id,user_id,role,status) VALUES(:o,:u,'owner','active') ON CONFLICT (organization_id,user_id) DO UPDATE SET role='owner', status='active'");
        $q->execute(['o'=>$orgId,'u'=>$userId]);
        $q = $pdo->prepare("SELECT id FROM public.gis_subscriptions WHERE organization_id=:o AND status='active' AND (starts_at IS NULL OR starts_at<=now()) AND (expires_at IS NULL OR expires_at>now()) LIMIT 1");
        $q->execute(['o'=>$orgId]);
        if (!$q->fetchColumn()) {
            $q = $pdo->prepare("INSERT INTO public.gis_subscriptions(organization_id,plan_code,requested_days,status,starts_at,expires_at,max_users,activation_note) VALUES(:o,'gis-test',3650,'active',now(),now()+interval '10 years',10,'Permanent test organization provisioning; ten-year active subscription')");
            $q->execute(['o'=>$orgId]);
        }
        $q = $pdo->prepare("INSERT INTO public.gis_admin_audit_log(actor_user_id,organization_id,action,target_type,target_id,details) VALUES(NULL,:o,'provision_test_org','organization',:id,CAST(:d AS jsonb))");
        $q->execute(['o'=>$orgId,'id'=>(string)$orgId,'d'=>json_encode(['slug'=>$o['slug'],'username'=>$o['username']],JSON_UNESCAPED_SLASHES)]);
        fwrite(STDOUT, "Ready: organization={$o['name']} (id={$orgId}); account={$o['username']} (id={$userId})\n");
    }
    $pdo->commit();
    fwrite(STDOUT, "SUCCESS: both organizations/accounts are provisioned. Existing passwords and IDs were preserved.\n");
} catch (Throwable $e) {
    if (isset($pdo) && $pdo instanceof PDO && $pdo->inTransaction()) $pdo->rollBack();
    error_log('[gis-test-org-provision] failed: '.get_class($e).': '.$e->getMessage());
    fwrite(STDERR, "FAILED; transaction rolled back. Check schema and least-privilege DB grants. No passwords were printed.\n");
    exit(1);
}
