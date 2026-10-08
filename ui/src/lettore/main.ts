// DaP Lettore — guarda qualsiasi file. Una pagina per tipo (il cinema, il giradischi, il tavolo luminoso, la
// scrivania…), tutte con la stessa barra in alto, le frecce per il file dopo e i tasti rapidi (premi «?»).
import '@fontsource/orbitron/700.css';
import '@fontsource/orbitron/900.css';
import '@fontsource/rajdhani/500.css';
import '@fontsource/rajdhani/600.css';
import '@fontsource/rajdhani/700.css';
import './lettore.css';
import { ascolta, chiedi, dentroApp } from '../ponte';
import { h, peso } from '../util';
import { ambiente, coloreTipo, conferma, menuSu, schermoIntero, dataItaliana, ic, nomeTipo, type Contesto, type Scheda, type Tasto, type Tipo, type Vista } from './comune';

const radice = document.getElementById('lettore')!;
const barra = h('header.l-barra');
const palco = h('main.l-palco');
const hudEl = h('div.l-hud');
const statoEl = h('div.l-stato');
const frecciaSx = h('button.l-freccia.sx', { title: 'Il file prima (Pag su)', html: ic.sx, onclick: () => vai(-1) });
const frecciaDx = h('button.l-freccia.dx', { title: 'Il file dopo (Pag giù)', html: ic.dx, onclick: () => vai(1) });
radice.append(barra, palco, frecciaSx, frecciaDx, hudEl, statoEl);

const s = {
  scheda: null as Scheda | null,
  vista: null as Vista | null,
  giro: 0,
  fratelli: [] as string[],
  indice: -1,
  aggiornamento: null as string | null,
  ffmpeg: true,
  office: false,
};

// ————————————————————————— la barra in alto —————————————————————————

const logo = `<svg viewBox="0 0 64 64"><defs>
<linearGradient id="llo" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fffbe0"/><stop offset=".45" stop-color="#ffd54a"/><stop offset="1" stop-color="#b87a14"/></linearGradient>
<linearGradient id="llm" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#ff7bf6"/><stop offset="1" stop-color="#8a2cff"/></linearGradient></defs>
<circle cx="32" cy="32" r="28" fill="#150e24" stroke="url(#llo)" stroke-width="4"/>
<path d="M20 26a13 13 0 0 1 23-6" fill="none" stroke="url(#llm)" stroke-width="4" stroke-linecap="round"/><path d="M45 13v8h-8" fill="none" stroke="url(#llm)" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"/>
<path d="M44 38a13 13 0 0 1-23 6" fill="none" stroke="url(#llo)" stroke-width="4" stroke-linecap="round"/><path d="M19 51v-8h8" fill="none" stroke="url(#llo)" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"/>
</svg>`;

function disegnaBarra() {
  const f = s.scheda;
  if (!f) return;
  const t = f.tipo;
  barra.replaceChildren(...[
    h('div.l-logo', { html: logo, title: 'DaP Convertitore' }),
    h('span.l-chip', { style: { '--tinta': coloreTipo[t] } }, (f.estensione || '·').replace('.', '').toUpperCase() || nomeTipo[t]),
    h('div.l-titolo', null,
      h('b', { title: f.percorso }, f.nome),
      h('span#l-meta', null, metaBarra())),
    h('div.l-trascina'),
    s.fratelli.length > 1 ? h('span.l-pos', { title: 'Pag su / Pag giù per scorrere' }, `${s.indice + 1} / ${s.fratelli.length}`) : null,
    s.aggiornamento ? h('button.l-chip-agg', { title: 'Installa la versione nuova e riapre', onclick: () => void chiedi('aggiorna') }, h('i'), `Nuova ${s.aggiornamento}`) : null,
    h('div.l-azioni', null,
      ...(s.vista?.azioni ?? []).map((a) => h('button.l-icona', { title: a.titolo, html: a.icona, onclick: a.fai })),
      f.rapide.length ? h('button.l-icona.fai-subito', { title: 'Fai subito: le conversioni rapide (U)', html: ic.fulmine, onclick: (e: Event) => faiSubito(e.currentTarget as HTMLElement) }) : null,
      f.convertibile ? h('button.l-converti', { title: 'Converti (Ctrl+E)', onclick: () => void chiedi('converti') }, h('span', { html: ic.converti }), 'Converti') : null,
      h('button.l-icona', { title: 'Apri con… (un altro programma)', html: ic.apriCon, onclick: () => void chiedi('apriCon') }),
      h('button.l-icona', { title: 'Mostra nella cartella', html: ic.cartella, onclick: () => void chiedi('mostra') }),
      h('button.l-icona', { title: 'Proprietà (I)', html: ic.info, onclick: () => void proprieta() }),
      h('button.l-icona', { title: 'Tasti rapidi (?)', html: ic.tasti, onclick: () => foglietto() }),
    ),
    h('div.l-finestra', null,
      h('button.l-icona', { title: 'Riduci', html: ic.riduci, onclick: () => void chiedi('finestra', { azione: 'riduci' }) }),
      h('button.l-icona', { title: 'Ingrandisci', html: ic.ingrandisci, onclick: () => void chiedi('finestra', { azione: 'massimizza' }) }),
      h('button.l-icona.chiudi', { title: 'Chiudi (Ctrl+W)', html: ic.chiudi, onclick: () => void chiedi('finestra', { azione: 'chiudi' }) })),
  ].filter((x): x is HTMLElement => !!x));
}

function metaBarra() {
  const f = s.scheda!;
  const extra = s.vista?.info?.();
  return [extra, peso(f.peso)].filter(Boolean).join(' · ');
}

// la barra sparisce sopra video e foto quando il mouse sta fermo
let quieto: number | undefined;
function sveglia() {
  radice.classList.remove('quieto');
  clearTimeout(quieto);
  if (!s.vista?.barraSopra) return;
  quieto = window.setTimeout(() => {
    if (barra.matches(':hover') || document.querySelector('.l-menu, .l-velo, .l-pannello')) return;
    radice.classList.add('quieto');
  }, 2600);
}
document.addEventListener('pointermove', sveglia);
document.addEventListener('pointerdown', sveglia);

// ————————————————————————— hud e stato —————————————————————————

let hudTimer: number | undefined;
function hud(testo: string, icona?: string) {
  hudEl.replaceChildren(...[icona ? h('span', { html: icona }) : null, h('b', null, testo)].filter((x): x is HTMLElement => !!x));
  hudEl.classList.remove('su');
  void hudEl.offsetWidth;
  hudEl.classList.add('su');
  clearTimeout(hudTimer);
  hudTimer = window.setTimeout(() => hudEl.classList.remove('su'), 900);
}

function stato(testo: string | null) {
  statoEl.textContent = testo ?? '';
  statoEl.classList.toggle('su', !!testo);
}

// ————————————————————————— le viste —————————————————————————

const caricatori: Record<Tipo, () => Promise<{ monta: (c: Contesto) => Promise<Vista> | Vista }>> = {
  video: () => import('./video'),
  audio: () => import('./audio'),
  immagine: () => import('./immagine'),
  pdf: () => import('./pdf'),
  documento: () => import('./documento'),
  presentazione: () => import('./documento'),
  tabella: () => import('./tabella'),
  markdown: () => import('./testo'),
  web: () => import('./testo'),
  json: () => import('./testo'),
  codice: () => import('./testo'),
  testo: () => import('./testo'),
  sottotitoli: () => import('./testo'),
  archivio: () => import('./archivio'),
  font: () => import('./font'),
  modello: () => import('./modello'),
  esadecimale: () => import('./esadecimale'),
};

async function mostra(f: Scheda, verso = 0) {
  const giro = ++s.giro;
  try { s.vista?.smonta(); } catch (e) { console.warn(e); }
  s.vista = null;
  s.scheda = f;
  document.querySelector('.l-pannello')?.remove();
  document.querySelector('.l-menu')?.remove();
  stato(null);
  radice.dataset.tipo = f.tipo;
  radice.style.setProperty('--tinta', coloreTipo[f.tipo]);
  palco.className = 'l-palco';
  palco.replaceChildren();
  palco.dataset.verso = String(Math.sign(verso));
  void palco.offsetWidth;
  palco.classList.add('entra');
  disegnaBarra();
  const ctx: Contesto = {
    scheda: f,
    palco,
    hud,
    stato,
    info: () => { const m = document.getElementById('l-meta'); if (m) m.textContent = metaBarra(); },
    vai,
    fratelli: () => s.fratelli.length,
    ricarica: () => void ricarica(),
    elenco: () => ({ file: s.fratelli, indice: s.indice }),
    vaiA: (i: number) => void vai(i - s.indice),
    viva: () => giro === s.giro,
  };
  try {
    const m = await caricatori[f.tipo]();
    if (giro !== s.giro) return;
    const v = await m.monta(ctx);
    if (giro !== s.giro) { v.smonta(); return; }
    s.vista = v;
  } catch (e) {
    console.error(e);
    if (giro !== s.giro) return;
    palco.replaceChildren(h('div.l-errore', null, h('b', null, 'Non riesco a mostrarlo'), h('p', null, (e as Error).message)));
  }
  radice.classList.toggle('sopra', !!s.vista?.barraSopra);
  const frecce = !!s.vista?.frecceLaterali && s.fratelli.length > 1;
  radice.classList.toggle('con-frecce', frecce);
  radice.classList.toggle('con-fratelli', s.fratelli.length > 1);
  disegnaBarra();
  sveglia();
}

async function vai(passo: number) {
  if (s.fratelli.length < 2) return;
  const n = s.fratelli.length;
  const i = ((s.indice + passo) % n + n) % n;
  try {
    const f = await chiedi<Scheda>('vai', { percorso: s.fratelli[i] });
    s.indice = i;
    void mostra(f, passo);
  } catch (e) {
    // sparito nel frattempo: si toglie dalla fila e si va avanti
    s.fratelli.splice(i, 1);
    if (passo < 0) s.indice = Math.max(0, i - 1);
    hud((e as Error).message);
  }
}

async function caricaFratelli() {
  const r = await chiedi<{ file: string[]; indice: number }>('fratelli');
  s.fratelli = r.file;
  s.indice = r.indice;
  disegnaBarra();
  const frecce = !!s.vista?.frecceLaterali && s.fratelli.length > 1;
  radice.classList.toggle('con-frecce', frecce);
  radice.classList.toggle('con-fratelli', s.fratelli.length > 1);
  document.dispatchEvent(new Event('dap-fratelli'));
}

/** Il file è cambiato (girato, tagliato): si rilegge da capo, con un indirizzo nuovo perché niente resti in memoria. */
async function ricarica() {
  const f = s.scheda;
  if (!f) return;
  try {
    const nuovo = await chiedi<Scheda>('vai', { percorso: f.percorso });
    nuovo.url += `?v=${Date.now()}`;
    await mostra(nuovo);
  } catch (e) { hud((e as Error).message); }
}

// ————————————————————————— proprietà, tasti, cestino —————————————————————————

async function proprieta() {
  const vecchio = document.querySelector('.l-pannello');
  if (vecchio) { vecchio.remove(); return; }
  const f = s.scheda!;
  const p = h('aside.l-pannello', null,
    h('div.l-pannello-testa', null, h('b', null, 'PROPRIETÀ'), h('button.l-icona', { html: ic.chiudi, onclick: () => p.remove() })),
    h('dl', null,
      riga('Nome', f.nome),
      riga('Tipo', f.tipoWindows || nomeTipo[f.tipo]),
      riga('Peso', `${peso(f.peso)} (${f.peso.toLocaleString('it-IT')} byte)`),
      riga('Modificato', dataItaliana(f.modificato)),
      riga('Creato', dataItaliana(f.creato)),
      riga('Cartella', f.cartella)),
    h('div.l-pannello-extra'),
    h('div.l-pannello-tasti', null,
      h('button.l-tasto', { onclick: () => navigator.clipboard.writeText(f.percorso).then(() => hud('Percorso copiato', ic.copia)) }, h('span', { html: ic.copia }), 'Copia il percorso'),
      h('button.l-tasto.rosso', { onclick: () => void cestino() }, h('span', { html: ic.cestino }), 'Nel Cestino')));
  document.body.append(p);
  const extra = s.vista?.scheda;
  if (extra) {
    const nodi = await extra();
    p.querySelector('.l-pannello-extra')?.append(...nodi);
  }
}

function riga(nome: string, valore: string) {
  return h('div', null, h('dt', null, nome), h('dd', { title: valore }, valore));
}

async function cestino() {
  const f = s.scheda!;
  if (!await conferma('Nel Cestino?', `«${f.nome}» va nel Cestino di Windows: se serve, lo ripeschi da lì.`, 'Nel Cestino', true)) return;
  try {
    await chiedi('cestino');
    document.querySelector('.l-pannello')?.remove();
    hud('Nel Cestino', ic.cestino);
    const i = s.fratelli.findIndex((x) => x === f.percorso);
    if (i >= 0) s.fratelli.splice(i, 1);
    if (s.fratelli.length === 0) { void chiedi('finestra', { azione: 'chiudi' }); return; }
    s.indice = Math.min(i >= 0 ? i : s.indice, s.fratelli.length - 1) - 1;
    await vai(1);
  } catch (e) { hud((e as Error).message, ic.cestino); }
}

/** «Fai subito»: le conversioni al volo del tipo di file, senza lasciare il lettore (partono nella finestrella). */
function faiSubito(ancora?: HTMLElement) {
  const f = s.scheda;
  if (!f?.rapide.length) return;
  menuSu(ancora ?? (document.querySelector('.fai-subito') as HTMLElement), f.rapide.map((r) => ({
    testo: r.etichetta,
    fai: () => { void chiedi('rapida', { azione: r.id }).then(() => hud(`${r.etichetta}…`, ic.fulmine)).catch((e) => hud((e as Error).message)); },
  })));
}

const globali: Tasto[] = [
  { k: ['u'], etichetta: 'U', cosa: 'Fai subito (conversioni rapide)', fai: () => faiSubito() },
  { k: ['pagedown'], etichetta: 'Pag ↓', cosa: 'File dopo', fai: () => vai(1) },
  { k: ['pageup'], etichetta: 'Pag ↑', cosa: 'File prima', fai: () => vai(-1) },
  { k: ['f11'], etichetta: 'F11', cosa: 'Schermo intero', fai: () => schermoIntero() },
  { k: ['i'], etichetta: 'I', cosa: 'Proprietà', fai: () => void proprieta() },
  { k: ['delete'], etichetta: 'Canc', cosa: 'Nel Cestino', fai: () => void cestino() },
  { k: ['ctrl+e'], etichetta: 'Ctrl E', cosa: 'Converti', fai: () => { if (s.scheda?.convertibile) void chiedi('converti'); } },
  { k: ['ctrl+o'], etichetta: 'Ctrl O', cosa: 'Apri un file', fai: () => void chiedi('apriFile') },
  { k: ['ctrl+shift+c'], etichetta: 'Ctrl Maiusc C', cosa: 'Copia il percorso', fai: () => void navigator.clipboard.writeText(s.scheda!.percorso).then(() => hud('Percorso copiato', ic.copia)) },
  { k: ['ctrl+w'], etichetta: 'Ctrl W', cosa: 'Chiudi', fai: () => void chiedi('finestra', { azione: 'chiudi' }) },
  { k: ['?', 'f1'], etichetta: '?', cosa: 'Questi tasti', fai: () => foglietto() },
];

function foglietto() {
  const vecchio = document.querySelector('.l-velo.tasti');
  if (vecchio) { vecchio.remove(); return; }
  const blocco = (titolo: string, tt: Tasto[]) => h('div.l-tasti-blocco', null, h('b', null, titolo),
    ...tt.map((t) => h('div.l-tasti-riga', null, h('span.l-cappucci', null, ...t.etichetta.split(' ').map((x) => h('kbd', null, x))), h('span', null, t.cosa))));
  const velo = h('div.l-velo.tasti', { onclick: () => velo.remove() },
    h('div.l-foglietto', { style: { '--tinta': coloreTipo[s.scheda!.tipo] } },
      h('div.l-foglietto-testa', null, h('b', null, 'TASTI RAPIDI'), h('span', null, nomeTipo[s.scheda!.tipo])),
      h('div.l-foglietto-corpo', null,
        s.vista?.tasti.length ? blocco(nomeTipo[s.scheda!.tipo].toUpperCase(), s.vista.tasti) : null,
        blocco('SEMPRE', globali))));
  document.body.append(velo);
}

function chiave(e: KeyboardEvent) {
  let k = e.key.length === 1 ? e.key.toLowerCase() : e.key.toLowerCase();
  // Maiusc conta per i tasti speciali e per le lettere (Maiusc R); per i simboli (? + :) il segno già lo dice
  if (e.shiftKey && (e.key.length > 1 || /^[a-z]$/i.test(e.key))) k = 'shift+' + k;
  if (e.altKey) k = 'alt+' + k;
  if (e.ctrlKey || e.metaKey) k = 'ctrl+' + k;
  return k;
}

document.addEventListener('keydown', (e) => {
  const t = e.target as HTMLElement;
  const scrive = t instanceof HTMLInputElement || t instanceof HTMLTextAreaElement || t.isContentEditable;
  if (e.key === 'Escape') {
    const sopra = document.querySelector('.l-menu, .l-velo, .l-pannello');
    if (sopra) { sopra.remove(); e.preventDefault(); return; }
    if (scrive) { t.blur(); return; }
    if (document.fullscreenElement) { void document.exitFullscreen(); return; }
    return;
  }
  if (scrive) return;
  const k = chiave(e);
  const tasto = [...(s.vista?.tasti ?? []), ...globali].find((x) => x.k.includes(k));
  if (!tasto) return;
  e.preventDefault();
  tasto.fai(e);
});

// la rotella con Ctrl non zooma la pagina (ci pensano le viste che lo vogliono)
document.addEventListener('wheel', (e) => { if (e.ctrlKey) e.preventDefault(); }, { passive: false });

// trascina dei file dentro: li apre il lettore
document.addEventListener('dragover', (e) => { e.preventDefault(); radice.classList.add('lascia'); });
document.addEventListener('dragleave', (e) => { if (!e.relatedTarget) radice.classList.remove('lascia'); });
document.addEventListener('drop', (e) => {
  e.preventDefault();
  radice.classList.remove('lascia');
  const w = (window as any).chrome?.webview;
  if (w?.postMessageWithAdditionalObjects && e.dataTransfer?.files.length) w.postMessageWithAdditionalObjects({ cmd: 'lasciati' }, e.dataTransfer.files);
});

document.addEventListener('fullscreenchange', () => radice.classList.toggle('pieno', !!document.fullscreenElement));

// ————————————————————————— via —————————————————————————

async function avvia() {
  if (!dentroApp) {
    palco.append(h('div.l-errore', null, h('b', null, 'Il lettore vive dentro DaP Convertitore'), h('p', null, 'Aprilo da un file: doppio clic, o «Apri con» → DaP Convertitore.')));
    return;
  }
  ascolta('apri', (st: { file: Scheda }) => { s.fratelli = []; s.indice = -1; void mostra(st.file).then(caricaFratelli); });
  ascolta('aggiornamento', (a: { versione: string }) => { s.aggiornamento = a.versione; disegnaBarra(); });
  const st = await chiedi<{ file: Scheda; ffmpeg: boolean; office: boolean }>('stato');
  s.ffmpeg = ambiente.ffmpeg = st.ffmpeg;
  s.office = ambiente.office = st.office;
  await mostra(st.file);
  void caricaFratelli();
}

void avvia();
