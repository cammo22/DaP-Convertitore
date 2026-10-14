// Incollare i file: tasto destro → «Incolla», o Ctrl+V. Entrano nella coda i file copiati in Esplora file (Ctrl+C).
import { chiedi } from '../ponte';
import { h } from '../util';
import { icone } from '../icone';
import { clac } from '../suoni';
import { s } from './stato';
import { mostraAvviso } from './scheletro';

export async function incolla() {
  clac();
  const n = await chiedi<number>('incolla').catch(() => 0);
  if (!n) mostraAvviso('Negli appunti non ci sono file: copiali in Esplora file (Ctrl+C) e poi incolla qui.');
}

/** Il menu del tasto destro sulla piastra: Incolla e Aggiungi file. (Il menu di WebView2 è spento.) */
function menuDestro(e: MouseEvent) {
  e.preventDefault();
  document.querySelector('.menu-destro')?.remove();
  if (s.stato.modo !== 'finestra' || s.vista !== 'scegli' || document.querySelector('.velo')) return;
  const voce = (testo: string, icona: string, tasto: string, fai: () => void) =>
    h('button', { onclick: () => { m.remove(); fai(); } }, h('span', { html: icona }), h('b', null, testo), h('small', null, tasto));
  const m = h('div.menu-destro', null,
    voce('Incolla', icone.aggiungi, 'Ctrl+V', () => void incolla()),
    voce('Aggiungi file…', icone.cartella, '', () => { clac(); void chiedi('scegli'); }));
  document.body.append(m);
  const r = m.getBoundingClientRect();
  m.style.left = `${Math.min(e.clientX, window.innerWidth - r.width - 8)}px`;
  m.style.top = `${Math.min(e.clientY, window.innerHeight - r.height - 8)}px`;
  setTimeout(() => document.addEventListener('pointerdown', function via(ev) {
    if (!m.contains(ev.target as Node)) { m.remove(); document.removeEventListener('pointerdown', via); }
  }), 0);
}

export function avviaIncolla() {
  document.addEventListener('contextmenu', menuDestro);
  document.addEventListener('keydown', (e) => {
    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'v' && s.stato.modo === 'finestra' && !document.querySelector('.velo') && !(e.target instanceof HTMLInputElement)) {
      e.preventDefault();
      void incolla();
    }
  });
}
