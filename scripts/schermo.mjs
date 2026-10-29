// Fotografa la pagina dell'app vera (WebView2 col debug remoto acceso), dopo aver eseguito del JavaScript.
// Uso: node scripts/schermo.mjs uscita.png ["espressione da eseguire prima"] [pagina: index|lettore]
// DAP_DIM=1120x900 fotografa con la pagina di quella misura (per le foto del README, dove serve più altezza).
import { writeFileSync } from 'node:fs';

const [uscita, prima = '', quale = 'lettore'] = process.argv.slice(2);
const porta = process.env.DAP_PORTA ?? '9333';
const pagine = await (await fetch(`http://127.0.0.1:${porta}/json`)).json();
const pagina = pagine.find((p) => p.type === 'page' && p.url.includes(`dap.locale/${quale}`)) ?? pagine.find((p) => p.type === 'page' && p.url.includes('dap.locale'));
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
if (prima) {
  const r = await manda('Runtime.evaluate', { expression: prima, awaitPromise: true, returnByValue: true, replMode: true });
  if (r?.result?.value !== undefined) console.log(JSON.stringify(r.result.value));
  if (r?.exceptionDetails) console.log('errore:', r.exceptionDetails.exception?.description ?? r.exceptionDetails.text);
}
const dim = (process.env.DAP_DIM ?? '').match(/^(\d+)x(\d+)$/);
if (dim) { await manda('Emulation.setDeviceMetricsOverride', { width: +dim[1], height: +dim[2], deviceScaleFactor: 1, mobile: false }); await new Promise((ok) => setTimeout(ok, 700)); }
const foto = await manda('Page.captureScreenshot', { format: 'png' });
if (dim) await manda('Emulation.clearDeviceMetricsOverride');
writeFileSync(uscita, Buffer.from(foto.data, 'base64'));
console.log(uscita);
ws.close();
