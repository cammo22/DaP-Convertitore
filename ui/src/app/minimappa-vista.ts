// La minimappa disegnata: una colonna stretta a destra del testo, col riquadro di quello che si vede (si trascina), i
// risultati della ricerca come segni dorati, il clic che porta lì. I conti stanno in `minimappa.ts`.
import './minimappa.css';
import { h } from '../util';
import { calcolaMappa, segniMappa, riquadro, frazioneDaY, frazioneDaRiga, rigaDaFrazione, type Fonte, type Mappa } from './minimappa';

export interface VistaMappa {
  el: HTMLElement;
  /** Il documento da mettere in miniatura (null: niente). */
  carica(fonte: Fonte | null, aCapo?: boolean): void;
  /** I risultati della ricerca: le righe (del documento) dove stanno, e quella scelta (-1: nessuna). */
  segni(righe: ArrayLike<number>, scelta: number): void;
  /** Quello che si vede adesso: da dove (0…1 del documento) e quanto (0…1). */
  vista(primo: number, quanto: number): void;
  /** Da una riga del documento (con la virgola) a un punto della miniatura (0…1): le righe lunghe a capo ne occupano più d'una. */
  daRiga(riga: number): number;
  /** Il contrario: da un punto della miniatura (0…1) alla riga del documento. */
  aRiga(frazione: number): number;
  /** Ridisegna alla misura attuale (si chiama quando la colonna cambia altezza). */
  misura(): void;
}

/**
 * @param vai chiamata quando si clicca o si trascina: il centro della parte da mostrare, 0…1 del documento.
 */
export function creaMinimappa(vai: (centro: number) => void): VistaMappa {
  const tela = h<HTMLCanvasElement>('canvas.mm-tela');
  const el = h('div.mm', { 'aria-hidden': 'true', title: 'Clic o trascina per scorrere' }, tela);
  const ctx = tela.getContext('2d')!;
  const base = document.createElement('canvas');
  const bctx = base.getContext('2d')!;

  let fonte: Fonte | null = null;
  let aCapo = false;
  let mappa: Mappa | null = null;
  let fatta: { fonte: Fonte | null; w: number; h: number } | null = null;
  let righeTrovate: ArrayLike<number> = [];
  let scelta = -1;
  let segniPx: Uint8Array = new Uint8Array(0);
  let primo = 0, quanto = 1;
  let scala = 1;

  function ridisegna() {
    ctx.clearRect(0, 0, tela.width, tela.height);
    if (!mappa) { el.classList.add('vuota'); return; }
    el.classList.remove('vuota');
    ctx.drawImage(base, 0, 0);
    // i segni della ricerca, a tutta larghezza (come il righello a destra di VS Code)
    const alto = Math.max(2, scala * 2);
    for (let y = 0; y < segniPx.length; y++) {
      if (!segniPx[y]) continue;
      ctx.fillStyle = segniPx[y] === 2 ? '#ff7a1a' : 'rgba(255, 213, 74, 0.85)';
      ctx.fillRect(0, y - (alto >> 1) + 1, mappa.w, alto + (segniPx[y] === 2 ? scala : 0));
    }
    const r = riquadro(mappa.h, primo, quanto, 14 * scala);
    ctx.fillStyle = 'rgba(138, 44, 255, 0.30)';
    ctx.fillRect(0, r.y, mappa.w, r.alto);
    ctx.fillStyle = 'rgba(255, 213, 74, 0.55)';
    ctx.fillRect(0, r.y, mappa.w, scala);
    ctx.fillRect(0, r.y + r.alto - scala, mappa.w, scala);
  }

  function misura() {
    scala = Math.min(2, Math.max(1, Math.round(window.devicePixelRatio || 1)));
    const cw = Math.round(el.clientWidth * scala), ch = Math.round(el.clientHeight * scala);
    if (!fonte || cw < 8 || ch < 8) { mappa = null; ridisegna(); return; }
    if (tela.width !== cw || tela.height !== ch) { tela.width = cw; tela.height = ch; fatta = null; }
    if (!fatta || fatta.fonte !== fonte || fatta.w !== cw || fatta.h !== ch) {
      mappa = calcolaMappa(fonte, cw, ch, scala, aCapo);
      base.width = mappa.w;
      base.height = mappa.h;
      const img = bctx.createImageData(mappa.w, mappa.h);
      const d = img.data;
      for (let i = 0, n = mappa.px.length; i < n; i++) {
        const v = mappa.px[i];
        if (!v) continue;
        d[i * 4] = 214; d[i * 4 + 1] = 210; d[i * 4 + 2] = 226;
        d[i * 4 + 3] = Math.round(v * 0.72);
      }
      bctx.putImageData(img, 0, 0);
      fatta = { fonte, w: cw, h: ch };
      segniPx = segniMappa(mappa.righe, mappa.h, righeTrovate, scelta, mappa.prima);
    }
    ridisegna();
  }

  // ——— clic e trascinamento: il riquadro segue il mouse, anche fuori dalla colonna ———
  el.addEventListener('pointerdown', (e) => {
    if (!mappa || e.button !== 0) return;
    e.preventDefault();
    el.setPointerCapture(e.pointerId);
    el.classList.add('tira');
    const vaiA = (ev: PointerEvent) => {
      const y = (ev.clientY - el.getBoundingClientRect().top) * scala;
      vai(frazioneDaY(mappa!.h, y));
    };
    vaiA(e);
    const lascia = () => {
      el.classList.remove('tira');
      el.removeEventListener('pointermove', vaiA);
      el.removeEventListener('pointerup', lascia);
      el.removeEventListener('pointercancel', lascia);
    };
    el.addEventListener('pointermove', vaiA);
    el.addEventListener('pointerup', lascia);
    el.addEventListener('pointercancel', lascia);
  });

  return {
    el,
    carica(f, capo = false) { fonte = f; aCapo = capo; fatta = null; righeTrovate = []; scelta = -1; segniPx = new Uint8Array(0); primo = 0; if (!f) mappa = null; misura(); },
    segni(righe, s) {
      righeTrovate = righe;
      scelta = s;
      segniPx = mappa && fonte ? segniMappa(mappa.righe, mappa.h, righe, s, mappa.prima) : new Uint8Array(0);
      ridisegna();
    },
    vista(p, q) { primo = p; quanto = q; ridisegna(); },
    daRiga: (r) => (fonte ? frazioneDaRiga(r, fonte.n, mappa?.righe ?? fonte.n, mappa?.prima ?? null) : 0),
    aRiga: (f) => (fonte ? rigaDaFrazione(f, fonte.n, mappa?.righe ?? fonte.n, mappa?.prima ?? null) : 0),
    misura,
  };
}
