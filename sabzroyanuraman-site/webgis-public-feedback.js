
(function(){
 const endpoint="api/feedback.php";
 const modal=document.getElementById("publicFeedbackModal");
 const openBtn=document.getElementById("publicFeedbackOpen");
 const closeBtn=document.getElementById("publicFeedbackClose");
 const form=document.getElementById("publicFeedbackForm");
 const field=document.getElementById("publicFeedbackText");
 const count=document.getElementById("publicFeedbackCount");
 const status=document.getElementById("publicFeedbackStatus");
 const list=document.getElementById("publicFeedbackList");
 const listToggle=document.getElementById("publicFeedbackToggle");
 const send=document.getElementById("publicFeedbackSend");
 if(!modal||!openBtn||!form||!field||!list)return;
 const setStatus=(message,isError=false)=>{status.textContent=message;status.style.color=isError?"#a33":"#286b4d"};
 const normalized=()=>field.value.replace(/\r\n?/g,"\n");
 const lineCount=value=>value.length?value.split("\n").length:0;
 const updateCount=()=>{const v=normalized();count.textContent=v.length+" / 600 · "+lineCount(v)+" / 6 lines";field.setCustomValidity(lineCount(v)>6?"لطفاً متن را حداکثر در ۶ خط بنویسید.":"");};
 function open(){modal.classList.add("show");document.body.style.overflow="hidden";setTimeout(()=>field.focus(),20)}
 function close(){modal.classList.remove("show");document.body.style.overflow="";}
 function dateLabel(value){const d=new Date(value);if(Number.isNaN(d.getTime()))return "";try{return new Intl.DateTimeFormat("fa-IR-u-ca-persian",{year:"numeric",month:"2-digit",day:"2-digit",hour:"2-digit",minute:"2-digit",hourCycle:"h23"}).format(d)}catch(_){return d.toLocaleString("fa-IR")}}
 function render(items){
   list.replaceChildren();
   if(!items.length){const empty=document.createElement("div");empty.className="public-feedback-item";empty.textContent="هنوز بازخوردی ثبت نشده است.";list.append(empty);return;}
   const chronological=[...items].sort((a,b)=>String(a.created_at||"").localeCompare(String(b.created_at||"")));
   const numbers=new Map();let next=1;chronological.forEach(item=>{const n=Number(item.number);const key=item.id||item;numbers.set(key,Number.isInteger(n)&&n>0?n:next);next=Math.max(next+1,Number.isInteger(n)&&n>0?n+1:next+1)});
   items.forEach(item=>{
     const card=document.createElement("article");card.className="public-feedback-item";
     const meta=document.createElement("div");meta.className="public-feedback-meta";
     const number=document.createElement("span");number.className="public-feedback-number";number.textContent="#"+String(numbers.get(item.id||item)||"");
     const body=document.createElement("div");body.className="public-feedback-text";body.dir="rtl";body.textContent=String(item.text||"");
     const date=document.createElement("time");date.textContent=dateLabel(item.created_at);date.dateTime=String(item.created_at||"");
     meta.append(number,body,date);
     card.append(meta);list.append(card);
   });
 }
 async function load(limit=null){
   list.replaceChildren();const loading=document.createElement("div");loading.className="public-feedback-item";loading.textContent="در حال دریافت بازخوردها…";list.append(loading);
   try{const response=await fetch(endpoint+"?t="+Date.now(),{headers:{Accept:"application/json"},cache:"no-store"});const data=await response.json();if(!response.ok||!data.ok)throw new Error(data.error||"دریافت بازخوردها failed.");const items=Array.isArray(data.feedback)?data.feedback:[];render(Number.isInteger(limit)&&limit>0?items.slice(0,limit):items);}
   catch(error){list.replaceChildren();const err=document.createElement("div");err.className="public-feedback-item";err.textContent="دریافت بازخوردها ممکن نشد. مطمئن شوید نسخه روی میزبانی دارای PHP منتشر شده است.";list.append(err);setStatus(error.message,true);}
 }
 openBtn.addEventListener("click",open);closeBtn.addEventListener("click",close);
 listToggle.addEventListener("click",()=>{
  const expanded=listToggle.getAttribute("aria-expanded")!=="true";
  listToggle.setAttribute("aria-expanded",String(expanded));
  listToggle.textContent=expanded?"▴":"▾";
  listToggle.setAttribute("aria-label",expanded?"پنهان‌کردن بازخوردها":"نمایش بازخوردها");
  listToggle.title=expanded?"پنهان‌کردن بازخوردها":"نمایش بازخوردها";
  list.classList.toggle("expanded",expanded);
  if(expanded)load();
 });
 modal.addEventListener("click",event=>{if(event.target===modal)close()});
 document.addEventListener("keydown",event=>{if(event.key==="Escape"&&modal.classList.contains("show"))close()});
 field.addEventListener("input",updateCount);
 document.getElementById("publicFeedbackRefresh").addEventListener("click",load);
 form.addEventListener("submit",async event=>{
   event.preventDefault();updateCount();const text=normalized().trim();
   if(!text){setStatus("لطفاً متن بازخورد را وارد کنید.",true);field.focus();return;}
   if(lineCount(text)>6){setStatus("متن باید حداکثر ۶ خط باشد.",true);field.focus();return;}
   send.disabled=true;setStatus("در حال ارسال…");
   try{
     const response=await fetch(endpoint,{method:"POST",headers:{"Content-Type":"application/json","Accept":"application/json"},body:JSON.stringify({text})});
     const data=await response.json();if(!response.ok||!data.ok)throw new Error(data.error||"ارسال بازخورد انجام نشد.");
     field.value="";updateCount();setStatus("بازخورد شما ثبت شد؛ بازخورد شما و دو بازخورد اخیر نمایش داده می‌شوند.");list.classList.add("expanded");listToggle.setAttribute("aria-expanded","true");listToggle.textContent="▴";listToggle.setAttribute("aria-label","پنهان‌کردن بازخوردها");listToggle.title="پنهان‌کردن بازخوردها";await load(3);
   }catch(error){setStatus(error.message||"ارسال بازخورد failed.",true);}
   finally{send.disabled=false;}
 });
 updateCount();
})();
