// Il lettore: i pezzi che tutte le pagine usano. Ogni tipo di file ha la sua pagina (vista), che riceve il
// palco dove disegnarsi e restituisce i suoi tasti rapidi (finiscono anche nel foglietto del «?»).
import { h } from '../util';

export interface Scheda {
  percorso: string;
  nome: string;
  cartella: string;
  estensione: string;
  peso: number;
  creato: string;
  modificato: string;
  tipo: Tipo;
  url: string;
  categoria: string;
  convertibile: boolean;
  tipoWindows: string;
}

export type Tipo =
  | 'video' | 'audio' | 'immagine' | 'pdf' | 'documento' | 'presentazione' | 'tabella' | 'markdown' | 'web'
  | 'json' | 'codice' | 'testo' | 'sottotitoli' | 'archivio' | 'font' | 'modello' | 'esadecimale';

export interface Tasto {
  /** e.key, minuscolo per le lettere ("k", "arrowleft", " ", "+"). Con "ctrl+" davanti se serve Ctrl. */
  k: string[];
  /** Come lo scrive il foglietto dei tasti. */
  etichetta: string;
  cosa: string;
  fai: (e: KeyboardEvent) => void;
}

export interface Vista {
  tasti: Tasto[];
  smonta(): void;
  /** La barra in alto sparisce quando il mouse sta fermo (video, foto, 3D). */
  barraSopra?: boolean;
  /** Le frecce ai lati per il file prima e dopo (le foto le usano; il video no: le frecce scorrono il tempo). */
  frecceLaterali?: boolean;
  /** Il pezzetto che la vista mette nella barra in alto (pagina 3/24, 1920×1080…). */
  info?: () => string;
  /** Le righe in più nel pannello delle proprietà (i dati della foto, le tracce del video…). */
  scheda?: () => Promise<Node[]>;
}

export interface Contesto {
  scheda: Scheda;
  palco: HTMLElement;
  /** Il messaggio grande in mezzo che sparisce da solo (volume, +10 s, zoom…). */
  hud(testo: string, icona?: string): void;
  /** La riga in basso che dice cosa sta succedendo («Traduco il video al volo…»). */
  stato(testo: string | null): void;
  /** Aggiorna il pezzetto di informazioni nella barra in alto. */
  info(): void;
  /** Al file dopo o prima nella cartella. */
  vai(passo: number): void;
  /** Ci sono altri file accanto da scorrere? */
  fratelli(): number;
  /** I file accanto (percorsi) e dove sei; e andare dritto a uno. */
  elenco(): { file: string[]; indice: number };
  vaiA(indice: number): void;
  /** La vista è ancora quella (il file non è cambiato nel frattempo). */
  viva(): boolean;
}

export const coloreTipo: Record<Tipo, string> = {
  video: '#ff3df2',
  audio: '#5dffb4',
  immagine: '#35e8ff',
  pdf: '#ff4d6d',
  documento: '#6ea8ff',
  presentazione: '#ff9f43',
  tabella: '#4dd17a',
  markdown: '#ebe7f4',
  web: '#ff9f43',
  json: '#b48cff',
  codice: '#ffd54a',
  testo: '#ebe7f4',
  sottotitoli: '#ffd54a',
  archivio: '#c9a26b',
  font: '#c18cff',
  modello: '#35e8ff',
  esadecimale: '#a19db0',
};

export const nomeTipo: Record<Tipo, string> = {
  video: 'Video', audio: 'Audio', immagine: 'Foto', pdf: 'PDF', documento: 'Documento', presentazione: 'Presentazione',
  tabella: 'Tabella', markdown: 'Markdown', web: 'Pagina web', json: 'JSON', codice: 'Codice', testo: 'Testo',
  sottotitoli: 'Sottotitoli', archivio: 'Archivio', font: 'Carattere', modello: 'Modello 3D', esadecimale: 'File',
};

/** Una finestrella di conferma dentro la pagina (niente finestre di Windows che saltano fuori). */
export function conferma(titolo: string, testo: string, si: string, pericolo = false): Promise<boolean> {
  return new Promise((ok) => {
    const chiudi = (v: boolean) => { velo.classList.add('via'); setTimeout(() => velo.remove(), 160); document.removeEventListener('keydown', tasto, true); ok(v); };
    const tasto = (e: KeyboardEvent) => {
      if (e.key === 'Escape') { e.stopPropagation(); e.preventDefault(); chiudi(false); }
      if (e.key === 'Enter') { e.stopPropagation(); e.preventDefault(); chiudi(true); }
    };
    const velo = h('div.l-velo', { onclick: (e: Event) => { if (e.target === velo) chiudi(false); } },
      h('div.l-dialogo', null,
        h('b', null, titolo),
        h('p', null, testo),
        h('div.l-dialogo-tasti', null,
          h('button.l-tasto', { onclick: () => chiudi(false) }, 'Annulla'),
          h(`button.l-tasto.${pericolo ? 'rosso' : 'oro'}`, { onclick: () => chiudi(true) }, si))));
    document.addEventListener('keydown', tasto, true);
    document.body.append(velo);
    (velo.querySelector('.l-tasto:last-child') as HTMLElement).focus();
  });
}

/** Un menu che si apre sotto (o sopra) un bottone: tracce audio, sottotitoli, velocità… */
export function menuSu(ancora: HTMLElement, voci: { testo: string; attiva?: boolean; fai: () => void }[], sopra = false) {
  document.querySelector('.l-menu')?.remove();
  const m = h('div.l-menu', null, ...voci.map((v) => h(`button${v.attiva ? '.su' : ''}`, { onclick: (e: Event) => { e.stopPropagation(); m.remove(); v.fai(); } }, h('i'), v.testo)));
  document.body.append(m);
  const b = ancora.getBoundingClientRect();
  const r = m.getBoundingClientRect();
  m.style.left = `${Math.max(8, Math.min(window.innerWidth - r.width - 8, b.left + b.width / 2 - r.width / 2))}px`;
  m.style.top = sopra ? `${b.top - r.height - 8}px` : `${b.bottom + 8}px`;
  setTimeout(() => document.addEventListener('pointerdown', function via(e) {
    if (!m.contains(e.target as Node)) { m.remove(); document.removeEventListener('pointerdown', via); }
  }), 0);
}

/** Aspetta un evento una volta sola. */
export function evento(el: EventTarget, nome: string): Promise<Event> {
  return new Promise((ok) => el.addEventListener(nome, ok, { once: true }));
}

/** Un caricamento che si vede: tre barre che ballano, come un vu-meter. */
export function attesa(testo: string) {
  return h('div.l-attesa', null, h('div.l-ballo', null, h('i'), h('i'), h('i'), h('i'), h('i')), h('span', null, testo));
}

export function errore(testo: string, dettaglio?: string) {
  return h('div.l-errore', null, h('b', null, 'Non riesco a mostrarlo'), h('p', null, testo), dettaglio ? h('small', null, dettaglio) : null);
}

export const dataItaliana = (iso: string) => new Date(iso).toLocaleString('it-IT', { day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit' });

/** Memoria per file (dove eri arrivato nel video, lo zoom…), solo su questo PC. */
export const memoria = {
  leggi<T>(chiave: string): T | null {
    try { const v = localStorage.getItem(chiave); return v ? JSON.parse(v) as T : null; } catch { return null; }
  },
  scrivi(chiave: string, v: unknown) {
    try { if (v == null) localStorage.removeItem(chiave); else localStorage.setItem(chiave, JSON.stringify(v)); } catch { /* pieno o spento */ }
  },
};

/** L'icona piccola: stessa mano delle icone del convertitore. */
const i = (corpo: string) => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">${corpo}</svg>`;

export const ic = {
  play: i('<path d="M7 4.5l12.5 7.5L7 19.5z" fill="currentColor"/>'),
  pausa: i('<rect x="6.5" y="5" width="3.6" height="14" rx="1" fill="currentColor"/><rect x="13.9" y="5" width="3.6" height="14" rx="1" fill="currentColor"/>'),
  indietro10: i('<path d="M4 12a8 8 0 1 0 2.3-5.6"/><path d="M4 4v4h4"/><text x="12" y="15.2" font-size="7" font-family="Rajdhani" font-weight="700" fill="currentColor" stroke="none" text-anchor="middle">10</text>'),
  avanti10: i('<path d="M20 12a8 8 0 1 1-2.3-5.6"/><path d="M20 4v4h-4"/><text x="12" y="15.2" font-size="7" font-family="Rajdhani" font-weight="700" fill="currentColor" stroke="none" text-anchor="middle">10</text>'),
  volume: i('<path d="M4 9.5h3.5L12 5.5v13l-4.5-4H4z" fill="currentColor" stroke-width="1.2"/><path d="M15.5 9a4 4 0 0 1 0 6M18 6.5a7.5 7.5 0 0 1 0 11"/>'),
  muto: i('<path d="M4 9.5h3.5L12 5.5v13l-4.5-4H4z" fill="currentColor" stroke-width="1.2"/><path d="M16 9.5l5 5M21 9.5l-5 5"/>'),
  schermo: i('<path d="M4 9V5h4M20 9V5h-4M4 15v4h4M20 15v4h-4"/>'),
  finestra: i('<path d="M9 5v4H5M15 5v4h4M9 19v-4H5M15 19v-4h4"/>'),
  sottotitoli: i('<rect x="3" y="5" width="18" height="14" rx="2.5"/><path d="M7 15h4M13 15h4M7 11h2M11 11h6"/>'),
  tracce: i('<path d="M4 7h10M4 12h16M4 17h7"/><circle cx="18" cy="7" r="2"/><circle cx="15" cy="17" r="2"/>'),
  velocita: i('<path d="M4.5 16a8 8 0 1 1 15 0"/><path d="M12 15l4-5"/><circle cx="12" cy="15.3" r="1.2" fill="currentColor"/>'),
  pip: i('<rect x="3" y="5" width="18" height="14" rx="2"/><rect x="12" y="11" width="7" height="6" rx="1" fill="currentColor"/>'),
  foto: i('<path d="M4 8h3l2-2.5h6L17 8h3v11H4z"/><circle cx="12" cy="13" r="3.5"/>'),
  prima: i('<path d="M6 5v14"/><path d="M19 5L9 12l10 7z" fill="currentColor"/>'),
  dopo: i('<path d="M18 5v14"/><path d="M5 5l10 7-10 7z" fill="currentColor"/>'),
  ripeti: i('<path d="M4 11V9a3 3 0 0 1 3-3h12l-3-3M20 13v2a3 3 0 0 1-3 3H5l3 3"/>'),
  elenco: i('<path d="M9 6h11M9 12h11M9 18h11"/><circle cx="4.5" cy="6" r="1" fill="currentColor"/><circle cx="4.5" cy="12" r="1" fill="currentColor"/><circle cx="4.5" cy="18" r="1" fill="currentColor"/>'),
  sx: i('<path d="M15 5l-7 7 7 7"/>'),
  dx: i('<path d="M9 5l7 7-7 7"/>'),
  zoomPiu: i('<circle cx="11" cy="11" r="7"/><path d="M20 20l-4-4M11 8v6M8 11h6"/>'),
  zoomMeno: i('<circle cx="11" cy="11" r="7"/><path d="M20 20l-4-4M8 11h6"/>'),
  adatta: i('<rect x="4" y="4" width="16" height="16" rx="2"/><path d="M9 4v16M15 4v16" stroke-dasharray="2 2"/>'),
  ruota: i('<path d="M20 12a8 8 0 1 1-2.6-5.9"/><path d="M20 4v5h-5"/>'),
  info: i('<circle cx="12" cy="12" r="9"/><path d="M12 11v6M12 7.5v.01"/>'),
  converti: i('<path d="M5 9a7 7 0 0 1 12.5-3.5L20 8"/><path d="M20 3v5h-5"/><path d="M19 15a7 7 0 0 1-12.5 3.5L4 16"/><path d="M4 21v-5h5"/>'),
  apriCon: i('<rect x="3" y="3" width="8" height="8" rx="2"/><rect x="13" y="3" width="8" height="8" rx="2"/><rect x="3" y="13" width="8" height="8" rx="2"/><path d="M17 14v6M14 17h6"/>'),
  cartella: i('<path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>'),
  cestino: i('<path d="M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13"/>'),
  tasti: i('<rect x="2.5" y="6" width="19" height="12" rx="2"/><path d="M6 10h.01M9 10h.01M12 10h.01M15 10h.01M18 10h.01M7 14h10"/>'),
  chiudi: i('<path d="M6 6l12 12M18 6L6 18"/>'),
  riduci: i('<path d="M6 12h12"/>'),
  ingrandisci: i('<rect x="6" y="6" width="12" height="12" rx="2"/>'),
  cerca: i('<circle cx="11" cy="11" r="7"/><path d="M20 20l-4-4"/>'),
  copia: i('<rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V6a2 2 0 0 0-2-2H6a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h2"/>'),
  sfondo: i('<rect x="3" y="4" width="18" height="13" rx="2"/><path d="M8 21h8M12 17v4"/><path d="M7 13l3-3 2 2 3-3 2 2"/>'),
  diapo: i('<rect x="3" y="4" width="18" height="12" rx="2"/><path d="M10 8l4 2-4 2z" fill="currentColor"/><path d="M8 20h8"/>'),
  estrai: i('<path d="M12 3v12M7 10l5 5 5-5"/><path d="M4 17v3h16v-3"/>'),
  scarica: i('<path d="M12 4v12M6 11l6 6 6-6M5 20h14"/>'),
  ok: i('<path d="M5 12.5l4.5 4.5L19 7.5"/>'),
  avvolgi: i('<path d="M4 6h16M4 12h13a3 3 0 0 1 0 6h-4"/><path d="M15 16l-2 2 2 2"/><path d="M4 18h5"/>'),
  sole: i('<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M2 12h2M20 12h2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/>'),
  griglia: i('<path d="M3 9h18M3 15h18M9 3v18M15 3v18"/>'),
  cubo: i('<path d="M12 2l9 5v10l-9 5-9-5V7z"/><path d="M12 22V12M21 7l-9 5-9-5"/>'),
  libro: i('<path d="M4 5a2 2 0 0 1 2-2h13v16H6a2 2 0 0 0-2 2z"/><path d="M4 19V5"/><path d="M8 7h7M8 11h5"/>'),
  codice: i('<path d="M8 7l-5 5 5 5M16 7l5 5-5 5M13.5 4l-3 16"/>'),
  impaginato: i('<path d="M6 2h8l5 5v15H6z"/><path d="M14 2v5h5"/><path d="M9 12h7M9 15h7M9 18h4"/>'),
  apri: i('<path d="M14 4h6v6"/><path d="M20 4l-9 9"/><path d="M19 14v4a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2h4"/>'),
};

/** Quello che il lettore sa del PC: FFmpeg c'è, Office o LibreOffice per l'impaginato vero. */
export const ambiente = { ffmpeg: true, office: false };

export function schermoIntero() {
  if (document.fullscreenElement) void document.exitFullscreen();
  else void document.documentElement.requestFullscreen().catch(() => { /* la pagina non può */ });
}
