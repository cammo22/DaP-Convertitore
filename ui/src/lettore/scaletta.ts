// La scaletta: i file della cartella (musica o video), a destra. Sta chiusa finché non la apri, ma una linguetta sul
// bordo ricorda che c'è. Si apre con il tasto, con la linguetta o col pulsante dei comandi; passando da un file all'altro
// in questa finestra resta com'era (aperta o chiusa), al prossimo avvio riparte chiusa.

import { h } from '../util';
import { type Contesto } from './comune';

/** La scaletta era aperta in questa finestra: vive quanto la pagina del lettore. */
let apertaInFinestra = false;

export interface Scaletta {
  /** La lista che scorre da destra: va messa dentro la stanza (musica o cinema). */
  el: HTMLElement;
  /** La linguetta sul bordo destro, visibile solo quando nella cartella c'è altro. */
  linguetta: HTMLElement;
  gira(): void;
  aperta(): boolean;
}

/**
 * @param nomeBrano come si scrive un file nell'elenco (di partenza il nome senza estensione)
 * @param extra una riga in più sotto il titolo (per la musica: «Di seguito»)
 */
export function scaletta(c: Contesto, opzioni: { extra?: () => Node | null; nomeBrano?: (percorso: string) => string } = {}): Scaletta {
  const el = h('aside.a-scaletta');
  const stanza = () => el.parentElement as HTMLElement | null;
  const nome = opzioni.nomeBrano ?? ((p: string) => p.split('\\').pop()!.replace(/\.[^.]+$/, ''));

  const apri = () => {
    const { file, indice } = c.elenco();
    const brani = file.map((p, i) =>
      h(`button.a-brano${i === indice ? '.su' : ''}`, { title: 'Clic: suona questo', onclick: () => c.vaiA(i) },
        h('span', null, String(i + 1).padStart(2, '0')), nome(p)));
    const extra = opzioni.extra?.();
    el.replaceChildren(
      h('div.a-scaletta-testa', null, h('b', null, 'SCALETTA'), h('button.a-chiudi', { title: 'Chiudi', onclick: () => gira() }, '×')),
      ...(extra ? [extra] : []),
      h('div.a-brani', null, ...brani));
    el.querySelector('.su')?.scrollIntoView({ block: 'center' });
  };

  function gira() {
    const s = stanza();
    if (!s) return;
    const aperta = s.classList.toggle('con-scaletta');
    apertaInFinestra = aperta;
    if (aperta) apri();
  }

  // dentro la scaletta le frecce su e giù passano da un brano all'altro (non cambiano il volume)
  el.addEventListener('keydown', (e) => {
    if (e.key !== 'ArrowUp' && e.key !== 'ArrowDown') return;
    const lista = [...el.querySelectorAll<HTMLElement>('.a-brano')];
    const ora = lista.indexOf(document.activeElement as HTMLElement);
    const partenza = ora < 0 ? lista.findIndex((x) => x.classList.contains('su')) : ora;
    const dopo = lista[Math.max(0, Math.min(lista.length - 1, partenza + (e.key === 'ArrowDown' ? 1 : -1)))];
    if (!dopo) return;
    e.preventDefault();
    e.stopPropagation();
    dopo.focus();
  });

  const linguetta = h('button.l-linguetta.solo-fratelli', { title: 'Mostra la scaletta', onclick: () => gira() }, h('span', null, 'SCALETTA'));

  // in questa finestra era già aperta (si è passati a un altro file): resta aperta, appena si sa cosa c'è accanto
  if (apertaInFinestra) {
    const riapri = () => { if (c.viva() && c.fratelli() > 1 && !stanza()?.classList.contains('con-scaletta')) gira(); };
    // la stanza arriva dopo: si aspetta un istante che il chiamante abbia messo la lista al suo posto
    queueMicrotask(() => { if (c.fratelli() > 1) riapri(); else document.addEventListener('dap-fratelli', riapri, { once: true }); });
  }

  return { el, linguetta, gira, aperta: () => !!stanza()?.classList.contains('con-scaletta') };
}
