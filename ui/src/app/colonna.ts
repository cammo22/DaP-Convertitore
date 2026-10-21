// La colonna a destra per i file che non sono testo: a sinistra la figura grande, a destra l'indice cliccabile come la
// minimappa di VS Code ma fatto di figurine: le pagine di un PDF, i fotogrammi di un video, le onde di un brano.
// Per tutto il resto (foto, documenti, cartelle…) c'è una figurina sola: nessun file resta senza anteprima.
import { chiedi } from '../ponte';
import type { InfoFile } from '../ponte';
import { h, durata } from '../util';
import { iconaCategoria, coloreCategoria } from '../icone';

interface Indice { tipo: 'pagine' | 'tempo' | 'singola'; voci: number; totale: number; punti: number[] }

export interface Colonna {
  el: HTMLElement;
  mostra(f: InfoFile): void;
  /** Il tipo di indice, quando è arrivato (per il piede del pannello). */
  descrizione(): string;
  /** Si ferma tutto (il file è cambiato o il pannello si è chiuso). */
  ferma(): void;
}

const LATO_FIGURINA = 160;
const LATO_GRANDE = 720;

export function creaColonna(apri: () => void, pronto: () => void = () => {}): Colonna {
  const img = h<HTMLImageElement>('img.cl-img', { alt: '', draggable: 'false', title: 'Apri nel lettore', onclick: () => apri() });
  const segnaposto = h('div.cl-segnaposto');
  const etichetta = h('div.cl-etichetta');
  const grande = h('div.cl-grande', null, h('div.cl-figura', null, segnaposto, img), etichetta);
  const strisce = h('div.cl-strisce', { tabindex: '0', role: 'listbox', 'aria-label': 'Indice dell\'anteprima' });
  const el = h('div.ap-colonna', null, grande, strisce);

  let gettone = 0;                              // cambia a ogni file: le risposte in ritardo si scartano
  let file: InfoFile | null = null;
  let indice: Indice | null = null;
  let scelta = -1;
  let tipoDescritto = '';
  const url = new Map<string, string>();        // le figure già arrivate (id:n:lato)
  const voci: HTMLElement[] = [];
  const attesa: number[] = [];                  // le figurine da chiedere, una alla volta
  let inCorso = false;
  let osserva: IntersectionObserver | null = null;

  const chiave = (n: number, lato: number) => `${file!.id}:${n}:${lato}`;

  async function chiediFigura(n: number, lato: number): Promise<string | null> {
    const k = chiave(n, lato), g = gettone;
    const c = url.get(k);
    if (c) return c;
    try {
      const r = await chiedi<{ url: string | null }>('voceAnteprima', { id: file!.id, n, lato });
      if (g !== gettone || !r.url) return null;
      url.set(k, r.url);
      // se ne tengono poche: sono data-url
      while (url.size > 600) url.delete(url.keys().next().value!);
      return r.url;
    } catch { return null; }
  }

  async function pompa() {
    if (inCorso) return;
    inCorso = true;
    const g = gettone;
    while (attesa.length && g === gettone) {
      const n = attesa.shift()!;
      const u = await chiediFigura(n, LATO_FIGURINA);
      if (g !== gettone) break;
      const v = voci[n];
      if (!v) continue;
      if (u) { (v.querySelector('img') as HTMLImageElement).src = u; v.classList.add('pronta'); } else v.classList.add('manca');
    }
    inCorso = false;
  }

  function etichettaDi(n: number): string {
    if (!indice) return '';
    const p = indice.punti[n];
    if (indice.tipo === 'pagine') return String(Math.round(p));
    if (indice.tipo === 'tempo') return durata(p);
    return '';
  }

  function didascalia(n: number): string {
    if (!indice) return '';
    const p = indice.punti[n];
    if (indice.tipo === 'pagine') return `Pagina ${Math.round(p)} di ${Math.round(indice.totale)}`;
    if (indice.tipo === 'tempo') return `${durata(p)} di ${durata(indice.totale)}`;
    return '';
  }

  async function scegli(n: number) {
    if (!indice || !file) return;
    n = Math.max(0, Math.min(indice.voci - 1, n));
    scelta = n;
    voci.forEach((v, k) => { v.classList.toggle('su', k === n); v.setAttribute('aria-selected', String(k === n)); });
    voci[n]?.scrollIntoView({ block: 'nearest' });
    etichetta.textContent = didascalia(n);
    // intanto la figurina (se c'è), poi la figura grande
    const piccola = url.get(chiave(n, LATO_FIGURINA)) ?? (indice.tipo === 'singola' ? file.anteprima : undefined);
    if (piccola) { img.src = piccola; img.hidden = false; segnaposto.hidden = true; }
    const g = gettone;
    const u = await chiediFigura(n, LATO_GRANDE);
    if (g !== gettone || scelta !== n) return;
    if (u) { img.src = u; img.hidden = false; segnaposto.hidden = true; }
    else if (!piccola) { img.hidden = true; segnaposto.hidden = false; segnaposto.classList.add('manca'); }
  }

  strisce.addEventListener('keydown', (e) => {
    if (!indice) return;
    const passo: Record<string, number> = { ArrowDown: 1, ArrowRight: 1, ArrowUp: -1, ArrowLeft: -1, PageDown: 5, PageUp: -5 };
    if (e.key in passo) { e.preventDefault(); void scegli(scelta + passo[e.key]); }
    else if (e.key === 'Home') { e.preventDefault(); void scegli(0); }
    else if (e.key === 'End') { e.preventDefault(); void scegli(indice.voci - 1); }
  });

  function costruisci(i: Indice) {
    indice = i;
    tipoDescritto = i.tipo === 'pagine' ? `${Math.round(i.totale)} ${i.totale === 1 ? 'pagina' : 'pagine'}` : i.tipo === 'tempo' ? `${i.voci} punti nel tempo` : '';
    strisce.replaceChildren();
    voci.length = 0;
    attesa.length = 0;
    osserva?.disconnect();
    osserva = new IntersectionObserver((es) => {
      for (const e of es) {
        if (!e.isIntersecting) continue;
        const n = Number((e.target as HTMLElement).dataset.n);
        osserva!.unobserve(e.target);
        if (!url.has(chiave(n, LATO_FIGURINA))) attesa.push(n);
        else { (e.target.querySelector('img') as HTMLImageElement).src = url.get(chiave(n, LATO_FIGURINA))!; e.target.classList.add('pronta'); }
      }
      // quelle in vista per prime: si chiede dalla più vicina alla scelta
      attesa.sort((a, b) => Math.abs(a - scelta) - Math.abs(b - scelta));
      void pompa();
    }, { root: strisce, rootMargin: '240px 0px' });
    for (let n = 0; n < i.voci; n++) {
      const v = h('button.cl-voce', { role: 'option', 'aria-label': didascalia(n) || 'Anteprima', dataset: { n: String(n) }, tabindex: '-1', onclick: () => { void scegli(n); strisce.focus({ preventScroll: true }); } },
        h('div.cl-mini', null, h('img', { alt: '', draggable: 'false' })),
        etichettaDi(n) ? h('span.cl-n', null, etichettaDi(n)) : null);
      voci.push(v);
      strisce.append(v);
      osserva.observe(v);
    }
    strisce.classList.toggle('sola', i.voci <= 1);
  }

  return {
    el,
    descrizione: () => tipoDescritto,
    ferma() { gettone++; attesa.length = 0; osserva?.disconnect(); file = null; indice = null; },
    mostra(f) {
      gettone++;
      attesa.length = 0;
      inCorso = false;
      file = f;
      indice = null;
      scelta = -1;
      tipoDescritto = '';
      strisce.replaceChildren();
      voci.length = 0;
      etichetta.textContent = '';
      // finché non arriva niente: la figurina della coda o l'icona della categoria
      segnaposto.classList.remove('manca');
      segnaposto.style.setProperty('--tinta', coloreCategoria[f.categoria] ?? 'var(--oro)');
      segnaposto.innerHTML = iconaCategoria[f.categoria] ?? '';
      if (f.anteprima) { img.src = f.anteprima; img.hidden = false; segnaposto.hidden = true; } else { img.hidden = true; segnaposto.hidden = false; }
      const g = gettone;
      chiedi<Indice>('indiceAnteprima', { id: f.id })
        .then((i) => { if (g !== gettone) return; costruisci(i); void scegli(0); pronto(); })
        .catch(() => { if (g !== gettone) return; costruisci({ tipo: 'singola', voci: 1, totale: 1, punti: [0] }); void scegli(0); pronto(); });
    },
  };
}
