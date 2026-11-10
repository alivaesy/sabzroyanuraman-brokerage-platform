'use strict';
const fs=require('node:fs');
const path=require('node:path');
const test=require('node:test');
const assert=require('node:assert/strict');

const html=fs.readFileSync(path.join(__dirname,'..','sabzroyanuraman-site','web-gis.html'),'utf8');
const start=html.indexOf('$("fillSelected").onclick=async()=>{');
const end=html.indexOf('$("measureSelected").onclick=',start);
assert.ok(start>=0&&end>start,'Calculate Field handler must exist');
const handler=html.slice(start,end);

test('Calculate Field targets all current-view records when no table rows are selected',()=>{
  assert.match(handler,/const items=currentAttributeItems\(\),selectedIndexes=\[\.\.\.new Set\(selectedTableIndexes\(\)\)\]/);
  assert.match(handler,/const calculateAll=selectedIndexes\.length===0/);
  assert.match(handler,/const inds=calculateAll\?items\.map\(\(_,index\)=>index\):selectedIndexes/);
  assert.match(handler,/all records in the current layer view/);
});

test('Calculate Field preserves explicit row selection and asks for confirmation before edits',()=>{
  assert.match(handler,/const calculateAll=selectedIndexes\.length===0/);
  assert.match(handler,/calculateAll\?items\.map\(\(_,index\)=>index\):selectedIndexes/);
  assert.match(handler,/arcMapDialog\.confirm\(\{title:"Calculate Field"/);
  assert.ok(handler.indexOf('arcMapDialog.confirm')<handler.indexOf('recordUndoState()'));
  assert.ok(handler.indexOf('recordUndoState()')<handler.indexOf('changed.forEach'));
});

test('Calculate Field retains edit-target validation and persistence',()=>{
  assert.match(handler,/!editSessionActive\|\|!layer\|\|layer!==editTarget/);
  assert.match(handler,/isLayerInEditTarget\(x\.layer\)/);
  assert.match(handler,/flushGeodatabaseEdits\(\)/);
  assert.match(handler,/scheduleAutoSave\(\)/);
});
