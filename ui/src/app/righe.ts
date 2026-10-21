// Il testo dell'anteprima diviso in righe da disegnare, e la ricerca. Niente DOM qui: solo conti, così si provano da
// soli (scripts/prova-anteprima.mjs) e restano veloci anche su un registro da 6 MB e centomila righe.

/** Una riga più lunga di così si spezza in più righe a video (il numero lo porta solo la prima): una riga da
 *  sei milioni di caratteri (un JSON minificato) in un solo elemento bloccherebbe la pagina. */
export const PEZZO = 2000;

/** Oltre questi trovati si smette di contare: il contatore dice «100.000+». */
export const MAX_TROVATI = 100_000;

export interface Doc {
  /** Il testo con gli a capo tutti «\n» (CRLF e CR soli diventano LF). */
  testo: string;
  /** Il file aveva le righe in CRLF. */
  crlf: boolean;
  /** Dove comincia ogni riga del file. */
  inizi: Int32Array;
  /** Le righe a video: di che riga sono, da dove a dove (le righe lunghe ne fanno più d'una). */
  vRiga: Int32Array;
  vDa: Int32Array;
  vA: Int32Array;
  /** La prima riga a video di ogni riga del file. */
  prima: Int32Array;
  /** La riga a video più lunga, in caratteri. */
  lungMax: number;
}

export function indicizza(grezzo: string): Doc {
  const crlf = grezzo.includes('\r\n');
  const testo = grezzo.includes('\r') ? grezzo.replace(/\r\n?/g, '\n') : grezzo;
  // prima si contano, poi si riempiono gli array
  let n = 1;
  for (let i = testo.indexOf('\n'); i >= 0; i = testo.indexOf('\n', i + 1)) n++;
  const inizi = new Int32Array(n);
  for (let k = 1, i = testo.indexOf('\n'); i >= 0; i = testo.indexOf('\n', i + 1)) inizi[k++] = i + 1;
  const fine = (r: number) => (r + 1 < n ? inizi[r + 1] - 1 : testo.length);
  const prima = new Int32Array(n);
  let v = 0;
  for (let r = 0; r < n; r++) { prima[r] = v; v += Math.max(1, Math.ceil((fine(r) - inizi[r]) / PEZZO)); }
  const vRiga = new Int32Array(v), vDa = new Int32Array(v), vA = new Int32Array(v);
  let lungMax = 0;
  for (let r = 0, k = 0; r < n; r++) {
    const a = fine(r);
    let d = inizi[r];
    do {
      const e = Math.min(a, d + PEZZO);
      vRiga[k] = r; vDa[k] = d; vA[k] = e; k++;
      if (e - d > lungMax) lungMax = e - d;
      d = e;
    } while (d < a);
  }
  return { testo, crlf, inizi, vRiga, vDa, vA, prima, lungMax };
}

/** La riga del file che contiene il carattere `pos` (ricerca binaria). */
export function rigaDi(doc: Doc, pos: number): number {
  let lo = 0, hi = doc.inizi.length - 1;
  while (lo < hi) {
    const m = (lo + hi + 1) >> 1;
    if (doc.inizi[m] <= pos) lo = m; else hi = m - 1;
  }
  return lo;
}

/** La riga a video che contiene il carattere `pos`. */
export function rigaVideoDi(doc: Doc, pos: number): number {
  const r = rigaDi(doc, pos);
  return doc.prima[r] + Math.floor((pos - doc.inizi[r]) / PEZZO);
}

export interface Trovati {
  /** Dove comincia ogni trovato, in ordine. */
  pos: Int32Array;
  /** Quanto è lungo (è sempre la stessa cosa cercata). */
  lung: number;
  /** Ce n'erano di più di MAX_TROVATI. */
  troppi: boolean;
}

const nessuno: Trovati = { pos: new Int32Array(0), lung: 0, troppi: false };

/**
 * Tutti i punti dove c'è `q`, senza sovrapposizioni (come Ctrl+F in VS Code). Senza «Aa» non conta maiuscole e
 * minuscole: con una RegExp «i» e non con toLowerCase, che per certe lettere (İ) cambia la lunghezza del testo e
 * sposterebbe le evidenziature.
 */
export function cerca(doc: Doc, q: string, maiuscole = false): Trovati {
  if (!q || q.includes('\n')) return nessuno;
  const out: number[] = [];
  let troppi = false;
  if (maiuscole) {
    for (let i = doc.testo.indexOf(q); i >= 0; i = doc.testo.indexOf(q, i + q.length)) {
      if (out.length >= MAX_TROVATI) { troppi = true; break; }
      out.push(i);
    }
  } else {
    const re = new RegExp(q.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'giu');
    for (let m = re.exec(doc.testo); m; m = re.exec(doc.testo)) {
      if (out.length >= MAX_TROVATI) { troppi = true; break; }
      out.push(m.index);
    }
  }
  return { pos: Int32Array.from(out), lung: q.length, troppi };
}

/** Il primo trovato che finisce dopo `da` (quelli prima non toccano il pezzo che comincia lì). */
export function primoDopo(t: Trovati, da: number): number {
  let lo = 0, hi = t.pos.length;
  while (lo < hi) {
    const m = (lo + hi) >> 1;
    if (t.pos[m] + t.lung <= da) lo = m + 1; else hi = m;
  }
  return lo;
}

/** Il trovato da cui partire: il primo che sta da `pos` in poi (Invio parte da dove stai guardando). */
export function vicino(t: Trovati, pos: number): number {
  const k = primoDopo(t, pos);
  return k < t.pos.length ? k : 0;
}

const esc = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

/** Un pezzo di testo in HTML, coi trovati evidenziati (quello scelto con la classe «su»). */
export function pezzoHtml(doc: Doc, da: number, a: number, t: Trovati, scelto: number): string {
  if (!t.pos.length) return esc(doc.testo.slice(da, a));
  let out = '', p = da;
  for (let k = primoDopo(t, da); k < t.pos.length && t.pos[k] < a; k++) {
    const s = Math.max(t.pos[k], da), e = Math.min(t.pos[k] + t.lung, a);
    if (s > p) out += esc(doc.testo.slice(p, s));
    out += `<mark${k === scelto ? ' class="su"' : ''}>${esc(doc.testo.slice(s, e))}</mark>`;
    p = e;
  }
  return out + esc(doc.testo.slice(p, a));
}
