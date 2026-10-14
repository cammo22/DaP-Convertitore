// Il tasto destro di Windows 11: l'invito, l'attivazione col permesso di Windows, il riavvio di Esplora file.

import { chiedi } from '../ponte';
import type { StatoMenu11 } from '../ponte';
import { h } from '../util';
import { icone } from '../icone';
import { clac, dinDon } from '../suoni';
import { s, ui } from './stato';
import { logoSvg, mostraAvviso } from './scheletro';

/** L'invito, una volta: su Windows 11 la voce va nel tasto destro nuovo, ma serve un permesso di Windows. */
export function invitoMenu11(): HTMLElement | null {
  const m = s.stato.menu11;
  if (!m?.supportato || m.registrato || m.chiesto) return null;
  return h('div.invito', null,
    h('div.invito-figura', { html: logoSvg }),
    h('div.invito-testo', null,
      h('b', null, 'Mettimi nel tasto destro di Windows 11'),
      h('p', null, 'Adesso sto sotto «Mostra altre opzioni». Con un clic vado nel menu principale, con le conversioni al volo. ',
        'Windows chiede il permesso ', h('b', null, 'una volta sola'), ': serve a fidarsi del certificato di DaProd.')),
    h('div.invito-tasti', null,
      h('button.tasto.su', { onclick: (e: Event) => void attivaMenu11(e.currentTarget as HTMLButtonElement) }, h('i.led.verde'), 'Attiva'),
      h('button.tasto', { onclick: () => { clac(); s.stato.menu11.chiesto = true; void chiedi('menu11', { azione: 'non-ora' }); ui.scegli(); } }, 'Non ora')));
}

export async function attivaMenu11(b?: HTMLButtonElement) {
  clac(true);
  if (b) { b.disabled = true; b.lastChild!.textContent = 'Aspetto Windows…'; }
  const r = await chiedi<{ stato: StatoMenu11; errore: string | null }>('menu11', { azione: 'attiva' });
  s.stato.menu11 = r.stato;
  if (document.querySelector('.velo.impostazioni')) ui.impostazioni();
  if (s.vista === 'scegli') ui.scegli();
  if (r.errore) mostraAvviso(r.errore);
  else { dinDon(); proponiRiavvio('Fatto. Esplora file legge il menu solo quando parte: riavvialo e trovi «DaP Convertitore» nel tasto destro.'); }
}

/** Esplora file va riavviato perché veda il menu nuovo (o il classico): lo si chiede, non lo si fa di nascosto. */
export function proponiRiavvio(testo: string) {
  document.querySelectorAll('.velo.riavvio').forEach((v) => v.remove());
  const velo = h('div.velo.riavvio', { onclick: (e: Event) => { if (e.target === velo) velo.remove(); } });
  velo.append(h('div.cassetto.piccolo', null,
    h('div.confronto-testa', null, h('b', null, 'RIAVVIA ESPLORA FILE'), h('span')),
    h('p.spiega', null, testo),
    h('p.spiega.piccolo', null, 'La barra delle applicazioni sparisce un secondo e le cartelle aperte si chiudono. I file non si toccano.'),
    h('div.invito-tasti', null,
      h('button.tasto.su', { onclick: async () => { clac(true); velo.remove(); await chiedi('esplora'); } }, h('i.led.verde'), 'Riavvia adesso'),
      h('button.tasto', { onclick: () => { clac(); velo.remove(); } }, 'Più tardi'))));
  document.body.append(velo);
}

/** La riga del menu di Windows 11: attiva (col permesso di Windows, una volta) o togli. */
export function rigaMenu11(): HTMLElement | null {
  const m = s.stato.menu11;
  if (!m?.supportato) return null;
  const testo = m.registrato
    ? 'Attivo: «DaP Convertitore ›» sta nel tasto destro principale, con «Apri nel convertitore…» in cima e sotto le conversioni al volo. Se non lo vedi, riavvia Esplora file qui sotto. Sulle versioni più nuove di Windows 11 può stare dentro «Estensioni app»: da Impostazioni → Personalizzazione → Menu contestuale lo porti in cima.'
    : m.fidato
      ? 'Il permesso c\'è già: un clic e la voce va nel tasto destro principale.'
      : 'La voce nel tasto destro principale, senza «Mostra altre opzioni». Windows chiede il permesso una volta sola: serve a fidarsi del certificato di DaProd.';
  const tasto = m.registrato
    ? h('button.tasto', { onclick: async () => { clac(); const r = await chiedi<{ stato: StatoMenu11 }>('menu11', { azione: 'togli' }); s.stato.menu11 = r.stato; ui.impostazioni(); } }, 'Togli')
    : h('button.tasto.su', { onclick: (e: Event) => void attivaMenu11(e.currentTarget as HTMLButtonElement) }, h('i.led.verde'), 'Attiva');
  return h('div.imp-riga', null, h('div', null, h('b', null, 'Nel tasto destro di Windows 11', m.registrato ? h('span.spunta', { html: icone.ok }) : null), h('p', null, testo)), tasto);
}
