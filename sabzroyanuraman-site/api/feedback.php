<?php
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
header('Cache-Control: no-store, no-cache, must-revalidate, max-age=0');
header('X-Content-Type-Options: nosniff');
header('Referrer-Policy: same-origin');

function respond(int $status, array $payload): never {
    http_response_code($status);
    echo json_encode($payload, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES);
    exit;
}
function char_length(string $value): int {
    if (function_exists('mb_strlen')) return mb_strlen($value, 'UTF-8');
    preg_match_all('/./us', $value, $matches);
    return count($matches[0] ?? []);
}
function read_feedback(string $path): array {
    if (!is_file($path)) return [];
    $raw = file_get_contents($path);
    if ($raw === false || $raw === '') return [];
    $items = json_decode($raw, true);
    return is_array($items) ? $items : [];
}

$method = $_SERVER['REQUEST_METHOD'] ?? 'GET';
if (!in_array($method, ['GET', 'POST'], true)) {
    header('Allow: GET, POST');
    respond(405, ['ok' => false, 'error' => 'Method not allowed.']);
}
$store = __DIR__ . '/feedback-data.json';

if ($method === 'GET') {
    $handle = @fopen($store, 'c+');
    if (!$handle || !flock($handle, LOCK_SH)) respond(500, ['ok' => false, 'error' => 'Feedback storage is unavailable.']);
    rewind($handle);
    $raw = stream_get_contents($handle);
    flock($handle, LOCK_UN);
    fclose($handle);
    $items = is_string($raw) && $raw !== '' ? json_decode($raw, true) : [];
    if (!is_array($items)) $items = [];
    usort($items, static fn($a, $b) => strcmp((string)($b['created_at'] ?? ''), (string)($a['created_at'] ?? '')));
    respond(200, ['ok' => true, 'feedback' => array_values($items)]);
}

$contentType = strtolower((string)($_SERVER['CONTENT_TYPE'] ?? ''));
if (strpos($contentType, 'application/json') !== 0) respond(415, ['ok' => false, 'error' => 'JSON request required.']);
$rawInput = file_get_contents('php://input');
$payload = json_decode(is_string($rawInput) ? $rawInput : '', true);
if (!is_array($payload) || !isset($payload['text']) || !is_string($payload['text'])) {
    respond(400, ['ok' => false, 'error' => 'Please enter feedback text.']);
}
$text = str_replace(["\r\n", "\r"], "\n", trim($payload['text']));
if ($text === '') respond(400, ['ok' => false, 'error' => 'Please enter feedback text.']);
if (char_length($text) > 600) respond(413, ['ok' => false, 'error' => 'Feedback must be 600 characters or fewer.']);
if (substr_count($text, "\n") + 1 > 6) respond(400, ['ok' => false, 'error' => 'Feedback must be no more than six lines.']);
if (preg_match('/[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]/u', $text)) respond(400, ['ok' => false, 'error' => 'Please remove control characters.']);

session_name('webgis_feedback');
session_set_cookie_params(['httponly' => true, 'secure' => !empty($_SERVER['HTTPS']) && $_SERVER['HTTPS'] !== 'off', 'samesite' => 'Lax', 'path' => '/']);
session_start();
$now = time();
if (isset($_SESSION['feedback_last_submit']) && $now - (int)$_SESSION['feedback_last_submit'] < 15) {
    respond(429, ['ok' => false, 'error' => 'لطفاً ۱۵ ثانیه پیش از ارسال بعدی صبر کنید.']);
}

$handle = @fopen($store, 'c+');
if (!$handle) respond(500, ['ok' => false, 'error' => 'Feedback storage is unavailable.']);
if (!flock($handle, LOCK_EX)) { fclose($handle); respond(500, ['ok' => false, 'error' => 'Feedback storage is busy.']); }
rewind($handle);
$current = stream_get_contents($handle);
$items = is_string($current) && $current !== '' ? json_decode($current, true) : [];
if (!is_array($items)) $items = [];
$nextNumber = 1;
foreach ($items as $existing) {
    $existingNumber = (int)($existing['number'] ?? 0);
    if ($existingNumber >= $nextNumber) $nextNumber = $existingNumber + 1;
}
$nextNumber = max($nextNumber, count($items) + 1);
$item = [
    'id' => bin2hex(random_bytes(8)),
    'number' => $nextNumber,
    'text' => $text,
    'created_at' => gmdate('c')
];
$items[] = $item;
rewind($handle);
if (!ftruncate($handle, 0) || fwrite($handle, json_encode($items, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES)) === false || !fflush($handle)) {
    flock($handle, LOCK_UN);
    fclose($handle);
    respond(500, ['ok' => false, 'error' => 'Could not save feedback.']);
}
flock($handle, LOCK_UN);
fclose($handle);
$_SESSION['feedback_last_submit'] = $now;
session_write_close();
respond(201, ['ok' => true, 'feedback' => $item]);
