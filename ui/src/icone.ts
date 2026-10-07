// Le icone, disegnate a mano: tratto 1.8, angoli tondi, stanno su una griglia di 24.

const i = (corpo: string) => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">${corpo}</svg>`;

export const icone = {
  chiudi: i('<path d="M6 6l12 12M18 6L6 18"/>'),
  riduci: i('<path d="M6 12h12"/>'),
  ingrandisci: i('<rect x="6" y="6" width="12" height="12" rx="2"/>'),
  piu: i('<path d="M12 5v14M5 12h14"/>'),
  cartella: i('<path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>'),
  apri: i('<path d="M14 4h6v6"/><path d="M20 4l-9 9"/><path d="M19 14v4a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2h4"/>'),
  confronta: i('<rect x="3" y="5" width="18" height="14" rx="2"/><path d="M12 3v18"/><path d="M8 10l-2 2 2 2M16 10l2 2-2 2"/>'),
  ingranaggio: i('<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.8-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.8.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.8 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.8.3h0a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.8-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.8v0a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1z"/>'),
  play: i('<path d="M7 5l12 7-12 7z" fill="currentColor"/>'),
  ferma: i('<rect x="6" y="6" width="12" height="12" rx="1.5" fill="currentColor"/>'),
  ok: i('<path d="M5 12.5l4.5 4.5L19 7.5"/>'),
  attenzione: i('<path d="M12 3l9.5 17h-19z"/><path d="M12 10v4M12 17.5v.01"/>'),
  freccia: i('<path d="M5 12h14M13 6l6 6-6 6"/>'),
  indietro: i('<path d="M19 12H5M11 6l-6 6 6 6"/>'),
  scarica: i('<path d="M12 4v12M6 11l6 6 6-6M5 20h14"/>'),
  cestino: i('<path d="M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13"/>'),
  fulmine: i('<path d="M13 2L4 14h7l-1 8 9-12h-7z" fill="currentColor" stroke="none"/>'),
  chip: i('<rect x="6" y="6" width="12" height="12" rx="2"/><path d="M9 2v4M15 2v4M9 18v4M15 18v4M2 9h4M2 15h4M18 9h4M18 15h4"/>'),
  aggiungi: i('<path d="M12 5v14M5 12h14"/>'),
  menu: i('<path d="M4 7h16M4 12h16M4 17h10"/>'),
};

export const iconaCategoria: Record<string, string> = {
  video: i('<rect x="3" y="6" width="13" height="12" rx="2"/><path d="M16 10l5-3v10l-5-3z"/>'),
  audio: i('<path d="M9 18V5l11-2v13"/><circle cx="6" cy="18" r="3"/><circle cx="17" cy="16" r="3"/>'),
  immagine: i('<rect x="3" y="4" width="18" height="16" rx="2"/><circle cx="9" cy="10" r="2"/><path d="M21 16l-5-5-9 9"/>'),
  pdf: i('<path d="M6 2h8l5 5v15H6z"/><path d="M14 2v5h5"/><path d="M9 14h6M9 17h4"/>'),
  documento: i('<path d="M6 2h8l5 5v15H6z"/><path d="M14 2v5h5"/><path d="M9 12h7M9 15h7M9 18h5"/>'),
  foglio: i('<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M3 10h18M3 15h18M9 4v16M15 4v16"/>'),
  presentazione: i('<rect x="3" y="4" width="18" height="12" rx="2"/><path d="M12 16v4M8 20h8"/><path d="M8 12l3-3 2 2 3-3"/>'),
  testo: i('<path d="M5 6h14M5 10h14M5 14h10M5 18h7"/>'),
  dati: i('<ellipse cx="12" cy="6" rx="8" ry="3"/><path d="M4 6v6c0 1.7 3.6 3 8 3s8-1.3 8-3V6"/><path d="M4 12v6c0 1.7 3.6 3 8 3s8-1.3 8-3v-6"/>'),
  sottotitoli: i('<rect x="3" y="5" width="18" height="14" rx="2"/><path d="M7 15h4M13 15h4M7 11h10"/>'),
  archivio: i('<rect x="4" y="3" width="16" height="18" rx="2"/><path d="M12 3v2M12 7v2M12 11v2"/><rect x="10" y="14" width="4" height="4" rx="1"/>'),
  cartella: i('<path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/>'),
  altro: i('<path d="M6 2h8l5 5v15H6z"/><path d="M14 2v5h5"/>'),
};

/** Ogni categoria ha il suo colore, come le cassette di una volta. */
export const coloreCategoria: Record<string, string> = {
  video: '#ff3df2',
  audio: '#5dffb4',
  immagine: '#35e8ff',
  pdf: '#ff4d6d',
  documento: '#6ea8ff',
  foglio: '#4dd17a',
  presentazione: '#ff9f43',
  testo: '#ebe7f4',
  dati: '#b48cff',
  sottotitoli: '#ffd54a',
  archivio: '#c9a26b',
  cartella: '#ffb23d',
  altro: '#a19db0',
};
