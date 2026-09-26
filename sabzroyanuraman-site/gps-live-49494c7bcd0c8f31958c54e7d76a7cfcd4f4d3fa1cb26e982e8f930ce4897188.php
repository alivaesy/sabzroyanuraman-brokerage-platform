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
const viewerKey="49494c7bcd0c8f31958c54e7d76a7cfcd4f4d3fa1cb26e982e8f930ce4897188";
const apiUrl="/api/location/track.php";
const map=L.map("map").setView([37.1395,45.9486],12);
L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png",{maxZoom:19,attribution:"© OpenStreetMap contributors"}).addTo(map);
const layers=new Map();
const colors=["#0878d1","#e65100","#7b1fa2","#00897b","#c62828","#5d4037","#3949ab","#558b2f"];
function colorFor(id){let n=0;for(let i=0;i<id.length;i++)n=(n+id.charCodeAt(i))%colors.length;return colors[n];}
function ageText(date){const n=Date.now()-Date.parse(date);if(!Number.isFinite(n))return "نامشخص";if(n<60000)return Math.max(0,Math.floor(n/1000))+" ثانیه پیش";return Math.floor(n/60000)+" دقیقه پیش";}
function formatTime(date){const d=new Date(date);return Number.isNaN(d.getTime())?"—":d.toLocaleTimeString("fa-IR",{hour:"2-digit",minute:"2-digit",second:"2-digit"});}
function removeMissing(visible){for(const [id,item] of layers){if(!visible.has(id)){map.removeLayer(item.group);layers.delete(id);}}}
function renderTracks(tracks){
 const list=document.getElementById("trackList"),visible=new Set();list.replaceChildren();
 let liveCount=0;
 tracks.forEach(function(track){
  const id=String(track.display_id||"").slice(0,10),points=Array.isArray(track.points)?track.points:[];
  if(!id)return;
  visible.add(id);
  const color=colorFor(id);
  let item=layers.get(id);
  if(!item){const group=L.featureGroup().addTo(map);item={group:group,line:null,marker:null,accuracy:null};layers.set(id,item);}
  const coords=points.map(p=>[Number(p.latitude),Number(p.longitude)]).filter(p=>Number.isFinite(p[0])&&Number.isFinite(p[1])&&Math.abs(p[0])<=90&&Math.abs(p[1])<=180);
  if(coords.length){
   if(!item.line)item.line=L.polyline(coords,{color:color,weight:4,opacity:.85}).addTo(item.group);else item.line.setLatLngs(coords);
   const last=points[points.length-1],ll=coords[coords.length-1];
   if(!item.marker)item.marker=L.circleMarker(ll,{radius:8,color:"#fff",weight:2,fillColor:color,fillOpacity:1}).addTo(item.group);else item.marker.setLatLng(ll);
   item.marker.bindPopup("ردیابی "+id+"<br>آخرین دریافت: "+formatTime(last.received_at||last.recorded_at));
   if(last.accuracy!==null&&Number.isFinite(Number(last.accuracy))){
    const radius=Math.max(1,Number(last.accuracy));
    if(!item.accuracy)item.accuracy=L.circle(ll,{radius:radius,color:color,weight:1,fillOpacity:.08}).addTo(item.group);else item.accuracy.setLatLng(ll).setRadius(radius);
   }
  }
  const age=Date.now()-Date.parse(track.last_seen_at);
  const state=track.stopped_at?"متوقف":(age<30000?"زنده":"سیگنال تازه نمی‌رسد");
  const cls=track.stopped_at?"stopped":(age<30000?"live":"stale");
  if(cls==="live")liveCount++;
  const row=document.createElement("div");row.className="track-row "+cls;
  const speed=points.length&&points[points.length-1].speed!==null&&Number.isFinite(Number(points[points.length-1].speed))?(Number(points[points.length-1].speed)*3.6).toFixed(1)+" km/h":"—";
  row.textContent="ردیابی "+id+" · "+state+" · آخرین دریافت: "+ageText(track.last_seen_at)+" · سرعت: "+speed+" · نقاط: "+Number(track.point_count||0);
  row.addEventListener("click",function(){if(coords.length)map.setView(coords[coords.length-1],17);});
  list.appendChild(row);
 });
 removeMissing(visible);
 document.getElementById("status").textContent=tracks.length?("ردیابی‌ها: "+tracks.length+" · زنده: "+liveCount+" · بروزرسانی: "+formatTime(new Date().toISOString())):"در ۲۴ ساعت گذشته ردیابی‌ای ثبت نشده است.";
}
async function refreshTracks(){
 try{
  const response=await fetch(apiUrl,{method:"POST",credentials:"same-origin",cache:"no-store",headers:{"Content-Type":"application/json","Accept":"application/json"},body:JSON.stringify({action:"live",viewer_key:viewerKey})});
  const data=await response.json();
  if(!response.ok||!data.ok)throw new Error(data.error||("HTTP "+response.status));
  renderTracks(Array.isArray(data.tracks)?data.tracks:[]);
 }catch(error){document.getElementById("status").textContent="دریافت موقعیت‌ها ممکن نشد: "+error.message;}
}
refreshTracks();
setInterval(refreshTracks,5000);
</script>
</body>
</html>
