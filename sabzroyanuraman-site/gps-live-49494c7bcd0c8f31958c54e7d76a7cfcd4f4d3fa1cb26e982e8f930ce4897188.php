<?php
declare(strict_types=1);
header('Cache-Control: no-store, private');
header('X-Robots-Tag: noindex, nofollow, noarchive');
header('Referrer-Policy: no-referrer');
?>
<!doctype html>
<html lang="fa" dir="rtl">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="robots" content="noindex,nofollow,noarchive">
<meta name="referrer" content="no-referrer">
<title>نمایش زنده ردیابی GPS</title>
<link rel="stylesheet" href="https://unpkg.com/leaflet@1.9.4/dist/leaflet.css">
<script src="https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"></script>
<style>
*{box-sizing:border-box}body{margin:0;background:#f3f7f5;color:#173b32;font-family:Tahoma,Arial,sans-serif}
header{padding:14px 18px;background:#07573e;color:#fff}h1{font-size:20px;margin:0 0 5px}
header p{font-size:13px;margin:0;opacity:.9}
main{display:grid;grid-template-columns:minmax(270px,340px) 1fr;gap:12px;padding:12px;height:calc(100vh - 86px)}
#tracks{background:#fff;border:1px solid #d5e3dd;border-radius:10px;padding:12px;overflow:auto}
#status{font-size:12px;color:#526b62;margin-bottom:10px;line-height:1.7}
.track-row{border:1px solid #dce7e2;border-radius:8px;padding:9px;margin:8px 0;font-size:12px;line-height:1.8}
.track-row.live{border-right:4px solid #169447}.track-row.stale{border-right:4px solid #c58a19}.track-row.stopped{border-right:4px solid #7b8983}
#map{min-height:420px;height:100%;border:1px solid #d5e3dd;border-radius:10px;overflow:hidden}
.leaflet-popup-content{direction:rtl;text-align:right}
@media(max-width:760px){main{height:auto;min-height:calc(100vh - 86px);grid-template-columns:1fr}#map{height:58vh;min-height:360px;grid-row:1}#tracks{max-height:36vh}}
</style>
</head>
<body>
<header><h1>موقعیت زنده ردیاب‌ها</h1><p>موقعیت‌های ارسال‌شده در ۲۴ ساعت گذشته؛ بروزرسانی خودکار هر ۵ ثانیه</p></header>
<main><section id="tracks"><div id="status">در حال دریافت موقعیت‌ها…</div><div id="trackList"></div></section><div id="map" aria-label="نقشه موقعیت ردیاب‌ها"></div></main>
<script>
document.getElementById("status").textContent = "نمایش زنده تا پیاده‌سازی احراز هویت و مجوزدهی سمت سرور غیرفعال است.";
document.getElementById("trackList").textContent = "داده‌های مکانی برای جلوگیری از دسترسی عمومی نمایش داده نمی‌شوند.";
document.getElementById("map").hidden = true;
</script>
</body>
</html>
