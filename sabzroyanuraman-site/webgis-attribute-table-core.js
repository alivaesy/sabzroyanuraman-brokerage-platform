(function(root){
"use strict";
const COMPUTED_FIELDS=new Set(["area_m2","length_m","x","y"]);
function isReservedField(key){return typeof key!=="string"||key.startsWith("_")||key.toLowerCase()==="geometry"}
function isComputedField(key){return typeof key==="string"&&COMPUTED_FIELDS.has(key.toLowerCase())}
function isProtectedField(key){return isReservedField(key)||isComputedField(key)}
function collectFields(schema,records){
 const declared=(Array.isArray(schema)?schema:[]).map(field=>typeof field==="string"?field:field?.name);
 const discovered=(Array.isArray(records)?records:[]).flatMap(record=>Object.keys(record&&typeof record==="object"?record:{}).filter(key=>!isReservedField(key)));
 return [...new Set([...declared,...discovered].filter(key=>typeof key==="string"&&key.length>0&&!isReservedField(key)))];
}
function compareValues(a,b){
 const aEmpty=a===null||a===undefined||a==="",bEmpty=b===null||b===undefined||b==="";
 if(aEmpty||bEmpty)return aEmpty===bEmpty?0:(aEmpty?1:-1);
 if(typeof a==="number"&&typeof b==="number")return a-b;
 if(typeof a==="boolean"&&typeof b==="boolean")return Number(a)-Number(b);
 const left=String(typeof a==="object"?JSON.stringify(a):a);
 const right=String(typeof b==="object"?JSON.stringify(b):b);
 return left.localeCompare(right,undefined,{numeric:true,sensitivity:"base"});
}
root.SabzWebGISAttributeTableCore=Object.freeze({isReservedField,isComputedField,isProtectedField,collectFields,compareValues});
})(typeof window!=="undefined"?window:globalThis);
