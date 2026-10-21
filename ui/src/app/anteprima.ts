// Il pannello a destra, come in VS Code: il testo del file scelto nella coda, coi numeri di riga. Si disegnano solo
// le righe che si vedono (anche un registro da 6 MB scorre liscio), Ctrl+F cerca con le evidenziature, il contatore
// e le frecce. Si allarga e si stringe trascinando il bordo sinistro; la larghezza e se è aperto restano.
import { chiedi } from '../ponte';
import type { InfoFile } from '../ponte';
import { h, peso, clamp, numero } from '../util';
import { icone } from '../icone';
import { clac } from '../suoni';
import { s } from './stato';
import { corpo } from './scheletro';
import { indicizza, cerca, rigaVideoDi, pezzoHtml, vicino, MAX_TROVATI, type Doc, type Trovati } from './righe';

interface Letto { testo: string; codifica: string; bom: boolean; troncato: boolean; peso: number }

/** Le categorie che si leggono come testo. */
const TESTUALI = new Set(['testo', 'dati', 'sottotitoli']);
export const testuale = (f?: InfoFile) => !!f && !f.eDirectory && TESTUALI.has(f.categoria);

const memo = {
  aperta: () => localStorage.getItem('anteprima-aperta') !== '0',
  larghezza: () => Number(localStorage.getItem('anteprima-larghezza')) || LARGHEZZA,
};

const ALTEZZA_RIGA = 19;
/** La larghezza di partenza, e quanto deve restare alla piastra in mezzo (con un testo scelto ci sono solo i formati). */
const LARGHEZZA = 400, PIASTRA_MIN = 290;
/** Quanto si legge al massimo (Contenuti.MaxTesto nel motore): un registro da un giga non serve tutto a schermo. */
const MAX_BYTE = 6 * 1024 * 1024;
/** Chromium non fa elementi più alti di ~33 milioni di pixel: oltre, lo scorrimento si fa in scala. */
const ALTEZZA_MAX = 3e7;

export const pannello = h('aside.anteprima');
const maniglia = h('div.ap-maniglia', { title: 'Trascina per allargare · doppio clic: larghezza di partenza' });
const linguetta = h('button.ap-linguetta', { title: 'Apri l\'anteprima del testo' }, h('span', { html: icone.pannello }), h('span.ap-linguetta-testo', null, 'ANTEPRIMA'));
corpo.append(pannello, linguetta);

// ——— lo stato del pannello ———
let fileId: string | null = null;       // il file che si sta mostrando
let chiesto: string | null = null;      // il file di cui si aspetta il testo
const letti = new Map<string, { letto: Letto; doc: Doc }>();
let doc: Doc | null = null;
let trovati: Trovati = { pos: new Int32Array(0), lung: 0, troppi: false };
let scelto = -1;
let maiuscole = false;
let da = -1, a = -1;                    // le righe a video disegnate adesso

// ——— i pezzi ———
const nome = h('div.ap-nome');
const info = h('div.ap-info');
const testa = h('div.ap-testa', null,
  h('div.ap-titolo', null, h('span', null, 'ANTEPRIMA'), h('span.ap-azioni', null,
    h('button.ap-btn', { title: 'Cerca (Ctrl+F)', html: icone.cerca, onclick: () => apriCerca() }),
    h('button.ap-btn', { title: 'Apri nel lettore', html: icone.occhio, onclick: () => { const f = fileId ? s.file.get(fileId) : null; if (f) { clac(); void chiedi('guarda', { percorso: f.percorso }); } } }),
    h('button.ap-btn', { title: 'Chiudi l\'anteprima', html: icone.chiudi, onclick: () => apri(false) }))),
  nome);

const campo = h<HTMLInputElement>('input.ap-campo', { placeholder: 'Cerca', spellcheck: false, 'aria-label': 'Cerca nel testo' });
const conta = h('span.ap-conta');
const bAa = h<HTMLButtonElement>('button.ap-aa', { title: 'Maiuscole e minuscole (Alt+C)', onclick: () => { maiuscole = !maiuscole; bAa.classList.toggle('su', maiuscole); nuovaRicerca(); campo.focus(); } }, 'Aa');
const bSu = h<HTMLButtonElement>('button.ap-btn', { title: 'Il precedente (Maiusc+Invio, Maiusc+F3)', html: icone.su, onclick: () => salta(-1) });
const bGiu = h<HTMLButtonElement>('button.ap-btn', { title: 'Il prossimo (Invio, F3)', html: icone.giu, onclick: () => salta(1) });
const barraCerca = h('div.ap-cerca', { hidden: true }, h('span.ap-lente', { html: icone.cerca }), campo, bAa, conta, bSu, bGiu,
  h('button.ap-btn', { title: 'Chiudi (Esc)', html: icone.chiudi, onclick: () => chiudiCerca() }));

const fogli = h('div.ap-righe');
const spazio = h('div.ap-spazio', null, fogli);
const scorre = h('div.ap-scorre', { tabindex: '0' }, spazio);
const avviso = h('div.ap-avviso');
const piede = h('div.ap-piede');
pannello.append(maniglia, testa, barraCerca, scorre, avviso, piede);

// ————————————————————————— aprire, chiudere, allargare —————————————————————————

function apri(si: boolean) {
  clac();
  localStorage.setItem('anteprima-aperta', si ? '1' : '0');
  aggiornaAnteprima();
  if (si) requestAnimationFrame(() => disegna(true));
}
linguetta.addEventListener('click', () => apri(true));

function applicaLarghezza(l: number) {
  // la piastra in mezzo non va sotto i 290 px: due colonne di formati (la coda a sinistra ne prende 318)
  const max = Math.max(280, corpo.clientWidth - 318 - PIASTRA_MIN);
  corpo.style.setProperty('--ap-larghezza', `${clamp(l, 280, max)}px`);
}

maniglia.addEventListener('pointerdown', (e) => {
  e.preventDefault();
  maniglia.setPointerCapture(e.pointerId);
  pannello.classList.add('tira');
  const destra = corpo.getBoundingClientRect().right;
  const muovi = (ev: PointerEvent) => applicaLarghezza(destra - ev.clientX);
  const lascia = () => {
    pannello.classList.remove('tira');
    maniglia.removeEventListener('pointermove', muovi);
    maniglia.removeEventListener('pointerup', lascia);
    maniglia.removeEventListener('pointercancel', lascia);
    localStorage.setItem('anteprima-larghezza', String(parseInt(corpo.style.getPropertyValue('--ap-larghezza')) || LARGHEZZA));
    disegna(true);
  };
  maniglia.addEventListener('pointermove', muovi);
  maniglia.addEventListener('pointerup', lascia);
  maniglia.addEventListener('pointercancel', lascia);
});
maniglia.addEventListener('dblclick', () => { localStorage.removeItem('anteprima-larghezza'); applicaLarghezza(LARGHEZZA); disegna(true); });
new ResizeObserver(() => { if (doc && !pannello.hidden) disegna(); }).observe(scorre);
window.addEventListener('resize', () => applicaLarghezza(memo.larghezza()));

// ————————————————————————— quale file —————————————————————————

/** Si chiama a ogni ridisegno della piastra: mostra (o nasconde) il pannello per il file scelto nella coda. */
export function aggiornaAnteprima() {
  const f = s.fileAttivo ? s.file.get(s.fileAttivo) : undefined;
  const c = s.stato.modo === 'finestra' && testuale(f);
  const aperta = c && memo.aperta();
  corpo.classList.toggle('con-anteprima', aperta);
  corpo.classList.toggle('con-linguetta', c && !aperta);
  pannello.hidden = !aperta;
  linguetta.hidden = !(c && !aperta);
  if (aperta) applicaLarghezza(memo.larghezza());
  if (!aperta || !f) return;
  if (f.id === fileId && doc) { requestAnimationFrame(() => disegna()); return; }
  if (f.id === fileId && chiesto === f.id) return;
  void carica(f);
}

async function carica(f: InfoFile) {
  fileId = f.id;
  nome.textContent = f.nome;
  nome.title = f.percorso;
  const gia = letti.get(f.id);
  if (gia) { mostra(gia.letto, gia.doc); return; }
  doc = null;
  chiesto = f.id;
  fogli.replaceChildren();
  spazio.style.height = '0';
  info.textContent = '';
  piede.replaceChildren(h('span', null, 'Leggo…'));
  avviso.hidden = true;
  try {
    const letto = await chiedi<Letto>('anteprimaTesto', { id: f.id });
    if (chiesto !== f.id) return;
    const d = indicizza(letto.testo);
    letti.set(f.id, { letto, doc: d });
    // se ne tengono pochi: un testo da 6 MB pesa
    while (letti.size > 6) letti.delete(letti.keys().next().value!);
    mostra(letto, d);
  } catch (e) {
    if (chiesto !== f.id) return;
    piede.replaceChildren(h('span.ap-errore', null, `Non si legge: ${(e as Error).message}`));
  } finally {
    if (chiesto === f.id) chiesto = null;
  }
}

function mostra(letto: Letto, d: Doc) {
  doc = d;
  const righe = d.inizi.length;
  avviso.hidden = !letto.troncato;
  avviso.textContent = letto.troncato ? `È lungo ${peso(letto.peso)}: qui ne vedi i primi ${peso(MAX_BYTE)}.` : '';
  piede.replaceChildren(
    h('span.ap-chip', { title: letto.bom ? 'Con il BOM all\'inizio' : 'Senza BOM' }, letto.codifica + (letto.bom ? ' · BOM' : '')),
    h('span', null, d.crlf ? 'CRLF' : 'LF'),
    h('span', null, `${numero(righe)} ${righe === 1 ? 'riga' : 'righe'}`),
    h('span', null, peso(letto.peso)),
    h('span.ap-dove'));
  scorre.style.setProperty('--cifre', String(String(righe).length));
  scorre.scrollTop = 0;
  scorre.scrollLeft = 0;
  cercato.q = '';
  if (campo.value && !barraCerca.hidden) nuovaRicerca(); else { trovati = { pos: new Int32Array(0), lung: 0, troppi: false }; scelto = -1; contatore(); }
  disegna(true);
}

// ————————————————————————— disegnare solo quello che si vede —————————————————————————

function totale() { return doc ? doc.vRiga.length : 0; }
function altezzaSpazio() { return Math.min(totale() * ALTEZZA_RIGA, ALTEZZA_MAX); }
function inScala() { return totale() * ALTEZZA_RIGA > ALTEZZA_MAX; }

/** La riga a video (con la frazione) che sta in cima, da quanto si è scesi. */
function primaVisibile(): number {
  if (!inScala()) return scorre.scrollTop / ALTEZZA_RIGA;
  const corsa = altezzaSpazio() - scorre.clientHeight;
  const visibili = scorre.clientHeight / ALTEZZA_RIGA;
  return corsa > 0 ? (scorre.scrollTop / corsa) * Math.max(0, totale() - visibili) : 0;
}

function topDi(riga: number): number {
  if (!inScala()) return riga * ALTEZZA_RIGA;
  const visibili = scorre.clientHeight / ALTEZZA_RIGA;
  return (riga / Math.max(1, totale() - visibili)) * (altezzaSpazio() - scorre.clientHeight);
}

let rafPendente = false;
scorre.addEventListener('scroll', () => {
  if (rafPendente) return;
  rafPendente = true;
  requestAnimationFrame(() => { rafPendente = false; disegna(); });
}, { passive: true });

function disegna(forza = false) {
  if (!doc || pannello.hidden) return;
  const d = doc;
  const n = totale();
  spazio.style.height = `${altezzaSpazio()}px`;
  spazio.style.setProperty('--lunga', String(d.lungMax));
  const fv = primaVisibile();
  const visibili = Math.ceil(scorre.clientHeight / ALTEZZA_RIGA) + 1;
  const primo = Math.max(0, Math.floor(fv) - 20);
  const ultimo = Math.min(n, Math.floor(fv) + visibili + 20);
  if (forza || primo < da || ultimo > a) {
    da = Math.max(0, Math.floor(fv) - 60);
    a = Math.min(n, Math.floor(fv) + visibili + 60);
    const rigaScelta = scelto >= 0 ? d.vRiga[rigaVideoDi(d, trovati.pos[scelto])] : -1;
    let html = '';
    for (let v = da; v < a; v++) {
      const r = d.vRiga[v];
      const primaDellaRiga = d.prima[r] === v;
      html += `<div class="ap-r${r === rigaScelta ? ' su' : ''}${primaDellaRiga ? '' : ' segue'}"><i>${primaDellaRiga ? r + 1 : ''}</i><code>${pezzoHtml(d, d.vDa[v], d.vA[v], trovati, scelto) || ' '}</code></div>`;
    }
    fogli.innerHTML = html;
  }
  // le righe stanno dove devono: in scala si spostano a ogni scorrimento, se no stanno ferme
  fogli.style.transform = `translateY(${scorre.scrollTop - (fv - da) * ALTEZZA_RIGA}px)`;
  const dove = piede.querySelector('.ap-dove');
  if (dove) dove.textContent = `riga ${numero(d.vRiga[Math.min(n - 1, Math.floor(fv))] + 1)}`;
}

// ————————————————————————— Ctrl+F —————————————————————————

function apriCerca() {
  if (!doc) return;
  barraCerca.hidden = false;
  // come VS Code: se c'è del testo scelto (su una riga sola) si cerca quello
  const sel = getSelection()?.toString() ?? '';
  if (sel && !sel.includes('\n') && sel.length < 200 && pannello.contains(getSelection()!.anchorNode)) campo.value = sel;
  campo.focus();
  campo.select();
  if (campo.value) nuovaRicerca();
}

function chiudiCerca() {
  barraCerca.hidden = true;
  cercato.q = '';
  trovati = { pos: new Int32Array(0), lung: 0, troppi: false };
  scelto = -1;
  contatore();
  disegna(true);
  scorre.focus();
}

let attesaRicerca = 0;
campo.addEventListener('input', () => {
  // sui testi grossi si aspetta che finisci di scrivere
  clearTimeout(attesaRicerca);
  attesaRicerca = window.setTimeout(nuovaRicerca, doc && doc.testo.length > 500_000 ? 160 : 0);
});

campo.addEventListener('keydown', (e) => {
  if (e.key === 'Enter' || e.key === 'F3') {
    e.preventDefault();
    clearTimeout(attesaRicerca);
    // Invio prima che la ricerca sia partita (si scriveva ancora): prima si cerca, poi si salta
    if (campo.value !== cercato.q || maiuscole !== cercato.aa) nuovaRicerca(); else salta(e.shiftKey ? -1 : 1);
  }
  else if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); chiudiCerca(); }
  else if (e.altKey && e.key.toLowerCase() === 'c') { e.preventDefault(); bAa.click(); }
});

/** Quello che si è cercato l'ultima volta. */
const cercato = { q: '', aa: false };

function nuovaRicerca() {
  if (!doc) return;
  cercato.q = campo.value;
  cercato.aa = maiuscole;
  trovati = cerca(doc, campo.value, maiuscole);
  // si parte dal primo trovato da dove stai guardando, non dall'inizio del file
  scelto = trovati.pos.length ? vicino(trovati, doc.vDa[Math.min(totale() - 1, Math.floor(primaVisibile()))]) : -1;
  contatore();
  if (scelto >= 0) vaiAlTrovato(); else disegna(true);
}

function salta(verso: 1 | -1) {
  if (!trovati.pos.length) return;
  scelto = (scelto + verso + trovati.pos.length) % trovati.pos.length;
  contatore();
  vaiAlTrovato();
}

function contatore() {
  const n = trovati.pos.length;
  conta.textContent = !campo.value ? '' : n ? `${numero(scelto + 1)} di ${numero(n)}${trovati.troppi ? '+' : ''}` : 'Nessun risultato';
  conta.classList.toggle('niente', !!campo.value && !n);
  bSu.disabled = bGiu.disabled = n === 0;
  campo.title = trovati.troppi ? `Mi fermo a ${numero(MAX_TROVATI)} risultati` : '';
}

function vaiAlTrovato() {
  if (!doc || scelto < 0) return;
  const v = rigaVideoDi(doc, trovati.pos[scelto]);
  const top = topDi(v);
  // si scorre solo se il trovato è fuori vista, e allora lo si mette a metà (in scala si conta in righe, non in pixel)
  if (inScala() || top < scorre.scrollTop || top + ALTEZZA_RIGA > scorre.scrollTop + scorre.clientHeight)
    scorre.scrollTop = Math.max(0, topDi(Math.max(0, v - scorre.clientHeight / ALTEZZA_RIGA / 2 + 0.5)));
  disegna(true);
  // e di lato: le righe lunghe scorrono anche in orizzontale
  const m = fogli.querySelector('mark.su');
  if (m) {
    const r = m.getBoundingClientRect(), vista = scorre.getBoundingClientRect();
    const margine = (fogli.querySelector('.ap-r i') as HTMLElement | null)?.offsetWidth ?? 40;
    if (r.left < vista.left + margine || r.right > vista.right - 12) scorre.scrollLeft += r.left - vista.left - margine - 60;
  }
}

// ————————————————————————— tasti e tasto destro —————————————————————————

document.addEventListener('keydown', (e) => {
  if (pannello.hidden || !doc || document.querySelector('.velo')) return;
  const k = e.key.toLowerCase();
  if ((e.ctrlKey || e.metaKey) && !e.altKey && k === 'f') { e.preventDefault(); apriCerca(); }
  else if (e.key === 'F3' && e.target !== campo) { e.preventDefault(); if (barraCerca.hidden) apriCerca(); else salta(e.shiftKey ? -1 : 1); }
  else if (e.key === 'Escape' && !barraCerca.hidden && pannello.contains(e.target as Node)) chiudiCerca();
});

// Il menu della piastra (Incolla, Aggiungi file) qui non serve: sul testo scelto si copia, o si cerca quello
pannello.addEventListener('contextmenu', (e) => {
  e.preventDefault();
  e.stopPropagation();
  document.querySelector('.menu-destro')?.remove();
  const sel = getSelection()?.toString() ?? '';
  if (!sel) return;
  const voce = (testo: string, icona: string, tasto: string, fai: () => void) =>
    h('button', { onclick: () => { m.remove(); fai(); } }, h('span', { html: icona }), h('b', null, testo), h('small', null, tasto));
  const m = h('div.menu-destro', null,
    voce('Copia', icone.copia, 'Ctrl+C', () => void navigator.clipboard.writeText(sel)),
    sel.includes('\n') ? null : voce('Cerca questo', icone.cerca, 'Ctrl+F', () => { campo.value = sel; apriCerca(); }));
  document.body.append(m);
  const r = m.getBoundingClientRect();
  m.style.left = `${Math.min(e.clientX, window.innerWidth - r.width - 8)}px`;
  m.style.top = `${Math.min(e.clientY, window.innerHeight - r.height - 8)}px`;
  setTimeout(() => document.addEventListener('pointerdown', function via(ev) {
    if (!m.contains(ev.target as Node)) { m.remove(); document.removeEventListener('pointerdown', via); }
  }), 0);
});

/** Un file tolto dalla coda: il suo testo non serve più. */
export function dimenticaAnteprima(id?: string) {
  if (id) letti.delete(id); else letti.clear();
  if (!id || id === fileId) { fileId = null; doc = null; }
}
