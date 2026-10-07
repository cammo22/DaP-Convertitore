// Piccoli attrezzi: pesi, tempi, numeri all'italiana, e un costruttore di elementi.
// I pesi si contano come Esplora file (1 KB = 1024 byte): il numero che vedi qui è quello che vedi lì.
export const KB = 1024, MB = KB * 1024, GB = MB * 1024;

export function peso(byte: number | null | undefined, cifre?: number): string {
  if (byte == null || !isFinite(byte)) return '—';
  const u = ['byte', 'KB', 'MB', 'GB', 'TB'];
  let v = byte;
  let i = 0;
  while (v >= 1000 && i < u.length - 1) { v /= 1024; i++; }
  const d = cifre ?? (i === 0 ? 0 : v < 10 ? 2 : v < 100 ? 1 : 0);
  return `${v.toLocaleString('it-IT', { minimumFractionDigits: d, maximumFractionDigits: d })} ${u[i]}`;
}

/** Il peso diviso in numero e unità, per il display a sette segmenti. */
export function pesoDisplay(byte: number): { numero: string; unita: string } {
  const u = ['B', 'KB', 'MB', 'GB', 'TB'];
  let v = byte;
  let i = 0;
  while (v >= 1000 && i < u.length - 1) { v /= 1024; i++; }
  const d = v < 10 ? 2 : v < 100 ? 1 : 0;
  return { numero: v.toFixed(d), unita: u[i] };
}

export function durata(s: number | null | undefined): string {
  if (s == null || !isFinite(s) || s < 0) return '—';
  s = Math.round(s);
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  const ss = s % 60;
  return h > 0 ? `${h}:${String(m).padStart(2, '0')}:${String(ss).padStart(2, '0')}` : `${m}:${String(ss).padStart(2, '0')}`;
}

/** Per il display: sempre HH:MM:SS. */
export function orologio(s: number | null | undefined): string {
  if (s == null || !isFinite(s) || s < 0) return '--:--:--';
  s = Math.round(s);
  const h = Math.min(99, Math.floor(s / 3600));
  return `${String(h).padStart(2, '0')}:${String(Math.floor((s % 3600) / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`;
}

export function numero(n: number, cifre = 0): string {
  return n.toLocaleString('it-IT', { minimumFractionDigits: cifre, maximumFractionDigits: cifre });
}

export function risoluzione(w?: number, h?: number): string {
  if (!w || !h) return '';
  const corto = Math.min(w, h);
  const nome = corto >= 2100 ? '4K' : corto >= 1400 ? '1440p' : corto >= 1060 ? '1080p' : corto >= 700 ? '720p' : `${corto}p`;
  return nome;
}

type Figlio = Node | string | null | undefined | false;

/** h('div.classe#id', {attributi}, figli…) */
export function h<T extends HTMLElement = HTMLElement>(sel: string, attr: Record<string, any> | null = null, ...figli: (Figlio | Figlio[])[]): T {
  // 'tag.classe.altra#id': l'id può stare in qualsiasi punto
  const tag = sel.match(/^[a-z0-9-]*/i)![0];
  const id = sel.match(/#([\w-]+)/)?.[1];
  const classi = [...sel.matchAll(/\.([\w-]+)/g)].map((m) => m[1]);
  const el = document.createElement(tag || 'div') as T;
  if (id) el.id = id;
  if (classi.length) el.className = classi.join(' ');
  if (attr) {
    for (const [k, v] of Object.entries(attr)) {
      if (v == null || v === false) continue;
      if (k.startsWith('on') && typeof v === 'function') el.addEventListener(k.slice(2), v);
      else if (k === 'style' && typeof v === 'object') Object.assign(el.style, v);
      else if (k === 'html') el.innerHTML = v;
      else if (k === 'dataset') Object.assign(el.dataset, v);
      else el.setAttribute(k, v === true ? '' : String(v));
    }
  }
  for (const f of figli.flat()) {
    if (f == null || f === false) continue;
    el.append(typeof f === 'string' ? document.createTextNode(f) : f);
  }
  return el;
}

export function svg(markup: string): SVGElement {
  const t = document.createElement('template');
  t.innerHTML = markup.trim();
  return t.content.firstElementChild as SVGElement;
}

export const clamp = (x: number, a: number, b: number) => Math.min(b, Math.max(a, x));
