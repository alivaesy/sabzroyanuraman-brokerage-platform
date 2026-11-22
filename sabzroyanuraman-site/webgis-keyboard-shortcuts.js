/* ArcMap-style keyboard shortcuts and safe tool cancellation. */
(function(root){
  "use strict";
  function init(ctx){
    const status=ctx&&ctx.status;
    const click=id=>document.getElementById(id)?.click();
    const isTyping=el=>!!el&&(
      el.matches('input,textarea,select')||
      el.isContentEditable||
      !!el.closest('[contenteditable="true"]')
    );
    document.addEventListener('keydown',function(event){
      const key=String(event.key||'').toLowerCase();
      const typing=isTyping(event.target);
      if(event.key==='Escape'){
        document.querySelectorAll('.arc-dropdown.show,.arc-context-menu.show,#topToolResults.show').forEach(el=>el.classList.remove('show'));
        document.querySelectorAll('.arc-menu-btn.active').forEach(el=>{el.classList.remove('active');el.setAttribute('aria-expanded','false');});
        const dialog=document.querySelector('.data-dialog.show');
        if(dialog){click('dataDialogCancel');event.preventDefault();return;}
        const projectDialog=document.getElementById('projectManager');
        if(projectDialog?.classList.contains('show')){click('closeProjectManager');event.preventDefault();return;}
        if(window.__webGisGdbBridge?.cancelActiveTool?.()){
          if(typeof status==='function')status('Active sketch/tool cancelled.');
          event.preventDefault();
          return;
        }
        if(!typing){document.querySelectorAll('#classicToolbars button.active').forEach(el=>el.classList.remove('active'));event.preventDefault();}
        return;
      }
      if(typing||event.altKey)return;
      if((event.ctrlKey||event.metaKey)&&(key==='y'||(key==='z'&&event.shiftKey))){
        event.preventDefault();click('redoBtn');return;
      }
      if((event.ctrlKey||event.metaKey)&&key==='z'&&!event.shiftKey){
        event.preventDefault();click('undoBtn');return;
      }
      if((event.ctrlKey||event.metaKey)&&key==='s'){
        event.preventDefault();
        const save=document.getElementById('saveEditsBtn');
        if(save&&!save.disabled)save.click();
        else document.querySelector('[data-action="saveProject"]')?.click();
        return;
      }
      // Match ArcMap: Delete removes selected features; Backspace must never delete them.
      if(!event.ctrlKey&&!event.metaKey&&event.key==='Delete'){
        const target=event.target;
        if(target?.isContentEditable||target?.closest?.('input,textarea,select,button,a,[role=button],[contenteditable=true]'))return;
        event.preventDefault();click('deleteBtn');
      }
    },true);
  }
  root.SabzWebGISKeyboardShortcuts=Object.freeze({init});
})(typeof window!=="undefined"?window:globalThis);
