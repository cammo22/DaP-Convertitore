// Dal modello 3D alle immagini dell'avatar: apre scripts/avatar/render.html in Edge senza finestra, disegna ogni fotogramma di
// ogni animazione su fondo trasparente e lo salva in avatar/frames/<oggetto>/<animazione>/NNN.png.
// Poi scripts/avatar/impacchetta.py li riduce e li mette in un'unica immagine (risorse/avatar/<oggetto>.png + .json).
// Se il modello esce coricato: ROT=90,0,0 (gradi intorno a x,y,z) lo raddrizza.
// Uso: node scripts/avatar/rendi.mjs <oggetto> [lato=384] [animazione:fotogrammi ...]
//   es. node scripts/avatar/rendi.mjs cassetta 384 idle:48 saluta:30 attento:24 gira:36 esci:24 entra:24
import { createServer } from 'node:http';
import { spawn } from 'node:child_process';
import { createReadStream, existsSync, mkdirSync, mkdtempSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, extname, join, normalize, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const radice = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const [oggetto, latoArg = '384', ...specs] = process.argv.slice(2);
if (!oggetto) { console.error('Uso: node scripts/avatar/rendi.mjs <oggetto> [lato] [animazione:fotogrammi ...]'); process.exit(1); }
const lato = +latoArg;
const animazioni = (specs.length ? specs : ['idle:48', 'saluta:30', 'attento:24', 'gira:36', 'esci:24', 'entra:24']).map((x) => { const [n, f] = x.split(':'); return { nome: n, frame: +f }; });

// il piccolo server: la pagina, three.js e i modelli
const cartelle = [['/three/', join(radice, 'ui', 'node_modules', 'three')], ['/modelli/', join(radice, 'avatar', 'sorgenti')], ['/', join(radice, 'scripts', 'avatar')]];
const tipi = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.mjs': 'text/javascript', '.glb': 'model/gltf-binary', '.json': 'application/json' };
const server = createServer((req, res) => {
  const url = decodeURIComponent((req.url ?? '/').split('?')[0]);
  for (const [prefisso, cartella] of cartelle) {
    if (!url.startsWith(prefisso)) continue;
    const file = normalize(join(cartella, url.slice(prefisso.length)));
    if (!file.startsWith(cartella) || !existsSync(file) || !statSync(file).isFile()) continue;
    res.writeHead(200, { 'Content-Type': tipi[extname(file)] ?? 'application/octet-stream', 'Content-Length': statSync(file).size });
    createReadStream(file).pipe(res);
    return;
  }
  res.writeHead(404).end();
});
await new Promise((ok) => server.listen(0, '127.0.0.1', ok));
const porta = server.address().port;

// Edge senza finestra, con la scheda video
const edge = ['C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', 'C:/Program Files/Microsoft/Edge/Application/msedge.exe'].find(existsSync);
if (!edge) { console.error('Non trovo Edge'); process.exit(1); }
const profilo = mkdtempSync(join(tmpdir(), 'dap-avatar-'));
const debug = 9456;
const proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${debug}`, `--user-data-dir=${profilo}`, '--no-first-run', '--hide-scrollbars',
  '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', `--window-size=${lato},${lato}`, 'about:blank'], { stdio: 'ignore' });
const chiudi = () => { try { proc.kill(); } catch { } server.close(); try { rmSync(profilo, { recursive: true, force: true }); } catch { } };
process.on('exit', chiudi);

let pagine = null;
for (let i = 0; i < 60 && !pagine; i++) {
  try { pagine = await (await fetch(`http://127.0.0.1:${debug}/json`)).json(); } catch { await new Promise((ok) => setTimeout(ok, 500)); }
}
const pagina = pagine?.find((p) => p.type === 'page');
if (!pagina) { console.error('Edge non risponde'); process.exit(1); }
const ws = new WebSocket(pagina.webSocketDebuggerUrl);
await new Promise((ok) => ws.addEventListener('open', ok));
let n = 0;
const manda = (method, params = {}) => new Promise((ok, no) => {
  const id = ++n;
  const f = (e) => { const m = JSON.parse(e.data); if (m.id === id) { ws.removeEventListener('message', f); m.error ? no(new Error(JSON.stringify(m.error))) : ok(m.result); } };
  ws.addEventListener('message', f);
  ws.send(JSON.stringify({ id, method, params }));
});
const valuta = async (expression) => {
  const r = await manda('Runtime.evaluate', { expression, awaitPromise: true, returnByValue: true });
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? r.exceptionDetails.text);
  return r.result.value;
};

await manda('Page.enable');
await manda('Emulation.setDefaultBackgroundColorOverride', { color: { r: 0, g: 0, b: 0, a: 0 } });
await manda('Emulation.setDeviceMetricsOverride', { width: lato, height: lato, deviceScaleFactor: 1, mobile: false });
await manda('Page.navigate', { url: `http://127.0.0.1:${porta}/render.html?lato=${lato}&rot=${process.env.ROT ?? '0,0,0'}` });
for (let i = 0; i < 120; i++) { if (await valuta('window.pronto === true').catch(() => false)) break; await new Promise((ok) => setTimeout(ok, 250)); }
console.log(`Carico ${oggetto}…`);
await valuta(`window.carica(${JSON.stringify(oggetto)})`);

for (const a of animazioni) {
  const cartella = join(radice, 'avatar', 'frames', oggetto, a.nome);
  rmSync(cartella, { recursive: true, force: true });
  mkdirSync(cartella, { recursive: true });
  for (let i = 0; i < a.frame; i++) {
    await valuta(`window.disegna(${JSON.stringify(a.nome)}, ${i}, ${a.frame})`);
    const foto = await manda('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
    writeFileSync(join(cartella, `${String(i).padStart(3, '0')}.png`), Buffer.from(foto.data, 'base64'));
  }
  console.log(`  ${a.nome}: ${a.frame} fotogrammi`);
}
ws.close();
chiudi();
process.exit(0);
