// Le impostazioni: il cassetto con le leve.

import { chiedi } from '../ponte';
import type { EsitoAggiusta, StatoMenu11 } from '../ponte';
import { h } from '../util';
import { icone, iconaCategoria } from '../icone';
import { clac, accendiSuoni } from '../suoni';
import { s, ui } from './stato';
import { mostraAvviso } from './scheletro';
import { proponiRiavvio } from './menu11';
import { apriNovita } from './novita';

/** Gruppi spenti di partenza: i documenti e gli archivi li aprono meglio Office ed Esplora file. */
const spentiDiPartenza = new Set(['Documento', 'Presentazione', 'Tabella', 'Archivio']);

/**
 * «Sicuro che vuoi che tutto si apra con DaProd?»: la lista dei tipi con un interruttore ciascuno. Con OK l'app registra
 * solo quelli accesi e apre la pagina di Windows già pronta: le app predefinite Windows le fa scegliere a te, con un clic.
 */
export function apriPredefinite() {
  const imp = s.stato.impostazioni;
  const gruppi = s.stato.apriConGruppi ?? [];
  const regola = s.stato.regolaPredefiniti ?? { supportata: false, attiva: false, diAltri: false };
  const acceso = new Map(gruppi.map((g) => [g.id, imp.gruppiPredefiniti ? imp.gruppiPredefiniti.includes(g.id) : !spentiDiPartenza.has(g.id)]));
  document.querySelector('.velo.predefinite')?.remove();
  const conta = h('span.pred-conta');
  const aggiorna = () => { const n = [...acceso.values()].filter(Boolean).length; conta.textContent = n === 1 ? '1 tipo' : `${n} tipi`; };
  const velo = h('div.velo.predefinite', { onclick: (e: Event) => { if (e.target === velo) velo.remove(); } });
  const riga = (g: { id: string; nome: string; esempi: string }, i: number) => {
    const b = h(`button.interruttore.solo${acceso.get(g.id) ? '.su' : ''}`, null, h('i.levetta'));
    const r = h('div.pred-riga', { onclick: () => { clac(); acceso.set(g.id, !acceso.get(g.id)); b.classList.toggle('su', !!acceso.get(g.id)); aggiorna(); } },
      h('span.pred-num', null, String(i + 1)), h('span.pred-icona', { html: iconaCategoria[({ Tabella: 'foglio', Font: 'altro', Modello: 'altro', Codice: 'testo' } as Record<string, string>)[g.id] ?? g.id.toLowerCase()] ?? iconaCategoria.altro }),
      h('div.pred-testo', null, h('b', null, g.nome), h('small', null, g.esempi.replaceAll('.', ''))), b);
    return r;
  };
  velo.append(h('div.cassetto.predefinite', null,
    h('div.confronto-testa', null, h('b', null, 'APRIRE TUTTO CON DAPROD?'), h('span'), h('button.btn-fin', { html: icone.chiudi, onclick: () => velo.remove() })),
    h('p.spiega', null, 'Sicuro che vuoi che questi file si aprano col lettore di DaP Convertitore? Spegni quelli che vuoi lasciare ai programmi di prima.'),
    h('div.pred-lista', null, ...gruppi.map(riga)),
    h('p.spiega.piccolo', null, regola.supportata && !regola.diAltri
      ? 'Con OK Windows ti chiede il permesso di amministratore (una volta): scrivo la lista dei tipi accesi come regola di Windows, e si applica quando ti disconnetti e riaccedi. Da lì quei tipi tornano al lettore anche se li cambi a mano, finché non togli la regola.'
      : 'Questo Windows non accetta la regola (edizione Home, o ce n\'è già una di altri): con OK registro i tipi accesi e apro la pagina di Windows, dove il clic su ognuno lo fai tu.'),
    h('div.invito-tasti', null,
      h('button.tasto.su', { onclick: () => {
        clac(true);
        const scelti = gruppi.filter((g) => acceso.get(g.id)).map((g) => g.id);
        imp.gruppiPredefiniti = scelti;
        void chiedi<{ esito: string }>('predefinite', { gruppi: scelti }).then((r) => { velo.remove(); if (r.esito !== 'pagina') esitoRegola(r.esito, true); });
      } }, h('i.led.verde'), regola.supportata && !regola.diAltri ? 'OK, applica (chiede l\'amministratore)' : 'OK, registra e vai a Windows', conta),
      regola.attiva ? h('button.tasto', { title: 'Cancella la regola di Windows: i tipi tornano a come li scegli tu', onclick: () => { clac(); void chiedi<{ esito: string }>('predefiniteTogli').then((r) => { velo.remove(); esitoRegola(r.esito, false); }); } }, 'Togli la regola') : null,
      h('button.tasto', { onclick: () => { clac(); velo.remove(); } }, 'Annulla'))));
  document.body.append(velo);
  aggiorna();
}

/** Com'è andata la regola di Windows (permesso dato o negato). */
function esitoRegola(esito: string, applicata: boolean) {
  const imp = s.stato.regolaPredefiniti;
  if (esito === 'ok' && imp) imp.attiva = applicata;
  mostraAvviso(({
    ok: applicata ? 'Fatto. Disconnettiti e riaccedi a Windows: da lì i tipi scelti si aprono con DaP Convertitore.' : 'Regola tolta. I tipi tornano a come li scegli tu (al prossimo accesso).',
    negato: 'Non hai dato il permesso: non ho cambiato niente.',
    gia: 'Su questo PC c\'è già una regola di associazioni (di un\'azienda o di un altro programma): non la tocco.',
    errore: 'Non sono riuscito a scrivere la regola. Il registro (Impostazioni → Apri il registro) dice perché.',
  } as Record<string, string>)[esito] ?? 'Fatto.');
}

/** Il tasto «Aggiusta tutto»: il motore fa i passi, qui si legge com'è andata e si ridisegna. */
async function aggiustaTutto(b: HTMLButtonElement) {
  clac(true);
  b.disabled = true;
  b.lastChild!.textContent = 'Aspetto Windows…';
  try {
    const r = await chiedi<EsitoAggiusta>('aggiustaTutto');
    Object.assign(s.stato.impostazioni, r.impostazioni);
    s.stato.menu11 = r.menu11;
    s.stato.regolaPredefiniti = r.regola;
    const segno = { ok: '✓', gia: '✓', pagina: '→', negato: '✗', errore: '✗' } as const;
    document.querySelectorAll('.velo.riassunto').forEach((v) => v.remove());
    const velo = h('div.velo.riassunto', { onclick: (e: Event) => { if (e.target === velo) velo.remove(); } });
    velo.append(h('div.cassetto.piccolo', null,
      h('div.confronto-testa', null, h('b', null, 'COM\'È ANDATA'), h('span')),
      h('ul.imp-stati', null, ...r.passi.map((p) => h(`li${p.esito === 'ok' || p.esito === 'gia' ? '.ok' : ''}`, null, h('i.puntino'), h('span', null, `${segno[p.esito]} ${p.nome}`), p.nota ? h('small', null, p.nota) : null))),
      h('div.invito-tasti', null, h('button.tasto.su', { onclick: () => { clac(); velo.remove(); } }, 'OK'))));
    document.body.append(velo);
    apriImpostazioni();
    if (r.daRiavviare) proponiRiavvio('Esplora file legge il menu solo quando parte: riavvialo e «DaP Convertitore» compare nel tasto destro.');
  } catch (e) {
    mostraAvviso(`Non sono riuscito: ${(e as Error).message}`);
    b.disabled = false;
  }
}

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
  // ——— il tasto solo: «Aggiusta tutto» mette a posto tasto destro, «Apri con» e app predefinite ———
  const m = s.stato.menu11;
  const regola = s.stato.regolaPredefiniti;
  const stati: { nome: string; ok: boolean; nota?: string }[] = [
    { nome: 'Tasto destro (menu classico e «Invia a»)', ok: imp.menu },
    ...(m?.supportato ? [{ nome: 'Tasto destro di Windows 11', ok: m.registrato }] : []),
    { nome: 'Menu «Apri con» e icone dei file', ok: imp.apriCon },
    { nome: 'App predefinite', ok: regola.totale > 0 && regola.impostati === regola.totale,
      nota: regola.totale ? `${regola.impostati} tipi su ${regola.totale} si aprono col lettore` : undefined },
  ];
  const tuttoOk = stati.every((x) => x.ok);
  const bAggiusta = h<HTMLButtonElement>('button.tasto.su.grande', { onclick: () => void aggiustaTutto(bAggiusta) }, h('i.led.verde'), tuttoOk ? 'Controlla di nuovo' : 'Aggiusta tutto');
  const sistema = h('div.imp-sistema', null,
    h('div.imp-sistema-testa', null,
      h('div', null, h('b', null, tuttoOk ? 'Tutto a posto' : 'C\'è qualcosa da sistemare'),
        h('p', null, 'Un tasto solo: mette il convertitore nel tasto destro, il lettore in «Apri con» e prepara le app predefinite, così i file si aprono da soli. Windows può chiedere il permesso di amministratore (una volta).')),
      bAggiusta),
    h('ul.imp-stati', null, ...stati.map((x) => h(`li${x.ok ? '.ok' : ''}`, null, h('i.puntino'), h('span', null, x.nome), x.nota ? h('small', null, x.nota) : null))),
    h('button.link', { title: 'Toglie le voci dal tasto destro e da «Apri con»', onclick: async () => {
      const r = await chiedi<{ menu11: StatoMenu11; regola: typeof regola }>('togliTutto');
      imp.menu = false; imp.apriCon = false;
      s.stato.menu11 = r.menu11; s.stato.regolaPredefiniti = r.regola;
      apriImpostazioni();
    } }, 'Togli le voci dal sistema'),
    regola.attiva ? h('button.link', { title: 'Cancella la regola di Windows: i tipi tornano a come li scegli tu', onclick: () => { clac(); void chiedi<{ esito: string }>('predefiniteTogli').then((r) => esitoRegola(r.esito, false)); } }, 'Togli la regola delle app predefinite') : null);

  cassetto.append(h('div.cassetto', null,
    h('div.confronto-testa', null, h('b', null, 'IMPOSTAZIONI'), h('span'), h('button.btn-fin', { html: icone.chiudi, onclick: () => cassetto.remove() })),
    riga('La scritta nel nome', 'Foto.jpg diventa «Foto (convertito).webp». Puoi cambiarla.', scritta),
    riga('L\'originale nel Cestino da solo', 'Spento: l\'originale resta, e lo butti tu col cestino accanto al risultato. Acceso: quando il convertito ne prende il posto (MOV → MP4, PNG → JPG…) l\'originale va nel Cestino da solo. Mai quando ne tiri fuori un pezzo (l\'audio, il testo, le pagine) né su chiavette e dischi di rete.',
      leva(imp.cestinoDaSolo, (v) => { salva({ cestinoDaSolo: v }); ui.piede(); })),
    h('div.imp-sezione', null, 'SISTEMA'),
    sistema,
    riga('Scegliere i tipi da aprire col lettore', 'La lista dei tipi di file (foto, video, musica, PDF…) con un interruttore ciascuno: «Aggiusta tutto» usa questa scelta.',
      h('button.tasto', { onclick: () => { clac(); apriPredefinite(); } }, 'Scegli i tipi')),
    ...(s.stato.menu11?.windows11
      ? [riga('Mostra tutti i menu del tasto destro', 'Su Windows 11 il tasto destro apre subito il menu completo, con tutte le voci a un clic e senza «Mostra altre opzioni». Spento torna il menu di Windows 11. Esplora file va riavviato: te lo chiedo.',
          leva(s.stato.menu11.classico, async (v) => {
            s.stato.menu11 = await chiedi<StatoMenu11>('classico', { attivo: v });
            proponiRiavvio(v ? 'Il menu completo arriva quando riparte Esplora file.' : 'Il menu di Windows 11 torna quando riparte Esplora file.');
          }))]
      : []),
    h('div.imp-sezione', null, 'AVATAR'),
    riga('Avatar sul desktop', 'Un oggetto piccolo (una musicassetta, poi un VHS, un floppy…) che sta in un angolo, ogni tanto fa un movimento e si trasforma. Non dà fastidio: sta fermo quando hai un programma a schermo intero. Ci trascini sopra dei file e ti chiede: aprire, convertire o comprimere. Si sposta col mouse, tasto destro per il menu.',
      leva(!!imp.avatarAttivo, (v) => { salva({ avatarAttivo: v }); apriImpostazioni(); })),
    ...(imp.avatarAttivo ? [riga('Parte con Windows', 'L\'avatar si accende da solo quando accedi, senza aprire finestre.', leva(!!imp.avatarAvvio, (v) => salva({ avatarAvvio: v })))] : []),
    h('div.imp-sezione', null, 'ALTRO'),
    riga('Spingi al massimo', 'FFmpeg a priorità normale: un po\' più veloce, ma mentre converte il PC si sente.', leva(imp.alMassimo, (v) => salva({ alMassimo: v }))),
    riga('Suoni', 'Il clac dei tasti e il din-don alla fine. Di partenza sono spenti.', leva(imp.suoniAccesi, (v) => { salva({ suoniAccesi: v }); accendiSuoni(v); })),
    riga('Apri la cartella alla fine', 'Quando ha finito, apre Esplora file col file convertito già selezionato.', leva(imp.apriCartella, (v) => salva({ apriCartella: v }))),
    h('div.imp-piede', null,
      h('span', null, `DaP Convertitore ${s.stato.versione} · DaProd`),
      h('button.link', { onclick: () => chiedi('registro') }, 'Apri il registro'),
      h('button.link', { onclick: () => void apriNovita() }, 'Novità e aggiornamenti')),
  ));
  if (!vecchio) document.body.append(cassetto);
  else (cassetto.querySelector('.cassetto') as HTMLElement).scrollTop = scorri;
}
