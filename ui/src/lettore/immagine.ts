// Il tavolo luminoso: la foto al centro, la rotella zooma dove c'è il mouse, si trascina, doppio clic per il
// 100%. Le frecce passano alla foto dopo (già caricata: si apre al volo). Proprietà: i dati dello scatto e
// l'istogramma. S fa partire la presentazione.
import { chiedi } from '../ponte';
import { h, clamp } from '../util';
import { attesa, conferma, errore, giri, ic, memoria, pillola, schermoIntero, type Contesto, type Tasto, type Vista } from './comune';
import { creaRitocco, type RitoccoConTasti } from './ritocco';

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
  const v = { z: 1, x: 0, y: 0, r: 0, f: false };
  const mod = pillola(tavolo);
  // il ritocco: disegnare e scrivere sopra la foto (ritocco.ts); i segni stanno su un foglio che segue lo zoom
  const rit = creaRitocco(c, tavolo, img, W, H, { cambiato: () => modifica(), salva: () => void salvaRitocco(), copia: () => void copia() }) as RitoccoConTasti;
  tavolo.append(rit.foglio);
  const lato = () => (v.r % 180 === 0 ? [W, H] : [H, W]);
  function adatta() {
    const [w, hh] = lato();
    const p = tavolo.getBoundingClientRect();
    const piccola = Math.max(w, hh) < 300;
    // col ritocco acceso la barra degli strumenti sta in basso: la foto le lascia il posto
    const barra = rit.attivo() ? 170 : 0;
    return Math.min((p.width - 40) / w, (p.height - 40 - barra) / hh, piccola ? 4 : 1);
  }
  let zAdatta = adatta();
  function applica(anima = false) {
    img.classList.toggle('anima', anima);
    img.style.width = `${W}px`;
    img.style.height = `${H}px`;
    img.style.transform = `translate(-50%, -50%) translate(${v.x}px, ${v.y}px) scale(${v.z}) rotate(${v.r}deg) scaleX(${v.f ? -1 : 1})`;
    rit.segui(img.style.transform);
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
    modifica();
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
    if ((e.button !== 0 && e.button !== 1) || v.z <= zAdatta * 1.01) return;
    if (e.button === 1) e.preventDefault();
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
    if (rit.attivo()) return;
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
      // coi ritocchi si copia la foto ritoccata (negli appunti soltanto: il file non cambia)
      const ritoccata = rit.haSegni();
      const cv = ritoccata ? rit.compone() : h<HTMLCanvasElement>('canvas', { width: W, height: H });
      if (!ritoccata) cv.getContext('2d')!.drawImage(img, 0, 0);
      const blob = await new Promise<Blob>((ok, no) => cv.toBlob((b) => (b ? ok(b) : no(new Error('niente'))), 'image/png'));
      await navigator.clipboard.write([new ClipboardItem({ 'image/png': blob })]);
      c.hud(ritoccata ? 'Foto ritoccata copiata · il file non è cambiato' : 'Immagine copiata', ic.copia);
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
  /** Girare e ritoccare insieme no: i segni sono sulla foto dritta. */
  function ritoccoInCorso() {
    if (!rit.attivo() && !rit.haSegni()) return false;
    c.hud('Chiudi il ritocco prima di girare la foto', ic.matita);
    return true;
  }
  function ruota(d: number) {
    if (ritoccoInCorso()) return;
    v.r = (v.r + d + 360) % 360;
    zAdatta = adatta();
    v.z = zAdatta; v.x = 0; v.y = 0;
    applica(true);
  }
  /** Specchia da sinistra a destra: dopo una rotazione il verso della rotazione si inverte. */
  function specchia() {
    if (ritoccoInCorso()) return;
    v.f = !v.f;
    v.r = (360 - v.r) % 360;
    zAdatta = adatta();
    v.z = zAdatta; v.x = 0; v.y = 0;
    applica(true);
  }
  function annulla() { v.r = 0; v.f = false; zAdatta = adatta(); v.z = zAdatta; v.x = 0; v.y = 0; applica(true); }

  /** La pillola «girata, non ancora salvata»: salvare riscrive il file vero (JPG e TIFF cambiano solo l'orientamento). */
  function modifica() {
    // i ritocchi hanno la precedenza: salvare scrive sul file i segni (e la rotazione non c'è, si gira o si ritocca)
    if (rit.haSegni()) {
      mod.mostra('Foto ritoccata · non ancora salvata',
        { t: 'Salva le modifiche', oro: true, fai: () => void salvaRitocco() },
        { t: 'Copia', fai: () => void copia() },
        { t: 'Butta i segni', fai: () => rit.scarta() });
      return;
    }
    const gira = v.r !== 0 || v.f;
    if (!gira) { mod.nascondi(); return; }
    const cosa = [v.f ? 'specchiata' : null, v.r ? `girata di ${v.r}°` : null].filter(Boolean).join(' e ');
    if (f.nota) { mod.mostra(`Solo da guardare: ${f.nota}`, { t: 'Annulla', fai: annulla }); return; }
    mod.mostra(`Foto ${cosa} · non ancora salvata`, { t: 'Salva nel file', oro: true, fai: () => void salva() }, { t: 'Annulla', fai: annulla });
  }
  let salvando = false;
  async function salva() {
    if (salvando || (v.r === 0 && !v.f) || f.nota) return;
    salvando = true;
    c.stato('Salvo la rotazione nel file…');
    try {
      await chiedi('ruotaFoto', { gradi: giri(v.r), specchio: v.f });
      giaViste.delete(f.percorso);
      c.stato(null);
      c.ricarica();
      c.hud('Salvata nel file', ic.salva);
    } catch (e) { c.stato(null); c.hud((e as Error).message, ic.ruotaDx); }
    finally { salvando = false; }
  }

  /** Il ritocco sostituisce la foto nel file (JPG, PNG, WEBP; gli altri formati escono accanto, in PNG). */
  async function salvaRitocco() {
    if (salvando || !rit.haSegni()) return;
    const est = f.estensione.toLowerCase();
    const mime = /^.(jpe?g|jpe|jfif)$/.test(est) ? 'image/jpeg' : est === '.webp' ? 'image/webp' : 'image/png';
    const sostituisce = (mime === 'image/jpeg' && /^.(jpe?g|jpe|jfif)$/.test(est)) || (mime === 'image/png' && est === '.png') || (mime === 'image/webp' && est === '.webp');
    const testo = sostituisce
      ? `«${f.nome}» viene sostituito con la foto ritoccata${mime === 'image/jpeg' ? ' (i dati dello scatto restano)' : ''}. L'originale non si ripesca: se ti serve, usa Copia.`
      : `Il formato ${est.replace('.', '').toUpperCase()} non si riscrive: la foto ritoccata esce accanto, in PNG, e «${f.nome}» resta com'è.`;
    if (!await conferma(sostituisce ? 'Salvare le modifiche?' : 'Salvare il ritocco accanto?', testo, sostituisce ? 'Sostituisci il file' : 'Salva accanto', sostituisce)) return;
    salvando = true;
    c.stato('Salvo la foto ritoccata…');
    try {
      const cv = rit.compone();
      const blob = await new Promise<Blob>((ok, no) => cv.toBlob((b) => (b ? ok(b) : no(new Error('La foto non si è composta.'))), sostituisce ? mime : 'image/png', 0.97));
      const dati = await new Promise<string>((ok, no) => {
        const r = new FileReader();
        r.onload = () => ok(String(r.result).split(',')[1] ?? '');
        r.onerror = () => no(new Error('La foto non si è composta.'));
        r.readAsDataURL(blob);
      });
      const r = await chiedi<{ percorso: string; accanto: boolean; nome: string }>('salvaRitocco', { dati });
      c.stato(null);
      if (r.accanto) { c.hud(`Salvata accanto: ${r.nome}`, ic.salva); return; }
      giaViste.delete(f.percorso);
      rit.scarta();
      rit.attiva(false);
      c.ricarica();
      c.hud('Modifiche salvate nel file', ic.salva);
    } catch (e) { c.stato(null); c.hud((e as Error).message, ic.matita); }
    finally { salvando = false; }
  }

  function ritocca() {
    if (rit.attivo()) { rit.attiva(false); inQuadro(true); return; }
    if (v.r !== 0 || v.f) { c.hud('Salva o annulla la rotazione prima di ritoccare', ic.matita); return; }
    rit.attiva(true);
    inQuadro(true);
    c.hud('Ritocco: disegna e scrivi sopra la foto', ic.matita);
  }

  const tasti: Tasto[] = [
    { k: ['d'], etichetta: 'D', cosa: 'Ritocca: disegna e scrivi sopra la foto', fai: ritocca },
    { k: ['ctrl+z'], etichetta: 'Ctrl Z', cosa: 'Ritocco: annulla l\'ultimo segno', fai: () => rit.annulla() },
    { k: ['ctrl+y', 'ctrl+shift+z'], etichetta: 'Ctrl Y', cosa: 'Ritocco: rifai', fai: () => rit.rifai() },
    { k: ['arrowright', ' '], etichetta: '→', cosa: 'Foto dopo', fai: () => c.vai(1) },
    { k: ['arrowleft', 'backspace'], etichetta: '←', cosa: 'Foto prima', fai: () => c.vai(-1) },
    { k: ['+', '=', 'ctrl+='], etichetta: '+', cosa: 'Più vicino', fai: () => zoomA(v.z * 1.4, undefined, undefined, true) },
    { k: ['-', 'ctrl+-'], etichetta: '−', cosa: 'Più lontano', fai: () => zoomA(v.z / 1.4, undefined, undefined, true) },
    { k: ['0'], etichetta: '0', cosa: 'Tutta nello schermo', fai: () => inQuadro(true) },
    { k: ['1'], etichetta: '1', cosa: 'Al 100% (un pixel = un pixel)', fai: () => zoomA(1, undefined, undefined, true) },
    { k: ['r'], etichetta: 'R', cosa: 'Gira a destra', fai: () => ruota(90) },
    { k: ['shift+r'], etichetta: 'Maiusc R', cosa: 'Gira a sinistra', fai: () => ruota(-90) },
    { k: ['m'], etichetta: 'M', cosa: 'Specchia (sinistra-destra)', fai: specchia },
    { k: ['ctrl+s'], etichetta: 'Ctrl S', cosa: 'Salva nel file (la rotazione o i ritocchi)', fai: () => void (rit.haSegni() ? salvaRitocco() : salva()) },
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
    azioni: [
      { titolo: 'Gira a sinistra (Maiusc R)', icona: ic.ruotaSx, fai: () => ruota(-90) },
      { titolo: 'Gira a destra (R)', icona: ic.ruotaDx, fai: () => ruota(90) },
      { titolo: 'Specchia (M)', icona: ic.specchio, fai: specchia },
      { titolo: 'Ritocca: disegna e scrivi sopra (D)', icona: ic.matita, fai: ritocca },
    ],
    sporco: () => rit.haSegni(),
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
      rit.smonta();
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
