// Le impostazioni: il cassetto con le leve.

import { chiedi } from '../ponte';
import type { StatoMenu11 } from '../ponte';
import { h } from '../util';
import { icone } from '../icone';
import { clac, accendiSuoni } from '../suoni';
import { s, ui } from './stato';
import { proponiRiavvio, rigaMenu11 } from './menu11';

export function apriImpostazioni() {
  clac();
  const imp = s.stato.impostazioni;
  // se è già aperto si ridisegna dentro lo stesso velo: due veli uno sull'altro facevano lo schermo nero
  const vecchio = document.querySelector('.velo.impostazioni') as HTMLElement | null;
  const cassetto = vecchio ?? h('div.velo.impostazioni', { onclick: (e: Event) => { if (e.target === cassetto) cassetto.remove(); } });
  const scorri = vecchio?.querySelector('.cassetto')?.scrollTop ?? 0;
  cassetto.replaceChildren();
  const salva = (m: Partial<typeof imp>) => { Object.assign(imp, m); void chiedi('impostazioni', m); };
  const scritta = h('input.campo', { value: imp.scritta, maxlength: 40, placeholder: 'convertito' }) as HTMLInputElement;
  scritta.addEventListener('change', () => { salva({ scritta: scritta.value.trim() }); ui.piede(); });
  const riga = (titolo: string, spiega: string, controllo: Node) => h('div.imp-riga', null, h('div', null, h('b', null, titolo), h('p', null, spiega)), controllo);
  const leva = (acceso: boolean, cambia: (v: boolean) => void) => {
    const b = h(`button.interruttore.solo${acceso ? '.su' : ''}`, null, h('i.levetta'));
    b.addEventListener('click', () => { clac(); acceso = !acceso; b.classList.toggle('su', acceso); cambia(acceso); });
    return b;
  };
  cassetto.append(h('div.cassetto', null,
    h('div.confronto-testa', null, h('b', null, 'IMPOSTAZIONI'), h('span'), h('button.btn-fin', { html: icone.chiudi, onclick: () => cassetto.remove() })),
    riga('La scritta nel nome', 'Foto.jpg diventa «Foto (convertito).webp». Puoi cambiarla.', scritta),
    riga('L\'originale nel Cestino da solo', 'Spento: l\'originale resta, e lo butti tu col cestino accanto al risultato. Acceso: quando il convertito ne prende il posto (MOV → MP4, PNG → JPG…) l\'originale va nel Cestino da solo. Mai quando ne tiri fuori un pezzo (l\'audio, il testo, le pagine) né su chiavette e dischi di rete.',
      leva(imp.cestinoDaSolo, (v) => { salva({ cestinoDaSolo: v }); ui.piede(); })),
    h('div.imp-sezione', null, 'LETTORE'),
    riga('Nel menu «Apri con»', 'Il lettore di DaP fra i programmi che aprono foto, video, musica, PDF, documenti, archivi, codice. Non si prende niente da solo. Porta anche le icone di ogni tipo e le anteprime vere in Esplora file (la foto, il fotogramma del video, il modello 3D, la forma dell\'onda).',
      leva(imp.apriCon, (v) => salva({ apriCon: v }))),
    riga('Aprire col doppio clic', 'Per farlo diventare il programma di tutti i giorni (al posto di Foto o Lettore multimediale): Windows lo fa scegliere a te, tipo per tipo.',
      h('button.tasto', { onclick: () => { clac(); void chiedi('predefinite'); } }, 'App predefinite')),
    h('div.imp-sezione', null, 'TASTO DESTRO'),
    rigaMenu11(),
    s.stato.menu11?.windows11
      ? riga('Menu classico ovunque', 'Il tasto destro apre subito il menu di Windows 10, con tutte le voci a un clic e senza «Mostra altre opzioni». Spegnilo per tornare al menu di Windows 11.',
          leva(s.stato.menu11.classico, async (v) => {
            s.stato.menu11 = await chiedi<StatoMenu11>('classico', { attivo: v });
            proponiRiavvio(v ? 'Il menu classico arriva quando riparte Esplora file.' : 'Il menu di Windows 11 torna quando riparte Esplora file.');
          }))
      : null,
    riga('Nel menu classico', 'La voce DaP Convertitore anche nel menu di prima: «Mostra altre opzioni» o Maiusc + tasto destro, e «Invia a».', leva(imp.menu, (v) => { salva({ menu: v }); void chiedi('menu', { attivo: v }); })),
    riga('Riavvia Esplora file', 'Esplora file legge le voci del tasto destro solo quando parte: se una voce non compare, di solito basta questo. Butta anche le anteprime e le icone vecchie, così ripartono nuove.',
      h('button.tasto', { onclick: () => { clac(); proponiRiavvio('Esplora file si spegne e si riaccende subito.'); } }, 'Riavvia')),
    h('div.imp-sezione', null, 'ALTRO'),
    riga('Spingi al massimo', 'FFmpeg a priorità normale: un po\' più veloce, ma mentre converte il PC si sente.', leva(imp.alMassimo, (v) => salva({ alMassimo: v }))),
    riga('Suoni', 'Il clac dei tasti e il din-don alla fine.', leva(imp.suoni, (v) => { salva({ suoni: v }); accendiSuoni(v); })),
    riga('Apri la cartella alla fine', 'Quando ha finito, apre Esplora file col file convertito già selezionato.', leva(imp.apriCartella, (v) => salva({ apriCartella: v }))),
    h('div.imp-piede', null,
      h('span', null, `DaP Convertitore ${s.stato.versione} · DaProd`),
      h('button.link', { onclick: () => chiedi('registro') }, 'Apri il registro'),
      h('button.link#cerca-agg', { onclick: async (e: Event) => {
        const b = e.currentTarget as HTMLButtonElement;
        b.textContent = 'Cerco…';
        const r = await chiedi<{ versione: string | null }>('aggiorna', { controlla: true }).catch(() => ({ versione: null }));
        b.textContent = r.versione ? `Installa la ${r.versione}` : 'Sei all\'ultima versione';
        if (r.versione) b.onclick = () => chiedi('aggiorna', { installa: true });
      } }, 'Cerca aggiornamenti')),
  ));
  if (!vecchio) document.body.append(cassetto);
  else (cassetto.querySelector('.cassetto') as HTMLElement).scrollTop = scorri;
}
