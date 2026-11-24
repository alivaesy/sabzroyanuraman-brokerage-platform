(function(root){
  "use strict";
  function create({stopActiveDraw,setActiveMapTool,document,$,status}){
    if(typeof stopActiveDraw!=="function"||typeof setActiveMapTool!=="function"||!document||typeof $!=="function"||typeof status!=="function")throw new Error("Map interaction tool dependencies are required.");
    function activate(name,buttonId,message){
      stopActiveDraw(true);
      setActiveMapTool(name);
      document.querySelectorAll("#classicToolbars button.icon-tool").forEach(button=>button.classList.remove("active"));
      const button=$(buttonId);
      if(button)button.classList.add("active");
      status(message);
    }
    return {
      startHyperlinkTool:()=>activate("hyperlink","hyperlinkBtn","Click a feature with a URL, link, website, or href attribute."),
      startHtmlPopupTool:()=>activate("htmlPopup","htmlPopupBtn","Click a feature to open its attribute pop-up.")
    };
  }
  root.SabzWebGISMapInteractionTools={create};
})(window);
