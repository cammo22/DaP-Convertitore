// Fa un clic VERO (non un .click() da JavaScript) su un elemento dell'app vera: serve dove la pagina vuole un gesto
// dell'utente (appunti, schermo intero…). Avvia l'app con WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9333
// Uso: node scripts/clic.mjs ".selettore css" [indice]
const [selettore, indice = '0'] = process.argv.slice(2);
const porta = process.env.DAP_PORTA ?? '9333';
const pagine = await (await fetch(`http://127.0.0.1:${porta}/json`)).json();
const pagina = pagine.find((p) => p.type === 'page' && p.url.includes('dap.locale'));
if (!pagina) { console.error('pagina non trovata', pagine.map((p) => p.url)); process.exit(1); }
const ws = new WebSocket(pagina.webSocketDebuggerUrl);
await new Promise((ok) => ws.addEventListener('open', ok));
let n = 0;
const manda = (method, params = {}) => new Promise((ok) => {
  const id = ++n;
  const f = (e) => { const m = JSON.parse(e.data); if (m.id === id) { ws.removeEventListener('message', f); ok(m.result); } };
  ws.addEventListener('message', f);
  ws.send(JSON.stringify({ id, method, params }));
});
const r = await manda('Runtime.evaluate', {
  expression: `(() => { const e = document.querySelectorAll(${JSON.stringify(selettore)})[${Number(indice)}]; if (!e) return null; const b = e.getBoundingClientRect(); return { x: b.left + b.width / 2, y: b.top + b.height / 2 }; })()`,
  returnByValue: true,
});
const p = r?.result?.value;
if (!p) { console.error('elemento non trovato:', selettore); process.exit(1); }
await manda('Input.dispatchMouseEvent', { type: 'mouseMoved', x: p.x, y: p.y });
await manda('Input.dispatchMouseEvent', { type: 'mousePressed', x: p.x, y: p.y, button: 'left', clickCount: 1 });
await manda('Input.dispatchMouseEvent', { type: 'mouseReleased', x: p.x, y: p.y, button: 'left', clickCount: 1 });
console.log('cliccato', selettore, Math.round(p.x), Math.round(p.y));
ws.close();
