// Il tavolo luminoso: la foto al centro, la rotella zooma dove c'è il mouse, si trascina, doppio clic per il
// 100%. Le frecce passano alla foto dopo (già caricata: si apre al volo). Proprietà: i dati dello scatto e
// l'istogramma. S fa partire la presentazione.
import { chiedi } from '../ponte';
import { h, clamp } from '../util';
import { attesa, conferma, errore, ic, memoria, schermoIntero, type Contesto, type Tasto, type Vista } from './comune';

const native = /\.(jpe?g|jpe|jfif|png|apng|webp|gif|bmp|dib|ico|svg|avif)$/i;
const giaViste = new Map<string, HTMLImageElement>();

export async function monta(c: Contesto): Promise<Vista> {
  const f = c.scheda;
  const img = h<HTMLImageElement>('img.i-foto', { alt: '', draggable: false });
  img.crossOrigin = 'anonymous';
  const zoomEl = h('div.i-zoom');
  const mappa = h('div.i-mappa', null, h('img'), h('i'));
  const tavolo = h('div.i-tavolo', null, img, zoomEl, mappa);
  let fondo = memoria.leggi<string>('fondo') ?? 'scuro';
  tavolo.dataset.fondo = fondo;
  const caricamento = attesa('Sviluppo la foto…');
  c.palco.append(tavolo);
  const tardi = setTimeout(() => tavolo.append(caricamento), 250);

  let url = f.url;
  if (!native.test(f.nome)) {
    try { url = (await chiedi<{ url: string }>('immagine')).url; }
    catch (e) { clearTimeout(tardi); tavolo.replaceChildren(errore((e as Error).message)); return { tasti: [], smonta() {} }; }
  }
  if (!c.viva()) return { tasti: [], smonta() {} };
  img.src = url;
  try { await img.decode(); }
  catch {
    // il file dice di essere una cosa ma è un'altra: ci prova Windows
    try { img.src = (await chiedi<{ url: string }>('immagine')).url; await img.decode(); }
    catch { clearTimeout(tardi); tavolo.replaceChildren(errore('Questa immagine non si apre.')); return { tasti: [], smonta() {} }; }
  }
  clearTimeout(tardi);
  caricamento.remove();
  if (!c.viva()) return { tasti: [], smonta() {} };
  (mappa.firstElementChild as HTMLImageElement).src = img.src;
  const W = img.naturalWidth || 800, H = img.naturalHeight || 600;

  // ——— zoom e spostamento ———
  const v = { z: 1, x: 0, y: 0, r: 0 };
  const lato = () => (v.r % 180 === 0 ? [W, H] : [H, W]);
  function adatta() {
    const [w, hh] = lato();
    const p = tavolo.getBoundingClientRect();
    const piccola = Math.max(w, hh) < 300;
    return Math.min((p.width - 40) / w, (p.height - 40) / hh, piccola ? 4 : 1);
  }
  let zAdatta = adatta();
  function applica(anima = false) {
    img.classList.toggle('anima', anima);
    img.style.width = `${W}px`;
    img.style.height = `${H}px`;
    img.style.transform = `translate(-50%, -50%) translate(${v.x}px, ${v.y}px) scale(${v.z}) rotate(${v.r}deg)`;
    img.classList.toggle('pixel', v.z >= 3);
    tavolo.classList.toggle('zoomata', v.z > zAdatta * 1.01);
    zoomEl.textContent = `${Math.round(v.z * 100)}%`;
    // la mappa in basso: dove sei dentro la foto
    const p = tavolo.getBoundingClientRect();
    const [w, hh] = lato();
    const fuori = w * v.z > p.width || hh * v.z > p.height;
    mappa.classList.toggle('su', fuori);
    if (fuori) {
      const m = mappa.getBoundingClientRect();
      const s = Math.min(m.width / w, m.height / hh);
      const r = mappa.lastElementChild as HTMLElement;
      const vw = Math.min(w, p.width / v.z), vh = Math.min(hh, p.height / v.z);
      const cx = w / 2 - v.x / v.z, cy = hh / 2 - v.y / v.z;
      Object.assign(r.style, { width: `${vw * s}px`, height: `${vh * s}px`, left: `${(cx - vw / 2) * s + (m.width - w * s) / 2}px`, top: `${(cy - vh / 2) * s + (m.height - hh * s) / 2}px` });
      (mappa.firstElementChild as HTMLElement).style.transform = `rotate(${v.r}deg)`;
    }
    c.info();
  }
  function limita() {
    const p = tavolo.getBoundingClientRect();
    const [w, hh] = lato();
    const mx = Math.max(0, (w * v.z - p.width) / 2 + 40), my = Math.max(0, (hh * v.z - p.height) / 2 + 40);
    v.x = clamp(v.x, -mx, mx);
    v.y = clamp(v.y, -my, my);
  }
  function zoomA(z: number, cx?: number, cy?: number, anima = false) {
    const p = tavolo.getBoundingClientRect();
    const px = (cx ?? p.left + p.width / 2) - (p.left + p.width / 2);
    const py = (cy ?? p.top + p.height / 2) - (p.top + p.height / 2);
    const nz = clamp(z, Math.min(zAdatta, 0.05), 32);
    // il punto sotto il mouse resta sotto il mouse
    v.x = px - ((px - v.x) * nz) / v.z;
    v.y = py - ((py - v.y) * nz) / v.z;
    v.z = nz;
    if (nz <= zAdatta * 1.001) { v.x = 0; v.y = 0; }
    limita();
    applica(anima);
  }
  function inQuadro(anima = false) { zAdatta = adatta(); v.z = zAdatta; v.x = 0; v.y = 0; applica(anima); }
  inQuadro();
  requestAnimationFrame(() => img.classList.add('su'));
  const ro = new ResizeObserver(() => { const eraAdatta = v.z <= zAdatta * 1.001; zAdatta = adatta(); if (eraAdatta) inQuadro(); else { limita(); applica(); } });
  ro.observe(tavolo);

  tavolo.addEventListener('wheel', (e) => { e.preventDefault(); zoomA(v.z * Math.pow(1.0018, -e.deltaY), e.clientX, e.clientY); }, { passive: false });
  let presa: { x: number; y: number; vx: number; vy: number } | null = null;
  tavolo.addEventListener('pointerdown', (e) => {
    if (e.button !== 0 || v.z <= zAdatta * 1.01) return;
    presa = { x: e.clientX, y: e.clientY, vx: v.x, vy: v.y };
    tavolo.setPointerCapture(e.pointerId);
    tavolo.classList.add('presa');
  });
  tavolo.addEventListener('pointermove', (e) => {
    if (!presa) return;
    v.x = presa.vx + e.clientX - presa.x;
    v.y = presa.vy + e.clientY - presa.y;
    limita();
    applica();
  });
  tavolo.addEventListener('pointerup', () => { presa = null; tavolo.classList.remove('presa'); });
  tavolo.addEventListener('dblclick', (e) => {
    if (v.z > zAdatta * 1.01) inQuadro(true);
    else zoomA(Math.max(1, zAdatta * 2), e.clientX, e.clientY, true);
  });

  // ——— le foto accanto, già pronte ———
  const { file, indice } = c.elenco();
  for (const d of [1, -1]) {
    const p = file[(indice + d + file.length) % file.length];
    if (!p || p === f.percorso || !native.test(p) || giaViste.has(p)) continue;
    void chiedi<string>('url', { percorso: p }).then((u) => { const i = new Image(); i.src = u; giaViste.set(p, i); if (giaViste.size > 12) giaViste.delete(giaViste.keys().next().value!); }).catch(() => {});
  }

  // ——— presentazione ———
  let diapo: number | undefined;
  function presentazione() {
    if (diapo) { clearInterval(diapo); diapo = undefined; c.hud('Presentazione ferma', ic.diapo); return; }
    if (c.fratelli() < 2) return;
    memoria.scrivi('diapo', true);
    if (!document.fullscreenElement) schermoIntero();
    c.hud('Presentazione · una foto ogni 4 s', ic.diapo);
    diapo = window.setInterval(() => c.vai(1), 4000);
  }
  // la presentazione continua sulla foto dopo
  if (memoria.leggi<boolean>('diapo') && document.fullscreenElement && c.fratelli() > 1) diapo = window.setInterval(() => c.vai(1), 4000);
  else memoria.scrivi('diapo', null);

  async function copia() {
    try {
      const cv = h<HTMLCanvasElement>('canvas', { width: W, height: H });
      cv.getContext('2d')!.drawImage(img, 0, 0);
      const blob = await new Promise<Blob>((ok, no) => cv.toBlob((b) => (b ? ok(b) : no(new Error('niente'))), 'image/png'));
      await navigator.clipboard.write([new ClipboardItem({ 'image/png': blob })]);
      c.hud('Immagine copiata', ic.copia);
    } catch { c.hud('Non si copia', ic.copia); }
  }
  async function sfondo() {
    if (!await conferma('Sfondo del desktop?', `«${f.nome}» diventa lo sfondo del desktop di Windows.`, 'Metti come sfondo')) return;
    try { await chiedi('sfondo'); c.hud('Sfondo cambiato', ic.sfondo); } catch (e) { c.hud((e as Error).message, ic.sfondo); }
  }
  function cambiaFondo() {
    const giro = ['scuro', 'scacchi', 'chiaro'];
    fondo = giro[(giro.indexOf(fondo) + 1) % giro.length];
    tavolo.dataset.fondo = fondo;
    memoria.scrivi('fondo', fondo);
    c.hud({ scuro: 'Sfondo scuro', scacchi: 'Scacchi (trasparenza)', chiaro: 'Sfondo chiaro' }[fondo]!, ic.sole);
  }
  function ruota(d: number) {
    v.r = (v.r + d + 360) % 360;
    zAdatta = adatta();
    v.z = zAdatta; v.x = 0; v.y = 0;
    applica(true);
  }

  const tasti: Tasto[] = [
    { k: ['arrowright', ' '], etichetta: '→', cosa: 'Foto dopo', fai: () => c.vai(1) },
    { k: ['arrowleft', 'backspace'], etichetta: '←', cosa: 'Foto prima', fai: () => c.vai(-1) },
    { k: ['+', '=', 'ctrl+='], etichetta: '+', cosa: 'Più vicino', fai: () => zoomA(v.z * 1.4, undefined, undefined, true) },
    { k: ['-', 'ctrl+-'], etichetta: '−', cosa: 'Più lontano', fai: () => zoomA(v.z / 1.4, undefined, undefined, true) },
    { k: ['0'], etichetta: '0', cosa: 'Tutta nello schermo', fai: () => inQuadro(true) },
    { k: ['1'], etichetta: '1', cosa: 'Al 100% (un pixel = un pixel)', fai: () => zoomA(1, undefined, undefined, true) },
    { k: ['r'], etichetta: 'R', cosa: 'Gira a destra (solo per guardarla)', fai: () => ruota(90) },
    { k: ['shift+r'], etichetta: 'Maiusc R', cosa: 'Gira a sinistra', fai: () => ruota(-90) },
    { k: ['f'], etichetta: 'F', cosa: 'Schermo intero', fai: schermoIntero },
    { k: ['s', 'f5'], etichetta: 'S', cosa: 'Presentazione', fai: presentazione },
    { k: ['b'], etichetta: 'B', cosa: 'Sfondo: scuro, scacchi, chiaro', fai: cambiaFondo },
    { k: ['ctrl+c'], etichetta: 'Ctrl C', cosa: 'Copia l\'immagine', fai: () => void copia() },
    { k: ['w'], etichetta: 'W', cosa: 'Metti come sfondo del desktop', fai: () => void sfondo() },
    { k: ['home'], etichetta: 'Inizio', cosa: 'La prima della cartella', fai: () => c.vaiA(0) },
    { k: ['end'], etichetta: 'Fine', cosa: 'L\'ultima della cartella', fai: () => c.vaiA(c.elenco().file.length - 1) },
  ];

  return {
    tasti,
    barraSopra: true,
    frecceLaterali: true,
    info: () => `${W}×${H}${W * H > 1.5e6 ? ` · ${(W * H / 1e6).toLocaleString('it-IT', { maximumFractionDigits: 1 })} MP` : ''} · ${Math.round(v.z * 100)}%`,
    scheda: async () => {
      const exif = await chiedi<{ nome: string; valore: string }[]>('exif').catch(() => []);
      const isto = istogramma(img);
      const nodi: Node[] = [h('dl', null,
        h('div', null, h('dt', null, 'Misure'), h('dd', null, `${W} × ${H} pixel`)),
        ...exif.map((x) => h('div', null, h('dt', null, x.nome), h('dd', { title: x.valore }, x.valore))))];
      if (isto) nodi.unshift(isto);
      return nodi;
    },
    smonta() {
      ro.disconnect();
      clearInterval(diapo);
      if (!diapo) memoria.scrivi('diapo', null);
    },
  };
}

/** L'istogramma: rosso, verde e blu sovrapposti, come sul retro della macchina fotografica. */
function istogramma(img: HTMLImageElement): HTMLCanvasElement | null {
  try {
    const lato = 256;
    const s = Math.min(1, lato / Math.max(img.naturalWidth, img.naturalHeight));
    const w = Math.max(1, Math.round(img.naturalWidth * s)), hh = Math.max(1, Math.round(img.naturalHeight * s));
    const cv = document.createElement('canvas');
    cv.width = w; cv.height = hh;
    const g = cv.getContext('2d', { willReadFrequently: true })!;
    g.drawImage(img, 0, 0, w, hh);
    const d = g.getImageData(0, 0, w, hh).data;
    const r = new Array(256).fill(0), gr = new Array(256).fill(0), b = new Array(256).fill(0);
    for (let i = 0; i < d.length; i += 4) { if (d[i + 3] < 10) continue; r[d[i]]++; gr[d[i + 1]]++; b[d[i + 2]]++; }
    const max = Math.max(...r.slice(2, 254), ...gr.slice(2, 254), ...b.slice(2, 254), 1);
    const out = document.createElement('canvas');
    out.className = 'i-isto';
    out.width = 512; out.height = 150;
    const o = out.getContext('2d')!;
    o.globalCompositeOperation = 'lighter';
    for (const [dati, col] of [[r, '#ff3b5c'], [gr, '#3dff8a'], [b, '#3d8bff']] as const) {
      o.fillStyle = col;
      o.globalAlpha = 0.75;
      o.beginPath();
      o.moveTo(0, 150);
      for (let x = 0; x < 256; x++) o.lineTo(x * 2, 150 - Math.min(1, dati[x] / max) * 146);
      o.lineTo(512, 150);
      o.fill();
    }
    return out;
  } catch { return null; }
}
