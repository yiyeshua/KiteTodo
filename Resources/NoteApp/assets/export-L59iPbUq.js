import{M as c,H as l}from"./index-Dps2Z6Qf.js";const p=new c({html:!0,linkify:!0,highlight:(e,t)=>{if(t&&l.getLanguage(t))try{return l.highlight(e,{language:t}).value}catch{}return""}});function d(e,t){const n=p.render(e);return`<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>${m(t)}</title>
  <style>
    body {
      max-width: 800px;
      margin: 0 auto;
      padding: 40px 20px;
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
      font-size: 16px;
      line-height: 1.7;
      color: #333;
    }
    h1 { font-size: 2em; border-bottom: 1px solid #eee; padding-bottom: 0.3em; }
    h2 { font-size: 1.5em; border-bottom: 1px solid #eee; padding-bottom: 0.3em; }
    pre { background: #1e1e1e; color: #d4d4d4; padding: 16px; border-radius: 6px; overflow-x: auto; }
    code { background: #f5f5f5; padding: 2px 6px; border-radius: 3px; }
    pre code { background: none; padding: 0; }
    table { border-collapse: collapse; width: 100%; }
    th, td { border: 1px solid #ddd; padding: 8px 12px; text-align: left; }
    blockquote { border-left: 4px solid #2563EB; padding: 0.5em 1em; color: #666; background: #f9f9f9; }
    img { max-width: 100%; }
  </style>
</head>
<body>
${n}
</body>
</html>`}function b(e,t){const n=d(e,t),a=new Blob([n],{type:"text/html"}),o=URL.createObjectURL(a),r=window.open(o,"_blank");r&&(r.onload=()=>{r.print(),setTimeout(()=>URL.revokeObjectURL(o),1e3)})}function h(e,t){const n=d(e,t),a=new Blob([n],{type:"text/html;charset=utf-8"}),o=URL.createObjectURL(a),r=document.createElement("a");r.href=o,r.download=`${i(t)}.html`,r.click(),URL.revokeObjectURL(o)}function u(e,t){const n=new Blob([e],{type:"text/plain;charset=utf-8"}),a=URL.createObjectURL(n),o=document.createElement("a");o.href=a,o.download=i(t),o.click(),URL.revokeObjectURL(a)}function m(e){return e.replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;").replace(/"/g,"&quot;")}function i(e){return e.replace(/[<>:"/\\|?*]/g,"_")}export{h as exportHTML,b as exportPDF,u as exportTextFile,d as markdownToHTML};
