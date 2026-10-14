// Il ritocco della foto: si disegna e si scrive sopra, con penna, evidenziatore, frecce, riquadri, ovali e testo.
// I segni stanno su due fogli trasparenti grandi quanto la foto (uno per quello che c'è già, uno per il segno in corso)
// e seguono lo zoom della foto. Niente si scrive nel file finché non premi «Salva le modifiche»; «Copia» mette la foto
// ritoccata negli appunti senza salvarla.
import { h, clamp } from '../util';
import { ic, memoria, type Contesto } from './comune';

type Strumento = 'penna' | 'evid' | 'freccia' | 'rett' | 'ovale' | 'testo';
type Punto = [number, number];
type Segno =
  | { t: 'penna' | 'evid'; c: string; w: number; p: Punto[] }
  | { t: 'freccia' | 'rett' | 'ovale'; c: string; w: number; a: Punto; b: Punto }
  | { t: 'testo'; c: string; dim: number; x: number; y: number; s: string; contorno: boolean };

export interface Ritocco {
  /** Il foglio dei segni, da mettere dentro il tavolo (segue la foto). */
  foglio: HTMLElement;
  attivo(): boolean;
  haSegni(): boolean;
  /** Apre o chiude la barra degli strumenti. */
  attiva(si: boolean): void;
  /** Il foglio prende la stessa posizione e lo stesso zoom della foto. */
  segui(trasformazione: string): void;
  /** La foto con i segni sopra, a misura piena (per salvare e per copiare). */
  compone(): HTMLCanvasElement;
  /** Butta tutti i segni. */
  scarta(): void;
  smonta(): void;
}

interface Aiuti {
  /** I segni sono cambiati (per la pillola «non ancora salvate»). */
  cambiato(): void;
  salva(): void;
  copia(): void;
}

const colori = ['#ff3b30', '#ffd54a', '#ff3df2', '#35e8ff', '#5dffb4', '#3d6bff', '#ffffff', '#111111'];

/** Il segno disegnato su un foglio (lo stesso codice serve al foglio che si vede e alla foto che si salva). */
export function disegnaSegno(g: CanvasRenderingContext2D, o: Segno) {
  g.save();
  g.lineCap = 'round';
  g.lineJoin = 'round';
  g.strokeStyle = o.c;
  g.fillStyle = o.c;
  if (o.t === 'penna' || o.t === 'evid') {
    g.lineWidth = o.t === 'evid' ? o.w * 3.2 : o.w;
    g.globalAlpha = o.t === 'evid' ? 0.4 : 1;
    const p = o.p;
    if (p.length === 1) { g.beginPath(); g.arc(p[0][0], p[0][1], g.lineWidth / 2, 0, Math.PI * 2); g.fill(); }
    else {
      // una sola linea (così il segno trasparente non si scurisce dove si sovrappone), addolcita fra i punti
      g.beginPath();
      g.moveTo(p[0][0], p[0][1]);
      for (let i = 1; i < p.length - 1; i++) g.quadraticCurveTo(p[i][0], p[i][1], (p[i][0] + p[i + 1][0]) / 2, (p[i][1] + p[i + 1][1]) / 2);
      g.lineTo(p[p.length - 1][0], p[p.length - 1][1]);
      g.stroke();
    }
  } else if (o.t === 'freccia') {
    g.lineWidth = o.w;
    const [x0, y0] = o.a, [x1, y1] = o.b;
    const ang = Math.atan2(y1 - y0, x1 - x0);
    const testa = Math.max(o.w * 4.5, 14);
    g.beginPath();
    g.moveTo(x0, y0);
    g.lineTo(x1 - Math.cos(ang) * testa * 0.6, y1 - Math.sin(ang) * testa * 0.6);
    g.stroke();
    g.beginPath();
    g.moveTo(x1, y1);
    g.lineTo(x1 - testa * Math.cos(ang - 0.45), y1 - testa * Math.sin(ang - 0.45));
    g.lineTo(x1 - testa * Math.cos(ang + 0.45), y1 - testa * Math.sin(ang + 0.45));
    g.closePath();
    g.fill();
  } else if (o.t === 'rett') {
    g.lineWidth = o.w;
    g.strokeRect(Math.min(o.a[0], o.b[0]), Math.min(o.a[1], o.b[1]), Math.abs(o.b[0] - o.a[0]), Math.abs(o.b[1] - o.a[1]));
  } else if (o.t === 'ovale') {
    g.lineWidth = o.w;
    g.beginPath();
    g.ellipse((o.a[0] + o.b[0]) / 2, (o.a[1] + o.b[1]) / 2, Math.abs(o.b[0] - o.a[0]) / 2, Math.abs(o.b[1] - o.a[1]) / 2, 0, 0, Math.PI * 2);
    g.stroke();
  } else if (o.t === 'testo') {
    g.font =`700 ${o.dim}px Rajdhani, "Segoe UI", sans-serif`;
    g.textBaseline = 'top';
    const righe = o.s.split('\n');
    righe.forEach((riga, i) => {
      const y = o.y + i * o.dim * 1.18;
      if (o.contorno) {
        // un contorno del colore opposto: si legge su qualsiasi foto
        g.lineWidth = Math.max(2, o.dim / 7);
        g.strokeStyle = luminosita(o.c) > 0.55 ? 'rgba(0,0,0,0.8)' : 'rgba(255,255,255,0.85)';
        g.strokeText(riga, o.x, y);
      }
      g.fillText(riga, o.x, y);
    });
  }
  g.restore();
}

function luminosita(c: string) {
  const n = parseInt(c.replace('#', ''), 16);
  return (0.2126 * ((n >> 16) & 255) + 0.7152 * ((n >> 8) & 255) + 0.0722 * (n & 255)) / 255;
}

export function creaRitocco(c: Contesto, tavolo: HTMLElement, img: HTMLImageElement, W: number, H: number, aiuti: Aiuti): Ritocco {
  const base = h<HTMLCanvasElement>('canvas.r-foglio', { width: W, height: H });
  const viva = h<HTMLCanvasElement>('canvas.r-foglio.viva', { width: W, height: H });
  const foglio = h('div.i-disegno', null, base, viva);
  foglio.style.width = `${W}px`;
  foglio.style.height = `${H}px`;
  const gb = base.getContext('2d')!;
  const gv = viva.getContext('2d')!;

  const segni: Segno[] = [];
  const rifare: Segno[] = [];
  let corrente: Segno | null = null;
  let strumento = memoria.leggi<Strumento>('rit-strumento') ?? 'penna';
  let colore = memoria.leggi<string>('rit-colore') ?? colori[0];
  let spessore = memoria.leggi<number>('rit-spessore') ?? 6;
  let contorno = memoria.leggi<boolean>('rit-contorno') ?? true;
  let barra: HTMLElement | null = null;
  let casella: HTMLTextAreaElement | null = null;

  /** Un'unità di spessore vale 1/1000 del lato lungo: lo stesso segno su una foto da 800 px e su una da 6000. */
  const unita = () => Math.max(W, H) / 1000;
  const w = () => Math.max(1, spessore * unita());

  const ridisegnaTutto = () => {
    gb.clearRect(0, 0, W, H);
    for (const o of segni) disegnaSegno(gb, o);
  };
  const ridisegnaViva = () => {
    gv.clearRect(0, 0, W, H);
    if (corrente) disegnaSegno(gv, corrente);
  };

  const punto = (e: PointerEvent): Punto => {
    const r = viva.getBoundingClientRect();
    return [clamp(((e.clientX - r.left) * W) / r.width, 0, W), clamp(((e.clientY - r.top) * H) / r.height, 0, H)];
  };

  const aggiungi = (o: Segno) => {
    segni.push(o);
    rifare.length = 0;
    disegnaSegno(gb, o);
    aggiornaBarra();
    aiuti.cambiato();
  };

  function annulla() {
    chiudiTesto(true);
    const o = segni.pop();
    if (!o) return;
    rifare.push(o);
    ridisegnaTutto();
    aggiornaBarra();
    aiuti.cambiato();
  }
  function rifai() {
    const o = rifare.pop();
    if (!o) return;
    segni.push(o);
    disegnaSegno(gb, o);
    aggiornaBarra();
    aiuti.cambiato();
  }
  function scarta() {
    chiudiTesto(true);
    if (!segni.length && !rifare.length) return;
    segni.length = 0;
    rifare.length = 0;
    corrente = null;
    ridisegnaTutto();
    ridisegnaViva();
    aggiornaBarra();
    aiuti.cambiato();
  }

  // ——— il segno col mouse ———
  viva.addEventListener('pointerdown', (e) => {
    if (e.button !== 0) return; // gli altri tasti spostano la foto
    e.stopPropagation();
    e.preventDefault();
    const p = punto(e);
    if (strumento === 'testo') { apriTesto(e.clientX, e.clientY, p); return; }
    chiudiTesto(true);
    try { viva.setPointerCapture(e.pointerId); } catch { /* senza cattura (eventi finti) funziona uguale */ }
    corrente = strumento === 'penna' || strumento === 'evid'
      ? { t: strumento, c: colore, w: w(), p: [p] }
      : { t: strumento, c: colore, w: w(), a: p, b: p };
    ridisegnaViva();
  });
  viva.addEventListener('pointermove', (e) => {
    if (!corrente) return;
    const p = punto(e);
    if (corrente.t === 'penna' || corrente.t === 'evid') {
      const ultimo = corrente.p[corrente.p.length - 1];
      if (Math.hypot(p[0] - ultimo[0], p[1] - ultimo[1]) < unita() * 0.8) return; // niente punti inutili
      corrente.p.push(p);
    } else if (corrente.t === 'freccia' || corrente.t === 'rett' || corrente.t === 'ovale') {
      corrente.b = e.shiftKey && corrente.t !== 'freccia' ? quadrato(corrente.a, p) : p;
    }
    ridisegnaViva();
  });
  const finisci = () => {
    if (!corrente) return;
    const o: Segno = corrente;
    corrente = null;
    gv.clearRect(0, 0, W, H);
    // un clic senza trascinare non fa una forma vuota
    if ((o.t === 'freccia' || o.t === 'rett' || o.t === 'ovale') && Math.hypot(o.b[0] - o.a[0], o.b[1] - o.a[1]) < unita() * 4) return;
    aggiungi(o);
  };
  viva.addEventListener('pointerup', finisci);
  viva.addEventListener('pointercancel', () => { corrente = null; ridisegnaViva(); });

  const quadrato = (a: Punto, b: Punto): Punto => {
    const d = Math.max(Math.abs(b[0] - a[0]), Math.abs(b[1] - a[1]));
    return [a[0] + Math.sign(b[0] - a[0] || 1) * d, a[1] + Math.sign(b[1] - a[1] || 1) * d];
  };

  // ——— il testo: una casella dove si clicca, Invio la fissa sulla foto ———
  function apriTesto(cx: number, cy: number, p: Punto) {
    chiudiTesto(true);
    const dim = Math.max(14, spessore * unita() * 4.5);
    const r = viva.getBoundingClientRect();
    const scala = r.width / W;
    const t = tavolo.getBoundingClientRect();
    casella = h<HTMLTextAreaElement>('textarea.r-testo', { rows: 1, placeholder: 'Scrivi…', spellcheck: false });
    Object.assign(casella.style, { left: `${cx - t.left}px`, top: `${cy - t.top}px`, color: colore, fontSize: `${Math.max(12, dim * scala)}px` });
    const adatta = () => { casella!.style.height = 'auto'; casella!.style.height = `${casella!.scrollHeight}px`; casella!.style.width = `${Math.max(120, casella!.scrollWidth)}px`; };
    casella.addEventListener('input', adatta);
    casella.addEventListener('keydown', (e) => {
      e.stopPropagation();
      if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); chiudiTesto(true); }
      else if (e.key === 'Escape') { e.preventDefault(); chiudiTesto(false); }
    });
    casella.addEventListener('blur', () => chiudiTesto(true));
    casella.dataset.x = String(p[0]);
    casella.dataset.y = String(p[1]);
    casella.dataset.dim = String(dim);
    tavolo.append(casella);
    casella.focus();
  }
  function chiudiTesto(fissa: boolean) {
    const k = casella;
    if (!k) return;
    casella = null;
    const testo = k.value.replace(/\s+$/, '');
    k.remove();
    if (fissa && testo) aggiungi({ t: 'testo', c: colore, dim: +k.dataset.dim!, x: +k.dataset.x!, y: +k.dataset.y!, s: testo, contorno });
  }

  // ——— la barra degli strumenti ———
  const sv = (corpo: string) => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">${corpo}</svg>`;
  const icone: Record<Strumento, string> = {
    penna: sv('<path d="M4 20l4-1 11-11-3-3L5 16z"/><path d="M14 6l3 3"/>'),
    evid: sv('<path d="M9 18l-4 2 1-4 9-9 3 3z"/><path d="M4 21h8" stroke-width="3" opacity=".6"/>'),
    freccia: sv('<path d="M5 19L18 6M18 6h-8M18 6v8"/>'),
    rett: sv('<rect x="4" y="6" width="16" height="12" rx="1.5"/>'),
    ovale: sv('<ellipse cx="12" cy="12" rx="8.5" ry="6"/>'),
    testo: sv('<path d="M5 6V4h14v2M12 4v16M9 20h6"/>'),
  };
  const nomi: Record<Strumento, string> = { penna: 'Penna', evid: 'Evidenziatore', freccia: 'Freccia', rett: 'Riquadro', ovale: 'Ovale', testo: 'Testo' };
  const bAnnulla = h<HTMLButtonElement>('button.r-b', { title: 'Annulla l\'ultimo segno (Ctrl Z)', html: ic.annulla, onclick: annulla });
  const bRifai = h<HTMLButtonElement>('button.r-b', { title: 'Rifai (Ctrl Y)', html: ic.rifai, onclick: rifai });
  const bSvuota = h<HTMLButtonElement>('button.r-b', { title: 'Butta tutti i segni', html: ic.cestino, onclick: scarta });
  const bCopia = h<HTMLButtonElement>('button.l-tasto.piccolo', { title: 'Copia la foto ritoccata negli appunti, senza salvarla (Ctrl C)', onclick: () => aiuti.copia() }, h('span', { html: ic.copia }), 'Copia');
  const bSalva = h<HTMLButtonElement>('button.l-tasto.piccolo.oro', { title: 'Scrive i segni nel file: l\'originale viene sostituito (Ctrl S)', onclick: () => aiuti.salva() }, h('span', { html: ic.salva }), 'Salva le modifiche');

  function aggiornaBarra() {
    if (!barra) return;
    bAnnulla.disabled = !segni.length;
    bRifai.disabled = !rifare.length;
    bSvuota.disabled = !segni.length;
    bSalva.disabled = !segni.length;
    bCopia.disabled = !segni.length;
    barra.querySelectorAll<HTMLElement>('[data-s]').forEach((b) => b.classList.toggle('su', b.dataset.s === strumento));
    barra.querySelectorAll<HTMLElement>('[data-c]').forEach((b) => b.classList.toggle('su', b.dataset.c === colore));
    barra.querySelector('.r-contorno')?.classList.toggle('su', contorno);
    viva.style.cursor = strumento === 'testo' ? 'text' : 'crosshair';
  }

  function apriBarra() {
    const strumenti = h('div.r-gruppo', null, ...(Object.keys(icone) as Strumento[]).map((k) =>
      h('button.r-b', { dataset: { s: k }, title: nomi[k], html: icone[k], onclick: () => { chiudiTesto(true); strumento = k; memoria.scrivi('rit-strumento', k); aggiornaBarra(); } })));
    const tavolozza = h('div.r-gruppo.colori', null,
      ...colori.map((k) => h('button.r-colore', { dataset: { c: k }, title: k, style: { background: k }, onclick: () => { colore = k; memoria.scrivi('rit-colore', k); if (casella) casella.style.color = k; aggiornaBarra(); } })),
      h('input.r-scegli', { type: 'color', value: colore, title: 'Un altro colore', oninput: (e: Event) => { colore = (e.target as HTMLInputElement).value; memoria.scrivi('rit-colore', colore); aggiornaBarra(); } }));
    const spess = h<HTMLInputElement>('input.r-spessore', { type: 'range', min: 1, max: 40, value: spessore, title: 'Spessore del segno e grandezza della scritta' });
    spess.addEventListener('input', () => { spessore = +spess.value; memoria.scrivi('rit-spessore', spessore); });
    const bContorno = h('button.r-b.r-contorno', { title: 'Contorno alla scritta: si legge su qualsiasi foto', onclick: () => { contorno = !contorno; memoria.scrivi('rit-contorno', contorno); aggiornaBarra(); } }, 'A');
    barra = h('div.r-barra', null,
      strumenti, tavolozza,
      h('div.r-gruppo', null, spess, bContorno),
      h('div.r-gruppo', null, bAnnulla, bRifai, bSvuota),
      h('span.r-spazio'),
      bCopia, bSalva,
      h('button.r-b', { title: 'Chiudi il ritocco (D)', html: ic.chiudi, onclick: () => rit.attiva(false) }));
    barra.addEventListener('pointerdown', (e) => e.stopPropagation());
    barra.addEventListener('wheel', (e) => e.stopPropagation());
    c.palco.append(barra);
    aggiornaBarra();
  }

  let acceso = false;
  const rit: Ritocco = {
    foglio,
    attivo: () => acceso,
    haSegni: () => segni.length > 0,
    attiva(si) {
      if (si === acceso) return;
      acceso = si;
      chiudiTesto(true);
      foglio.classList.toggle('attivo', si);
      tavolo.classList.toggle('ritocco', si);
      if (si) apriBarra();
      else { barra?.remove(); barra = null; }
      aiuti.cambiato();
    },
    segui(t) { foglio.style.transform = t; },
    compone() {
      const out = h<HTMLCanvasElement>('canvas', { width: W, height: H });
      const g = out.getContext('2d')!;
      g.drawImage(img, 0, 0, W, H);
      for (const o of segni) disegnaSegno(g, o);
      return out;
    },
    scarta,
    smonta() { chiudiTesto(false); barra?.remove(); },
  };
  // tasti del ritocco (Ctrl Z / Ctrl Y) li inoltra la pagina della foto: qui sono esposti per lei
  Object.assign(rit, { annulla, rifai });
  return rit;
}

/** Quello che la pagina della foto può chiedere in più al ritocco (tasti rapidi). */
export type RitoccoConTasti = Ritocco & { annulla(): void; rifai(): void };
