// La scrivania: i fogli uno sotto l'altro con la loro ombra, le miniature a sinistra, il numero di pagina sul
// display a sette segmenti. Le pagine le disegna Windows alla risoluzione vera dello schermo, solo quelle che
// vedi; zoomando si ridisegnano più nitide.
import { chiedi } from '../ponte';
import { h, clamp } from '../util';
import { attesa, conferma, errore, ic, memoria, type Contesto, type Tasto, type Vista } from './comune';

export interface DatiPdf { radice: string; pagine: { w: number; h: number }[] }

export async function monta(c: Contesto): Promise<Vista> {
  c.palco.append(attesa('Apro il PDF…'));
  let d: DatiPdf;
  try { d = await chiedi<DatiPdf>('pdf'); }
  catch (e) { c.palco.replaceChildren(errore((e as Error).message)); return { tasti: [], smonta() {} }; }
  if (!c.viva()) return { tasti: [], smonta() {} };
  return scrivania(c, d, undefined, true);
}

/** `modificabile`: il file è il PDF vero (si possono girare le pagine e salvare); il PDF impaginato da Word no. */
export function scrivania(c: Contesto, d: DatiPdf, prima?: HTMLElement, modificabile = false): Vista {
  const chiave = `pdf:${c.scheda.percorso}`;
  const salvato = memoria.leggi<{ z: number; p: number }>(chiave);
  let zoom = salvato?.z ?? 0; // 0 = larghezza adatta
  const fogli: HTMLElement[] = [];
  const miniature: HTMLElement[] = [];
  const pagine = h('div.p-pagine');
  const colonna = h('div.p-colonna', null, pagine);
  const lato = h('aside.p-lato');
  const numero = h('span.p-numero', null, '1');
  const banco = h('div.p-banco', null, lato, colonna);
  const sopra = h('div.p-comandi', null,
    h('button.l-icona', { title: 'Miniature (T)', html: ic.elenco, onclick: () => giraLato() }),
    h('div.p-display', null, h('small', null, 'PAG'), numero, h('i', null, '/'), h('span', null, String(d.pagine.length))),
    h('button.l-icona', { title: 'Più lontano (−)', html: ic.zoomMeno, onclick: () => zooma(-1) }),
    h('span.p-zoom'),
    h('button.l-icona', { title: 'Più vicino (+)', html: ic.zoomPiu, onclick: () => zooma(1) }),
    h('button.l-icona', { title: 'Larghezza della finestra (W)', html: ic.adatta, onclick: () => { zoom = 0; disponi(); } }),
    modificabile ? h('span.p-sep') : null,
    modificabile ? h('button.l-icona', { title: 'Gira la pagina a sinistra e salva (Maiusc R)', html: ic.ruotaSx, onclick: () => void ruotaPagina(-90, false) }) : null,
    modificabile ? h('button.l-icona', { title: 'Gira la pagina a destra e salva (R)', html: ic.ruotaDx, onclick: () => void ruotaPagina(90, false) }) : null,
    modificabile ? h('button.l-tasto.piccolo', { title: 'Gira tutte le pagine e salva (Ctrl R)', onclick: () => void ruotaPagina(90, true) }, 'Gira tutte') : null,
    prima ?? null);
  c.palco.replaceChildren(h('div.p-scrivania', null, sopra, banco));
  if (memoria.leggi<boolean>('pdf-lato') !== false && d.pagine.length > 1) banco.classList.add('con-lato');

  d.pagine.forEach((p, i) => {
    const foglio = h('div.p-foglio', { dataset: { i: String(i) } }, h('span.p-num', null, String(i + 1)));
    fogli.push(foglio);
    pagine.append(foglio);
    const mini = h('button.p-mini', { onclick: () => vaiA(i) }, h('div', { style: { aspectRatio: `${p.w} / ${p.h}` } }), h('span', null, String(i + 1)));
    miniature.push(mini);
    lato.append(mini);
  });

  // ——— la misura dei fogli ———
  const larghezzaAdatta = () => Math.min(colonna.clientWidth - 48, 1100);
  function scala() {
    const maxW = Math.max(...d.pagine.map((p) => p.w));
    return zoom === 0 ? larghezzaAdatta() / maxW : zoom;
  }
  function disponi() {
    const s = scala();
    const prima = paginaVista();
    d.pagine.forEach((p, i) => {
      fogli[i].style.width = `${Math.round(p.w * s)}px`;
      fogli[i].style.height = `${Math.round(p.h * s)}px`;
    });
    (sopra.querySelector('.p-zoom') as HTMLElement).textContent = `${Math.round(s * 100)}%`;
    // le pagine già disegnate si ridisegnano più nitide se servono più pixel
    for (const f of fogli) if (f.dataset.w && +f.dataset.w < f.clientWidth * devicePixelRatio * 0.9) delete f.dataset.w;
    controlla();
    vaiA(prima, false);
  }
  function zooma(d2: number) {
    const s = scala();
    zoom = clamp(s * (d2 > 0 ? 1.2 : 1 / 1.2), 0.2, 5);
    disponi();
    c.hud(`${Math.round(zoom * 100)}%`, d2 > 0 ? ic.zoomPiu : ic.zoomMeno);
  }

  // ——— disegna solo quello che si vede (più una pagina sopra e una sotto) ———
  const osserva = new IntersectionObserver((voci) => {
    for (const v of voci) if (v.isIntersecting) disegna(v.target as HTMLElement);
  }, { root: colonna, rootMargin: '600px 0px' });
  fogli.forEach((f) => osserva.observe(f));
  const osservaMini = new IntersectionObserver((voci) => {
    for (const v of voci) if (v.isIntersecting) {
      const m = v.target as HTMLElement;
      const i = miniature.indexOf(m);
      if (m.dataset.fatta) continue;
      m.dataset.fatta = '1';
      const img = h('img', { src: `${d.radice}${i}?w=${Math.round(140 * devicePixelRatio)}`, loading: 'lazy', alt: '' });
      m.firstElementChild!.append(img);
    }
  }, { root: lato, rootMargin: '300px 0px' });
  miniature.forEach((m) => osservaMini.observe(m));

  function disegna(f: HTMLElement) {
    const w = Math.round(Math.min(4000, f.clientWidth * devicePixelRatio) / 100) * 100 || 800;
    if (f.dataset.w && +f.dataset.w >= w) return;
    f.dataset.w = String(w);
    const i = +f.dataset.i!;
    const img = new Image();
    img.alt = '';
    img.src = `${d.radice}${i}?w=${w}`;
    img.decode().then(() => {
      f.querySelector('img')?.remove();
      f.prepend(img);
      f.classList.add('pronto');
    }).catch(() => { delete f.dataset.w; });
  }
  function controlla() {
    const r = colonna.getBoundingClientRect();
    for (const f of fogli) {
      const b = f.getBoundingClientRect();
      if (b.bottom > r.top - 600 && b.top < r.bottom + 600) disegna(f);
    }
  }

  // ——— a che pagina sei ———
  function paginaVista() {
    const r = colonna.getBoundingClientRect();
    const mezzo = r.top + r.height * 0.35;
    let migliore = 0;
    for (let i = 0; i < fogli.length; i++) { if (fogli[i].getBoundingClientRect().top <= mezzo) migliore = i; else break; }
    return migliore;
  }
  let attuale = -1;
  function segna() {
    const p = paginaVista();
    if (p === attuale) return;
    attuale = p;
    numero.textContent = String(p + 1);
    miniature.forEach((m, i) => m.classList.toggle('su', i === p));
    if (banco.classList.contains('con-lato')) {
      const m = miniature[p];
      const lr = lato.getBoundingClientRect(), mr = m.getBoundingClientRect();
      if (mr.top < lr.top || mr.bottom > lr.bottom) m.scrollIntoView({ block: 'nearest' });
    }
    memoria.scrivi(chiave, { z: zoom, p });
    c.info();
  }
  colonna.addEventListener('scroll', segna, { passive: true });
  function vaiA(i: number, liscio = true) {
    i = clamp(i, 0, fogli.length - 1);
    const su = colonna.scrollTop + fogli[i].getBoundingClientRect().top - colonna.getBoundingClientRect().top - 24;
    colonna.scrollTo({ top: su, behavior: liscio ? 'smooth' : 'auto' });
  }
  function giraLato() {
    banco.classList.toggle('con-lato');
    memoria.scrivi('pdf-lato', banco.classList.contains('con-lato'));
    setTimeout(disponi, 0);
  }
  colonna.addEventListener('wheel', (e) => { if (e.ctrlKey) { e.preventDefault(); zooma(e.deltaY < 0 ? 1 : -1); } }, { passive: false });

  /** Gira una pagina (o tutte) nel PDF vero: si chiede prima, perché si riscrive il file. */
  async function ruotaPagina(gradi: number, tutte: boolean) {
    if (!modificabile) return;
    const p = paginaVista();
    const verso = gradi > 0 ? 'a destra' : 'a sinistra';
    const cosa = tutte ? 'tutte le pagine' : `la pagina ${p + 1}`;
    if (!await conferma('Girare il PDF?', `Giro ${cosa} ${verso} e salvo nel file. Si può rigirare quando vuoi.`, 'Gira e salva')) return;
    c.stato('Salvo il PDF girato…');
    try {
      await chiedi('ruotaPdf', { pagina: tutte ? -1 : p, gradi: ((gradi % 360) + 360) % 360 });
      memoria.scrivi(chiave, { z: zoom, p });
      c.stato(null);
      c.ricarica();
      c.hud('PDF girato e salvato', ic.salva);
    } catch (e) { c.stato(null); c.hud((e as Error).message, ic.ruotaDx); }
  }

  const ro = new ResizeObserver(() => { if (zoom === 0) disponi(); });
  ro.observe(colonna);
  disponi();
  requestAnimationFrame(() => { if (salvato?.p) vaiA(salvato.p, false); segna(); });

  const tasti: Tasto[] = [
    { k: ['arrowright', 'pagedown', 'shift+ '], etichetta: '→ Pag ↓', cosa: 'Pagina dopo', fai: () => vaiA(paginaVista() + 1) },
    { k: ['arrowleft', 'pageup'], etichetta: '← Pag ↑', cosa: 'Pagina prima', fai: () => vaiA(paginaVista() - 1) },
    { k: ['home'], etichetta: 'Inizio', cosa: 'Prima pagina', fai: () => vaiA(0) },
    { k: ['end'], etichetta: 'Fine', cosa: 'Ultima pagina', fai: () => vaiA(fogli.length - 1) },
    { k: ['+', '=', 'ctrl+='], etichetta: '+', cosa: 'Più vicino (anche Ctrl + rotella)', fai: () => zooma(1) },
    { k: ['-', 'ctrl+-'], etichetta: '−', cosa: 'Più lontano', fai: () => zooma(-1) },
    { k: ['w', '0'], etichetta: 'W', cosa: 'Larga quanto la finestra', fai: () => { zoom = 0; disponi(); } },
    { k: ['t'], etichetta: 'T', cosa: 'Miniature', fai: giraLato },
    { k: ['n'], etichetta: 'N', cosa: 'Il file dopo', fai: () => c.vai(1) },
    ...(modificabile ? [
      { k: ['r'], etichetta: 'R', cosa: 'Gira la pagina a destra e salva', fai: () => void ruotaPagina(90, false) },
      { k: ['shift+r'], etichetta: 'Maiusc R', cosa: 'Gira la pagina a sinistra e salva', fai: () => void ruotaPagina(-90, false) },
      { k: ['ctrl+r'], etichetta: 'Ctrl R', cosa: 'Gira tutte le pagine a destra e salva', fai: () => void ruotaPagina(90, true) },
    ] : []),
  ];

  return {
    tasti,
    info: () => `${d.pagine.length} ${d.pagine.length === 1 ? 'pagina' : 'pagine'}`,
    smonta() { osserva.disconnect(); osservaMini.disconnect(); ro.disconnect(); },
  };
}
