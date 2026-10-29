// La prova automatica dell'interfaccia vera: avvia l'app (con i dati in una cartella temporanea, così non tocca le tue
// impostazioni), la guida come farebbe una persona e controlla che non ci siano errori in console.
// Uso: node scripts/prova-interfaccia.mjs [percorso\DaPConvertitore.exe]
// L'app non deve essere già aperta (è a istanza unica: la richiesta finirebbe nella finestra che c'è).
import { spawn, execSync } from 'node:child_process';
import { mkdtempSync, rmSync, existsSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const radice = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const exe = process.argv[2] ?? join(radice, 'src', 'DaPConvertitore', 'bin', 'Debug', 'net10.0-windows10.0.22621.0', 'win-x64', 'DaPConvertitore.exe');
const porta = 9334;
if (!existsSync(exe)) { console.error('non trovo l\'app:', exe, '\nCompila con scripts\\compila.ps1'); process.exit(2); }
if (execSync('tasklist /FI "IMAGENAME eq DaPConvertitore.exe" /NH', { encoding: 'utf8' }).includes('DaPConvertitore.exe')) {
  console.error('DaP Convertitore è già aperto: chiudilo e rilancia la prova.'); process.exit(2);
}

const dati = mkdtempSync(join(tmpdir(), 'dap-prova-'));
const app = spawn(exe, [], {
  cwd: dirname(exe), stdio: 'ignore', detached: false,
  env: { ...process.env, DAP_DATI: dati, WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS: `--remote-debugging-port=${porta}` },
});

const attesa = (ms) => new Promise((ok) => setTimeout(ok, ms));
const esiti = [];
const errori = [];
const prova = (nome, ok, dettaglio = '') => { esiti.push({ nome, ok }); console.log(`${ok ? '  ok ' : ' MALE'}  ${nome}${!ok && dettaglio ? `  → ${dettaglio}` : ''}`); };

function chiudi(codice) {
  try { execSync(`taskkill /PID ${app.pid} /T /F`, { stdio: 'ignore' }); } catch { /* già chiusa */ }
  setTimeout(() => { try { rmSync(dati, { recursive: true, force: true }); } catch { /* in uso: pazienza */ } process.exit(codice); }, 800);
}

try {
  // aspetta che la pagina ci sia
  let pagina;
  for (let i = 0; i < 60 && !pagina; i++) {
    try { pagina = (await (await fetch(`http://127.0.0.1:${porta}/json`)).json()).find((p) => p.type === 'page' && p.url.includes('dap.locale')); } catch { /* ancora in avvio */ }
    if (!pagina) await attesa(500);
  }
  if (!pagina) { prova('la pagina dell\'app si apre', false, 'dopo 30 secondi non c\'è'); chiudi(1); }
  else {
    const ws = new WebSocket(pagina.webSocketDebuggerUrl);
    await new Promise((ok) => ws.addEventListener('open', ok));
    let id = 0; const attese = new Map();
    ws.addEventListener('message', (e) => {
      const m = JSON.parse(e.data);
      if (m.id && attese.has(m.id)) { attese.get(m.id)(m); attese.delete(m.id); return; }
      if (m.method === 'Runtime.exceptionThrown') errori.push(m.params.exceptionDetails?.exception?.description ?? m.params.exceptionDetails?.text ?? 'eccezione');
      else if (m.method === 'Runtime.consoleAPICalled' && m.params.type === 'error') errori.push(m.params.args.map((a) => a.value ?? a.description).join(' '));
      else if (m.method === 'Log.entryAdded' && m.params.entry.level === 'error') errori.push(`${m.params.entry.text} ${m.params.entry.url ?? ''}`);
    });
    const invia = (method, params = {}) => new Promise((ok) => { const i = ++id; attese.set(i, ok); ws.send(JSON.stringify({ id: i, method, params })); });
    const dentro = async (expr) => (await invia('Runtime.evaluate', { expression: expr, awaitPromise: true, returnByValue: true, replMode: true })).result?.result?.value;
    const finche = async (expr, ms = 6000) => { for (let t = 0; t < ms; t += 200) { if (await dentro(expr)) return true; await attesa(200); } return false; };
    await invia('Runtime.enable'); await invia('Log.enable');
    await attesa(1500);

    // ——— la pagina e la barra in alto
    prova('l\'interfaccia si disegna (testa e home)', await finche(`!!document.querySelector('.testa .marchio') && document.body.innerText.includes('Qualsiasi file')`));
    prova('la barra ha novità, avatar, impostazioni e chiudi', await dentro(`['Novità e aggiornamenti','Impostazioni','Chiudi'].every(t => !!document.querySelector('.finestra button[title="'+t+'"]')) && !!document.querySelector('.finestra .btn-fin.av')`));
    prova('la home mostra le sei strade', (await dentro(`document.querySelectorAll('.strada, .home-strade > *, [class*=strad]').length`)) >= 1 || await dentro(`document.body.innerText.includes('Video') && document.body.innerText.includes('Archivi')`));

    // ——— le impostazioni: la X resta in vista anche scorrendo in fondo
    await dentro(`document.querySelector('.finestra button[title="Impostazioni"]').click()`);
    prova('le impostazioni si aprono', await finche(`!!document.querySelector('.velo.impostazioni .cassetto')`));
    const righe = await dentro(`document.querySelectorAll('.velo.impostazioni .imp-riga').length`);
    prova('le impostazioni hanno le loro righe', righe >= 8, `righe: ${righe}`);
    await dentro(`(()=>{const c=document.querySelector('.velo.impostazioni .cassetto'); c.scrollTop=c.scrollHeight})()`);
    await attesa(300);
    prova('in fondo alle impostazioni la X è ancora visibile', await dentro(`(()=>{const c=document.querySelector('.velo.impostazioni .cassetto').getBoundingClientRect(); const x=document.querySelector('.velo.impostazioni .confronto-testa .btn-fin').getBoundingClientRect(); return x.top>=c.top-1 && x.bottom<=c.bottom && x.width>0})()`));
    prova('la sezione Avatar e «Cambio automatico» ci sono', await dentro(`document.querySelector('.velo.impostazioni').innerText.includes('AVATAR')`));
    await dentro(`document.querySelector('.velo.impostazioni .confronto-testa .btn-fin').click()`);
    prova('la X chiude le impostazioni', await finche(`!document.querySelector('.velo.impostazioni')`));

    // ——— il simbolo dell'avatar nella barra: acceso e spento (cambia solo i dati in cartella temporanea)
    const era = await dentro(`document.querySelector('.finestra .btn-fin.av').classList.contains('su')`);
    await dentro(`document.querySelector('.finestra .btn-fin.av').click()`);
    prova('il simbolo dell\'avatar si accende', await finche(`document.querySelector('.finestra .btn-fin.av').classList.contains('su') === ${!era}`));
    await dentro(`document.querySelector('.finestra .btn-fin.av').click()`);
    prova('e si spegne di nuovo', await finche(`document.querySelector('.finestra .btn-fin.av').classList.contains('su') === ${era}`));

    // ——— novità e aggiornamenti
    await dentro(`document.querySelector('.finestra button[title="Novità e aggiornamenti"]').click()`);
    prova('novità e aggiornamenti si aprono con il testo', await finche(`!!document.querySelector('.velo.novita') && document.querySelector('.velo.novita').innerText.length > 50`));
    await dentro(`document.querySelectorAll('.velo.novita').forEach(v => v.remove())`);

    // ——— errori in console
    await attesa(500);
    prova('nessun errore in console', errori.length === 0, errori.slice(0, 3).join(' | '));
    ws.close();
    const male = esiti.filter((e) => !e.ok).length;
    console.log(`\n${esiti.length - male} su ${esiti.length} controlli ok${male ? `, ${male} da guardare` : ''}.`);
    chiudi(male ? 1 : 0);
  }
} catch (e) {
  console.error('la prova si è fermata:', e?.message ?? e);
  chiudi(1);
}
