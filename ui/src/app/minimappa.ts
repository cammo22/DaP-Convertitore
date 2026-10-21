// La minimappa, come quella di VS Code: il documento in miniatura, una riga ogni due pixel, un pixel per carattere.
// Qui ci sono solo i conti (niente DOM), così si provano da soli (scripts/prova-anteprima.mjs) e restano veloci anche
// su un registro da centomila righe: il disegno vero sta in `minimappa-vista.ts`.

/** Il testo da mettere in miniatura, riga per riga (le righe a video: quelle lunghissime sono già spezzate). */
export interface Fonte {
  n: number;
  /** I primi `max` caratteri della riga `i` (basta l'inizio: la minimappa è stretta). */
  riga(i: number, max: number): string;
  /** Quanto è lunga la riga `i`, se si sa senza leggerla (se no si legge). */
  lung?(i: number): number;
}

export interface Mappa {
  /** Larghezza in pixel (= colonne di caratteri × `scala`). */
  w: number;
  /** Altezza del disegno: `n × 2 × scala` se ci sta, se no tutta l'altezza disponibile (e le righe si fondono). */
  h: number;
  /** Quanti pixel fa una riga del documento (2 × scala, o meno se le righe si fondono). */
  pxRiga: number;
  /** L'inchiostro di ogni pixel, 0 = vuoto, 255 = pieno. */
  px: Uint8Array;
  /** Quante righe ci sono nella miniatura (con le righe lunghe a capo sono più di quelle del documento). */
  righe: number;
  /** Con le righe a capo: la prima riga della miniatura di ogni riga del documento. Se no, null. */
  prima: Int32Array | null;
}

/** Quante righe della miniatura al massimo per una riga sola: un JSON da sei milioni di caratteri non ne fa sessantamila. */
export const MAX_A_CAPO = 60;

/** Il documento con le righe lunghe spezzate in `colonne` caratteri, come le farebbe l'a capo. */
function avvolgi(fonte: Fonte, colonne: number): { fonte: Fonte; prima: Int32Array } {
  const prima = new Int32Array(fonte.n + 1);
  const tetto = MAX_A_CAPO * colonne;
  for (let i = 0; i < fonte.n; i++) {
    const l = Math.min(tetto, fonte.lung ? fonte.lung(i) : fonte.riga(i, tetto).length);
    prima[i + 1] = prima[i] + Math.max(1, Math.ceil(l / colonne));
  }
  const tot = prima[fonte.n];
  const src = new Int32Array(tot);
  for (let i = 0; i < fonte.n; i++) for (let k = prima[i]; k < prima[i + 1]; k++) src[k] = i;
  let ultima = -1, testo = '';
  return {
    prima,
    fonte: {
      n: tot,
      riga(k: number) {
        const i = src[k];
        if (i !== ultima) { ultima = i; testo = fonte.riga(i, tetto); }
        const o = (k - prima[i]) * colonne;
        return testo.slice(o, o + colonne);
      },
    },
  };
}

/** Una tabulazione vale quattro colonne, come nell'anteprima. */
const TAB = 4;

/**
 * Il documento in miniatura. `scala` è quanti pixel fa un carattere (1, o 2 sugli schermi ad alta densità): la riga
 * alta 2×scala ha il pixel di sopra pieno e quello di sotto a metà, così le righe si staccano come in VS Code.
 */
export function calcolaMappa(fonte: Fonte, larghezza: number, altezza: number, scala = 1, aCapo = false): Mappa {
  const colonne = Math.max(1, Math.floor(larghezza / scala));
  if (aCapo) {
    const a = avvolgi(fonte, colonne);
    return { ...calcolaMappa(a.fonte, larghezza, altezza, scala, false), prima: a.prima };
  }
  const w = colonne * scala;
  const naturale = fonte.n * 2 * scala;
  const h = Math.max(1, Math.min(altezza, naturale));
  const pxRiga = h / fonte.n;
  const px = new Uint8Array(w * h);
  if (pxRiga >= 2 * scala - 1e-9) {
    // ogni riga ha il suo posto
    const alta = 2 * scala;
    const riga = new Uint8Array(colonne);
    for (let i = 0; i < fonte.n; i++) {
      if (!inchiostra(fonte.riga(i, colonne * TAB), colonne, riga)) continue;
      const y0 = i * alta;
      for (let c = 0; c < colonne; c++) {
        if (!riga[c]) continue;
        for (let dy = 0; dy < alta && y0 + dy < h; dy++) {
          const v = dy < scala ? riga[c] : riga[c] >> 1;
          for (let dx = 0; dx < scala; dx++) px[(y0 + dy) * w + c * scala + dx] = v;
        }
      }
    }
    return { w, h, pxRiga: alta, px, righe: fonte.n, prima: null };
  }
  // troppe righe per l'altezza: ogni pixel verticale fa la media delle righe che ci cadono
  const somma = new Float32Array(colonne * h);
  const conta = new Uint32Array(h);
  const riga = new Uint8Array(colonne);
  for (let i = 0; i < fonte.n; i++) {
    const y = Math.min(h - 1, Math.floor(i * pxRiga));
    conta[y]++;
    if (!inchiostra(fonte.riga(i, colonne * TAB), colonne, riga)) continue;
    const o = y * colonne;
    for (let c = 0; c < colonne; c++) if (riga[c]) somma[o + c] += riga[c];
  }
  for (let y = 0; y < h; y++) {
    if (!conta[y]) continue;
    for (let c = 0; c < colonne; c++) {
      // la media schiarirebbe tutto: si alza un po' (come VS Code che a distanza sfuma ma si legge ancora)
      const v = Math.min(255, Math.round((somma[y * colonne + c] / conta[y]) * 1.7));
      if (!v) continue;
      for (let dx = 0; dx < scala; dx++) px[y * w + c * scala + dx] = v;
    }
  }
  return { w, h, pxRiga, px, righe: fonte.n, prima: null };
}

/** Riempie `fuori` con l'inchiostro di una riga (colonna per colonna). Dice se c'è qualcosa di scritto. */
function inchiostra(testo: string, colonne: number, fuori: Uint8Array): boolean {
  fuori.fill(0);
  let col = 0, scritto = false;
  for (let k = 0; k < testo.length && col < colonne; k++) {
    const c = testo.charCodeAt(k);
    if (c === 9) { col += TAB - (col % TAB); continue; }
    if (c === 32 || c === 160) { col++; continue; }
    // le lettere pesano più della punteggiatura: la riga ha una sua grana
    fuori[col++] = (c >= 48 && c <= 57) || (c >= 65 && c <= 90) ? 235 : c >= 97 && c <= 122 ? 205 : c > 127 ? 205 : 140;
    scritto = true;
  }
  return scritto;
}

/** Dove (che pixel) cadono le righe `righe` del documento: 0 = niente, 1 = trovata, 2 = quella scelta. */
export function segniMappa(n: number, h: number, righe: ArrayLike<number>, scelta: number, prima: Int32Array | null = null): Uint8Array {
  const out = new Uint8Array(h);
  if (!n) return out;
  const f = h / n;
  // con le righe a capo il segno sta sulla prima riga della miniatura di quella riga del documento
  const dove = (r: number) => Math.min(h - 1, Math.floor((prima ? prima[Math.min(r, prima.length - 1)] : r) * f));
  for (let k = 0; k < righe.length; k++) out[dove(righe[k])] = 1;
  if (scelta >= 0) out[dove(scelta)] = 2;
  return out;
}

/** Da dove si è scesi a dove sta il riquadro: frazioni del documento (0…1) → pixel, con un'altezza minima che si afferra. */
export function riquadro(h: number, primo: number, quanto: number, minimo = 14): { y: number; alto: number } {
  const alto = Math.min(h, Math.max(minimo, quanto * h));
  const y = Math.min(h - alto, Math.max(0, primo * h));
  return { y, alto };
}

/** Un clic a `y` pixel: il centro della parte da mostrare, come frazione del documento (0…1). */
export function frazioneDaY(h: number, y: number): number {
  return h > 0 ? Math.min(1, Math.max(0, y / h)) : 0;
}

/** Da una riga del documento (anche con la virgola: 3,5 = a metà della quarta) a dove sta nella miniatura, 0…1. */
export function frazioneDaRiga(riga: number, n: number, righe: number, prima: Int32Array | null): number {
  if (n <= 0 || righe <= 0) return 0;
  if (!prima) return Math.min(1, Math.max(0, riga / n));
  const i = Math.min(n - 1, Math.max(0, Math.floor(riga)));
  const a = prima[i], b = prima[i + 1];
  return Math.min(1, Math.max(0, (a + (riga >= n ? 1 : riga - i) * (b - a)) / righe));
}

/** Il contrario: da un punto della miniatura (0…1) alla riga del documento (con la virgola). */
export function rigaDaFrazione(f: number, n: number, righe: number, prima: Int32Array | null): number {
  if (n <= 0) return 0;
  if (!prima) return Math.min(n, Math.max(0, f * n));
  const t = Math.min(righe, Math.max(0, f * righe));
  let lo = 0, hi = n - 1;
  while (lo < hi) {
    const m = (lo + hi + 1) >> 1;
    if (prima[m] <= t) lo = m; else hi = m - 1;
  }
  const largo = prima[lo + 1] - prima[lo];
  return lo + (largo > 0 ? Math.min(1, (t - prima[lo]) / largo) : 0);
}
