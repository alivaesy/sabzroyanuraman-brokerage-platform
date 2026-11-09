/* ArcMap-style Calculate Field engine extracted behind the legacy toolbar delegate. */
(function(root){
"use strict";
function evaluateAttributeFieldExpression(expression,properties){
 const source=String(expression??"").trim();
 if(!source)throw new Error("Enter a value or field expression.");
 const fieldPattern=/!([^!]+)!/g;
 const hasField=fieldPattern.test(source);fieldPattern.lastIndex=0;
 if(!hasField)return {value:source,expression:false};
 let expr=source.replace(fieldPattern,(_,name)=>{
  const key=Object.keys(properties||{}).find(k=>k.toLowerCase()===name.trim().toLowerCase());
  if(!key)throw new Error("Unknown field: "+name.trim());
  const value=properties[key];
  if(value===undefined||value===null)return "null";
  if(typeof value==="number"&&!Number.isFinite(value))throw new Error("Field "+key+" contains a non-finite number.");
  return JSON.stringify(value);
 });
 const fn=expr.match(/^(UPPER|LOWER|TRIM|ROUND|ABS|CONCAT)\((.*)\)$/i);
 if(fn){
  const args=fn[2].split(/,(?![^[]*\])/).map(x=>x.trim());
  const vals=args.map(x=>{
   if(!/^(?:null|true|false|-?(?:\d+\.?\d*|\.\d+)(?:e[+-]?\d+)?|"(?:[^"\\]|\\.)*"|'(?:[^'\\]|\\.)*')$/i.test(x))throw new Error("Use field references or literal values inside functions.");
   if(x[0]==='"')return JSON.parse(x);
   if(x[0]==="'")return x.slice(1,-1).replace(/\\'/g,"'").replace(/\\\\/g,"\\");
   if(x==="null")return null;if(x==="true")return true;if(x==="false")return false;return Number(x);
  });
  const name=fn[1].toUpperCase();
  if(name==="UPPER"||name==="LOWER"||name==="TRIM"){
   if(vals.length!==1)throw new Error(name+" accepts one argument.");
   return {value:vals[0]===null?"":name==="UPPER"?String(vals[0]).toUpperCase():name==="LOWER"?String(vals[0]).toLowerCase():String(vals[0]).trim(),expression:true};
  }
  if(name==="ABS"||name==="ROUND"){
   if(vals.length<1||vals.length>(name==="ROUND"?2:1)||vals.some(v=>typeof v!=="number"||!Number.isFinite(v)))throw new Error(name+" requires numeric arguments.");
   return {value:name==="ABS"?Math.abs(vals[0]):Number(vals[0].toFixed(vals[1]===undefined?0:Math.max(0,Math.min(12,vals[1])))),expression:true};
  }
  return {value:vals.map(v=>v===null?"":String(v)).join(""),expression:true};
 }
 // Arithmetic expressions only: no property access, statements, or arbitrary JS.
 if(!/^[\d\s.+\-*/()%<>=!&|?:,"'A-Za-z_]+$/.test(expr))throw new Error("Expression contains unsupported characters.");
 const bareExpr=expr.replace(/"(?:[^"\\]|\\.)*"|'(?:[^'\\]|\\.)*'/g,'""');
 if(/[A-Za-z_$][\w$]*\s*\./.test(bareExpr)||/\b(?:new|this|window|document|globalThis|function|return|import|constructor|prototype|eval)\b/i.test(bareExpr)||/\b[A-Za-z_$][\w$]*\s*\(/.test(bareExpr)||/\b(?!true\b|false\b|null\b)[A-Za-z_$][\w$]*\b/i.test(bareExpr))throw new Error("Only field references, literals, arithmetic and supported functions are allowed.");
 let value;
 try{value=Function('"use strict";return ('+expr+');')()}catch(_){throw new Error("Invalid expression. Example: !POPULATION! / 100 or UPPER(!NAME!).");}
 if(value===undefined||typeof value==="function"||typeof value==="object"&&value!==null)throw new Error("Expression must return a single scalar value.");
 if(typeof value==="number"&&!Number.isFinite(value))throw new Error("Expression returned an invalid number.");
 return {value,expression:true};
}
function create(ctx){
 if(!ctx||typeof ctx.$!=="function"||typeof ctx.status!=="function"||typeof ctx.currentAttributeLayer!=="function")throw new Error("Calculate Field context is incomplete.");
 async function calculateField(){
  if(ctx.hasUnsavedTableEdits()){ctx.status("Save Table Edits before calculating fields; unsaved cell values have been preserved.");return false;}
  const layer=ctx.currentAttributeLayer();
  if(!ctx.editSessionActive||!layer||layer!==ctx.editTarget||!ctx.isLayerInEditTarget(layer)){ctx.status("Start Editing this layer before calculating fields.");return false;}
  const items=ctx.currentAttributeItems(),selectedIndexes=[...new Set(ctx.selectedTableIndexes())];
  if(!items.length){ctx.status("The active layer view contains no records to calculate.");return false;}
  // ArcMap semantics: a table selection scopes Calculate Field; with no selected
  // rows, the operation targets every record in the current definition-query view.
  const calculateAll=selectedIndexes.length===0;
  const inds=calculateAll?items.map((_,index)=>index):selectedIndexes;
  const keys=ctx.tableKeys(items),requested=await ctx.arcMapDialog.select({title:"Calculate Field",message:"Choose the field to calculate ("+(calculateAll?"all records in the current layer view":"selected records")+ "):",label:"Field",value:keys[0]||"",options:keys.map(field=>({value:field,label:field}))});
  if(requested===null)return false;
  const key=keys.find(k=>k.toLowerCase()===requested.trim().toLowerCase());
  if(!key){ctx.status("Choose an existing field from the list.");return false;}
  if(ctx.isComputedAttributeField(key)){ctx.status("Calculated fields Area_m2, Length_m, X and Y are read-only.");return false;}
  const raw=await ctx.arcMapDialog.prompt({title:"Calculate Field",message:"Enter a value or expression for "+key+". Examples: 12, text, !FIELD! + 10, UPPER(!NAME!), ROUND(!AREA! / 100, 2), CONCAT(!FIRST!, !LAST!)",label:"Value or expression",value:ctx.$("fillValue").value||""});
  if(raw===null)return false;
  const targets=inds.map(i=>Number.isInteger(i)&&i>=0?items[i]:null);
  if(targets.some(x=>!x||!x.layer||!ctx.isLayerInEditTarget(x.layer))){ctx.status("Calculation cancelled: selected rows are stale or outside the active edit layer.");return false;}
  const schemaField=(layer._gdbMetadata?.fields||[]).find(f=>String(typeof f==="string"?f:f.name).toLowerCase()===key.toLowerCase());
  const schemaType=String(typeof schemaField==="string"?"TEXT":schemaField?.type||"").toUpperCase();
  const samples=items.map(x=>(x.feature.properties||{})[key]).filter(v=>v!==null&&v!==undefined&&v!=="");
  const types=[...new Set(samples.map(v=>Array.isArray(v)?"array":typeof v))];
  if(types.length>1){ctx.status("Calculation cancelled: field contains mixed data types.");return false;}
  const type=schemaType==="INTEGER"||schemaType==="REAL"?"number":schemaType==="DATE"?"date":schemaType==="TEXT"?"string":types[0]||"string",staged=[];
  try{
   targets.forEach(item=>{
    const props=item.feature.properties||{},evaluated=evaluateAttributeFieldExpression(raw,props);
    let value=evaluated.value;
    if(!evaluated.expression){
     if(type==="number"){
      if(raw.trim()==="")value=null;
      else if(!Number.isFinite(Number(raw)))throw new Error("Enter a valid number or expression for "+key+".");
      else value=Number(raw);
     }else if(type==="boolean"){
      if(!/^(true|false)$/i.test(raw.trim()))throw new Error("Enter true or false, or a boolean field expression.");
      value=raw.trim().toLowerCase()==="true";
     }else if(type==="object"||type==="array"){
      value=JSON.parse(raw);
      if(value===null||typeof value!=="object"||(type==="array"&&!Array.isArray(value))||(type==="object"&&Array.isArray(value)))throw new Error("Enter valid "+type+" JSON.");
     }else value=raw.trim()===""?null:raw;
    }
    if(value===null||value===undefined){value=null}
    else if(type==="number"){
     if(typeof value!=="number"||!Number.isFinite(value))throw new Error("Expression for "+key+" must return a number.");
     if(schemaType==="INTEGER"&&!Number.isInteger(value))throw new Error("Field "+key+" requires whole numbers.");
    }else if(type==="boolean"&&typeof value!=="boolean")throw new Error("Expression for "+key+" must return true or false.");
    else if(type==="date"&&typeof value==="string"&&value!==""&&Number.isNaN(Date.parse(value)))throw new Error("Expression for "+key+" must return a valid date.");
    else if(type==="string"&&typeof value!=="string")value=String(value);
    else if((type==="object"||type==="array")&&((typeof value!=="object")||(type==="array"&&!Array.isArray(value))||(type==="object"&&Array.isArray(value))))throw new Error("Expression result does not match the field type.");
    staged.push({item,value});
   });
  }catch(err){ctx.status("Field calculation cancelled; no records changed. "+err.message);return false;}
  const changed=staged.filter(({item,value})=>JSON.stringify((item.feature.properties||{})[key])!==JSON.stringify(value));
  if(!changed.length){ctx.status("The selected features already have these values.");return false;}
  if(!await ctx.arcMapDialog.confirm({title:"Calculate Field",message:"Calculate field '"+key+"' for "+changed.length+(calculateAll?" record(s) in the current layer view?":" selected feature(s)?"),confirmText:"Calculate"}))return false;
  ctx.recordUndoState();
  changed.forEach(({item,value})=>{ctx.ensureFeatureProperties(item.layer)[key]=value;ctx.updateFeaturePopup(item.layer)});
  ctx.renderEditableTable();
  const synced=await ctx.flushGeodatabaseEdits(),projectSaved=await ctx.scheduleAutoSave();
  ctx.status("Calculated '"+key+"' for "+changed.length+" feature(s)."+(synced&&projectSaved?"":" Local save/sync failed; retry Save Edits before leaving."));
  return true;
 }
 return Object.freeze({calculateField,evaluateAttributeFieldExpression});
}
root.SabzAttributeFieldCalculator=Object.freeze({create,evaluateAttributeFieldExpression});
})(window);
