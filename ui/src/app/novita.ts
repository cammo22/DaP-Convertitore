// Novità e aggiornamenti: il tasto in alto apre le novità della versione in uso (dal CHANGELOG che viaggia con l'app) e dice
// se c'è un aggiornamento da installare. Dopo un aggiornamento si apre da solo, una volta, alla prima apertura.

import { chiedi } from '../ponte';
import { h } from '../util';
import { icone } from '../icone';
import { clac } from '../suoni';
import { s } from './stato';

export interface DatiNovita { versione: string; testo: string | null; nuova: boolean }

/** Un markdown piccolo piccolo: elenchi (anche su più righe), **grassetto** e `codice`. */
function markdown(md: string): HTMLElement {
  const box = h('div.novita-testo');
  const inline = (t: string) => {
    const el = h('span');
    const re = /\*\*([^*]+)\*\*|`([^`]+)`/g;
    let k = 0, m: RegExpExecArray | null;
    while ((m = re.exec(t))) {
      if (m.index > k) el.append(t.slice(k, m.index));
      el.append(m[1] ? h('b', null, m[1]) : h('code', null, m[2]));
      k = re.lastIndex;
    }
    if (k < t.length) el.append(t.slice(k));
    return el;
  };
  let lista: HTMLElement | null = null;
  let testo = '';
  let voce: HTMLElement | null = null;
  const chiudi = () => { if (voce) { voce.replaceChildren(inline(testo.trim())); voce = null; testo = ''; } };
  for (const r of md.split('\n')) {
    const li = r.match(/^[-*]\s+(.*)$/);
    if (li) {
      chiudi();
      if (!lista) { lista = h('ul'); box.append(lista); }
      voce = h('li');
      lista.append(voce);
      testo = li[1];
    } else if (voce && r.trim()) testo += ' ' + r.trim();
    else if (!r.trim()) { chiudi(); lista = null; }
  }
  chiudi();
  return box;
}

export async function apriNovita(dati?: DatiNovita) {
  clac();
  document.querySelectorAll('.velo.novita').forEach((v) => v.remove());
  const d = dati ?? await chiedi<DatiNovita>('novita').catch(() => null);
  const velo = h('div.velo.novita', { onclick: (e: Event) => { if (e.target === velo) velo.remove(); } });
  const stato = h('span.novita-stato', null, s.aggiornamento ? `C'è la ${s.aggiornamento}` : '');
  const bAgg = h('button.tasto.su', { onclick: async (e: Event) => {
    const b = e.currentTarget as HTMLButtonElement;
    clac(true);
    if (s.aggiornamento) { void chiedi('aggiorna', { installa: true }); b.textContent = 'Scarico e riapro…'; return; }
    b.textContent = 'Cerco…';
    const r = await chiedi<{ versione: string | null }>('aggiorna', { controlla: true }).catch(() => ({ versione: null }));
    if (r.versione) { s.aggiornamento = r.versione; b.textContent = `Installa la ${r.versione}`; stato.textContent = `C'è la ${r.versione}`; }
    else { b.textContent = 'Sei all\'ultima versione'; b.disabled = true; }
  } }, h('i.led.verde'), s.aggiornamento ? `Installa la ${s.aggiornamento}` : 'Cerca aggiornamenti');
  velo.append(h('div.cassetto.novita-cassetto', null,
    h('div.confronto-testa', null,
      h('b', null, d?.nuova ? `APPENA AGGIORNATO · ${d.versione}` : `NOVITÀ · ${d?.versione ?? s.stato.versione}`),
      h('span'), h('button.btn-fin', { html: icone.chiudi, onclick: () => velo.remove() })),
    d?.testo ? markdown(d.testo) : h('p.spiega', null, 'Le novità di questa versione non ci sono qui: le trovi nella pagina delle release su GitHub.'),
    h('div.invito-tasti', null, bAgg, stato, h('span.l-spazio'), h('button.tasto', { onclick: () => { clac(); velo.remove(); } }, 'Chiudi'))));
  document.body.append(velo);
}
