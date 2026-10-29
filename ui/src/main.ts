// DaP Convertitore — la piastra di conversione. Vecchio stile, moderno dentro:
// a sinistra la cassetta d'origine (i file), a destra cosa diventano, sotto il tasto grosso.
// I pezzi stanno in app/: stato, scheletro, controlli, pannelli (video, immagine, altri), lavoro, risultati, impostazioni, rapido.
import '@fontsource/orbitron/700.css';
import '@fontsource/orbitron/900.css';
import '@fontsource/rajdhani/500.css';
import '@fontsource/rajdhani/600.css';
import '@fontsource/rajdhani/700.css';
import './stile.css';
import { ascolta, avviaFinto, chiedi, dentroApp } from './ponte';
import type { Carico, Categoria, Formato, InfoFile, Lavoro, Opzioni, Stato } from './ponte';
import { h, peso, durata, risoluzione } from './util';
import { icone, iconaCategoria, coloreCategoria } from './icone';
import { clac, accendiSuoni } from './suoni';
import { disegnaHome } from './app/home';
import { apriNovita, type DatiNovita } from './app/novita';
import { s, ui, nomeCat, formato, fileDi, categoriePresenti, sceltaPer, eVideoVero } from './app/stato';
import { app, testa, coda, banco, piede, corpo, logoSvg, mostraAvviso, conservaVista } from './app/scheletro';
import { regolazioni } from './app/pannelli';
import { aggiornaPiano, pesoObiettivo } from './app/pannelli/video';
import { conQualita, pesoVolutoFoto, QUALITA_PER_PESO } from './app/pannelli/immagine';
import { disegnaLavoro, aggiornaLavoro, statoGiro, strumenti } from './app/lavoro';
import { disegnaFatto, tastoCestino } from './app/risultati';
import { disegnaRapido } from './app/rapido';
import { apriImpostazioni } from './app/impostazioni';
import { invitoMenu11 } from './app/menu11';
import { avviaIncolla } from './app/incolla';
import { aggiornaAnteprima, dimenticaAnteprima } from './app/anteprima';

// ————————————————————————————————— testa —————————————————————————————————

function cpuCorta(n: string) {
  return n.replace(/\(R\)|\(TM\)|CPU|Processor/gi, '').replace(/with Radeon.*$/i, '').replace(/@.*$/, '').replace(/^(AMD|Intel)\s+/i, '').replace(/Core\s+/i, '').replace(/\s+/g, ' ').trim();
}

function disegnaTesta() {
  const hw = s.stato.hardware;
  testa.replaceChildren(
    h('div.marchio', null,
      h('span.logo', { html: logoSvg }),
      h('span.nome', null, h('b', null, 'DaP'), h('span', null, 'CONVERTITORE')),
    ),
    h('div.trascina', { 'data-regione': 'trascina' }),
    h('div.chip-hw', null,
      s.aggiornamento ? h('button.chip.agg', { title: `Installa la ${s.aggiornamento} e riapre il convertitore`, onclick: () => { clac(true); void chiedi('aggiorna', { installa: true }); } }, h('i.led.verde'), `Aggiornamento ${s.aggiornamento} disponibile`) : null,
      hw?.acceleratore ? h('span.chip.gpu', { title: hw.gpu.map((g) => g.nome).join(' · ') }, h('i.led.verde'), hw.acceleratore) : h('span.chip', null, h('i.led'), 'Solo CPU'),
      hw ? h('span.chip', { title: hw.cpu }, h('i.led.ambra'), `${cpuCorta(hw.cpu)} · ${hw.thread} thread`) : h('span.chip', null, h('i.led.lampeggia'), 'Guardo la macchina…'),
    ),
    h('div.finestra', null,
      h('button.btn-fin.nov', { title: 'Novità e aggiornamenti', onclick: () => void apriNovita(), html: icone.novita }),
      h(`button.btn-fin.av${s.stato.impostazioni.avatarAttivo ? '.su' : ''}`, { title: s.stato.impostazioni.avatarAttivo ? "Spegni l'avatar" : "Accendi l'avatar", onclick: () => accendiAvatar(!s.stato.impostazioni.avatarAttivo), html: icone.avatar }),
      h('button.btn-fin', { title: 'Impostazioni', onclick: () => apriImpostazioni() , html: icone.ingranaggio }),
      h('button.btn-fin', { title: 'Riduci', onclick: () => chiedi('finestra', { azione: 'riduci' }), html: icone.riduci }),
      s.stato.modo === 'finestra' ? h('button.btn-fin', { title: 'Ingrandisci', onclick: () => chiedi('finestra', { azione: 'massimizza' }), html: icone.ingrandisci }) : null,
      h('button.btn-fin.chiudi', { title: 'Chiudi', onclick: () => chiedi('finestra', { azione: 'chiudi' }), html: icone.chiudi }),
    ),
  );
}

/** «Indietro» nella mini interfaccia: i suoi file escono dalla lista, riappare il fumetto «Cosa vuoi farne?», e la finestra si chiude se è vuota. */
async function tornaIndietro() {
  clac();
  const r = await chiedi<{ ids: string[] }>('indietro');
  for (const id of r.ids) { s.file.delete(id); s.ordine = s.ordine.filter((x) => x !== id); void chiedi('togli', { id }); }
  if (s.ordine.length === 0) void chiedi('finestra', { azione: 'chiudi' });
  else void chiedi('finestra', { azione: 'completa' });
}

/** Il simbolo accanto alle novità: accende e spegne l'avatar sul desktop (come l'interruttore nelle Impostazioni). */
function accendiAvatar(si: boolean) {
  clac();
  s.stato.impostazioni.avatarAttivo = si;
  void chiedi('impostazioni', { avatarAttivo: si });
  disegnaTesta();
}

// ————————————————————————————————— coda (sinistra) —————————————————————————————————

function metaDi(f: InfoFile): string {
  const d = f.dettagli;
  const p: string[] = [];
  if (d?.errore) p.push('non si legge');
  if (f.categoria === 'video' && d) {
    if (d.larghezza) p.push(risoluzione(d.larghezza, d.altezza));
    if (d.durata) p.push(durata(d.durata));
    if (d.codec) p.push(`${d.codec.toUpperCase()}${d.hdr ? ' HDR' : ''}`);
  } else if (f.categoria === 'audio' && d) {
    if (d.durata) p.push(durata(d.durata));
    if (d.audio) p.push(d.audio.toUpperCase());
  } else if (f.categoria === 'immagine' && d?.larghezza) p.push(`${d.larghezza}×${d.altezza}`);
  else if (f.categoria === 'pdf' && d?.pagine) p.push(`${d.pagine} ${d.pagine === 1 ? 'pagina' : 'pagine'}`);
  else if (f.categoria === 'cartella' && d?.file != null) p.push(`${d.file} file`);
  p.push(peso(f.eDirectory ? d?.peso ?? 0 : f.peso));
  return p.join(' · ');
}

function disegnaCoda() { conservaVista(coda, disegnaCodaVera); }

/** Sceglie un file della coda. Mentre si lavora o a lavoro finito si cambia solo la selezione e l'anteprima: la piastra in mezzo non si tocca. */
function scegliFile(id: string) {
  const f = s.file.get(id);
  if (!f) return;
  clac();
  s.attiva = f.categoria;
  s.fileAttivo = id;
  s.piano = null;
  if (s.vista === 'scegli') disegnaTutto();
  else { disegnaCoda(); aggiornaAnteprima(); }
}

function disegnaCodaVera() {
  const tot = s.ordine.reduce((a, i) => a + (s.file.get(i)?.peso ?? 0), 0);
  const lista = h('div.lista');
  for (const id of s.ordine) {
    const f = s.file.get(id)!;
    const lav = [...s.lavori.values()].reverse().find((l) => l.fileId === id);
    const scheda = h(`div.scheda${s.fileAttivo === id ? '.attiva' : ''}${f.categoria !== s.attiva ? '.spenta' : ''}`,
      { dataset: { id }, tabindex: '0', role: 'option', 'aria-selected': String(s.fileAttivo === id), style: { '--tinta': coloreCategoria[f.categoria] },
        onclick: () => scegliFile(id),
        onkeydown: (e: KeyboardEvent) => { if (e.target === e.currentTarget && (e.key === 'Enter' || e.key === ' ')) { e.preventDefault(); scegliFile(id); } } },
      h('div.figurina', null,
        f.anteprima ? h('img', { src: f.anteprima, alt: '' }) : h('span.icona-cat', { html: iconaCategoria[f.categoria] }),
        // senza estensione (LEGGIMI, LICENSE) non è una cartella: si dice FILE
        h('span.est', null, (f.eDirectory ? 'cartella' : f.estensione || 'file').replace('.', '').toUpperCase()),
      ),
      h('div.testo', null,
        h('div.nome', { title: f.percorso }, f.nome),
        h('div.meta', null, metaDi(f)),
        lav && lav.stato !== 'attesa' ? h(`div.mini-barra.${lav.stato}`, null, h('i', { style: { width: `${Math.round((lav.stato === 'fatto' ? 1 : Math.max(0, lav.frazione)) * 100)}%` } })) : null,
      ),
      f.eDirectory ? null : h('button.guarda', { title: 'Guarda nel lettore', html: icone.occhio, onclick: (e: Event) => { e.stopPropagation(); clac(); void chiedi('guarda', { percorso: f.percorso }); } }),
      s.vista === 'scegli' ? h('button.togli', { title: 'Togli dalla coda', html: icone.chiudi, onclick: (e: Event) => { e.stopPropagation(); togli(id); } }) : null,
    );
    lista.append(scheda);
  }
  coda.replaceChildren(...[
    h('div.titolo-sezione', null, h('span', null, 'SORGENTE'), h('span.conta', null, s.ordine.length ? `${s.ordine.length} · ${peso(tot)}` : ''),
      s.ordine.length && s.vista === 'scegli' ? h('button.svuota', { title: 'Svuota la coda (Ctrl+Canc): i file restano dove sono', onclick: () => { clac(); svuota(); } }, 'svuota') : null),
    s.ordine.length ? lista : h('div.vuota', null, h('div.icona-grande', { html: icone.scarica }), h('p', null, 'Trascina qui i file da convertire'), h('p.piccolo', null, 'o tasto destro su un file → DaP Convertitore')),
    s.vista === 'scegli' ? h('button.aggiungi', { onclick: () => { clac(); void chiedi('scegli'); } }, h('span', { html: icone.aggiungi }), 'Aggiungi file') : null,
  ].filter((x): x is HTMLElement => !!x));
}

/** Svuota la coda in un colpo: i file non si toccano, si tolgono solo dalla lista. */
function svuota() {
  if (s.ordine.length === 0) return;
  s.file.clear();
  s.ordine = [];
  s.pesoPerFile.clear();
  s.attiva = null;
  s.fileAttivo = null;
  s.piano = null;
  dimenticaAnteprima();
  void chiedi('svuota');
  disegnaTutto();
}

function togli(id: string) {
  clac();
  s.file.delete(id);
  s.ordine = s.ordine.filter((x) => x !== id);
  s.pesoPerFile.delete(id);
  dimenticaAnteprima(id);
  void chiedi('togli', { id });
  if (s.fileAttivo === id) s.fileAttivo = null;
  sistemaAttiva();
  disegnaTutto();
}

function sistemaAttiva() {
  const cc = categoriePresenti();
  if (!s.attiva || !cc.includes(s.attiva)) s.attiva = cc[0] ?? null;
  if (!s.fileAttivo || s.file.get(s.fileAttivo)?.categoria !== s.attiva) s.fileAttivo = s.attiva ? fileDi(s.attiva)[0]?.id ?? null : null;
}

// ————————————————————————————————— banco: scegli —————————————————————————————————

function disegnaScegli() { conservaVista(banco, disegnaScegliVera); }

/** «Comprimi» dall'avatar: il formato e il punto di partenza (metà peso, foto più piccola) sono già scelti, il resto lo decide Cammo. */
function applicaRichiesta() {
  const r = s.sceltaRichiesta;
  if (!r) return;
  const f = s.stato.formati.find((x) => x.id === r.formato);
  if (!f) { s.sceltaRichiesta = null; return; }
  const cat = f.categoria as Categoria;
  const dei = fileDi(cat);
  if (!dei.length) return;          // i file non sono ancora arrivati: si riprova al prossimo disegno
  s.attiva = cat;
  s.fileAttivo = dei[0].id;
  s.scelta[cat] = f.id;
  s.piano = null;
  if (r.preset === 'meta') { s.opzioni.video.modo = 'peso'; s.opzioni.video.codec = 'hevc'; for (const x of dei) s.pesoPerFile.set(x.id, Math.max(1, Math.round(x.peso / 2))); }
  if (r.preset === 'piccola') { s.opzioni.immagine.modo = 'qualita'; s.opzioni.immagine.lato = 1920; s.opzioni.immagine.qualita = 85; }
  s.sceltaRichiesta = null;
}

function disegnaScegliVera() {
  sistemaAttiva();
  applicaRichiesta();
  if (!s.attiva) {
    banco.replaceChildren(disegnaHome());
    return;
  }
  const cc = categoriePresenti();
  const att = s.attiva;
  const scelto = sceltaPer(att);
  const fmt = s.stato.formati.filter((f) => f.categoria === att && fileDi(att).some((x) => x.formati.includes(f.id)));

  const schede = cc.length > 1
    ? h('div.schede-cat', null, ...cc.map((c) => h(`button.scheda-cat${c === att ? '.su' : ''}`, { style: { '--tinta': coloreCategoria[c] }, onclick: () => { clac(); s.attiva = c; s.fileAttivo = null; s.piano = null; disegnaTutto(); } },
      h('i.led'), h('span', { html: iconaCategoria[c] }), nomeCat(c), h('small', null, String(fileDi(c).length)))))
    : null;

  const griglia = h('div.cassette', null, ...fmt.map((f) => h(`button.cassetta${scelto?.id === f.id ? '.scelta' : ''}${f.manca ? '.manca' : ''}`,
    { style: { '--tinta': coloreCategoria[att] }, title: f.manca ?? f.descrizione, disabled: !!f.manca && f.manca.includes('FFmpeg'),
      onclick: () => {
        if (f.manca) { mostraAvviso(f.manca); return; }
        clac(); s.scelta[att] = f.id; s.piano = null; disegnaTutto();
      } },
    h('span.striscia'),
    h('span.etichetta', null, f.etichetta),
    h('span.descr', null, f.descrizione),
    h('i.led'),
  )));

  const nomiFile = s.ordine.map((i) => s.file.get(i)?.nome).filter(Boolean) as string[];
  const testaCompatta = app.classList.contains('compatto')
    ? h('div.compatto-testa', null,
        h('div', null, h('b', null, nomiFile.length > 1 ? `${nomiFile.length} file` : (nomiFile[0] ?? '')), h('small', null, 'mini interfaccia: scegli tutto qui')),
        h('div.compatto-tasti', null,
          h('button.tasto', { title: 'Torna a «Cosa vuoi farne?»', onclick: () => void tornaIndietro() }, h('span', { html: icone.freccia, style: 'display:inline-block;transform:scaleX(-1)' }), 'Indietro'),
          h('button.tasto', { title: 'Apre la finestra intera, con la lista dei file e l\'anteprima', onclick: () => { clac(); void chiedi('finestra', { azione: 'completa' }); } }, h('span', { html: icone.ingrandisci }), 'Apri nell\'app intera')))
    : null;
  banco.replaceChildren(
    ...[testaCompatta, invitoMenu11()].filter((x): x is HTMLElement => !!x),
    schede ?? h('div'),
    h('div.titolo-sezione', null, h('span', null, 'IN COSA LO TRASFORMO?'), h('span.conta', null, nomeCat(att))),
    griglia,
    regolazioni(att, scelto),
  );
  if (eVideoVero(scelto) && s.opzioni.video.modo !== 'copia') aggiornaPiano();
}

// ————————————————————————————————— piede —————————————————————————————————

function nomeUscita(f: InfoFile, fmt: Formato) {
  const radice = f.eDirectory ? f.nome : f.nome.replace(/(\.tar)?\.[^.]+$/i, '');
  if (fmt.id === 'arch.estrai') return `${radice}\\`;
  const scritta = s.stato.impostazioni.scritta?.trim();
  return `${radice}${scritta ? ` (${scritta})` : ''}${fmt.estensione || '\\'}`;
}

function disegnaPiede() {
  if (s.vista === 'scegli') {
    const elementi = elementiDaConvertire();
    const n = elementi.reduce((a, e) => a + (formato(e.formato)?.unisce ? 1 : e.ids.length), 0);
    let dest = '';
    if (n === 1) {
      const e = elementi[0];
      dest = nomeUscita(s.file.get(e.ids[0])!, formato(e.formato)!);
    } else if (n > 1) dest = `${n} conversioni, ognuna accanto al suo originale`;
    // l'originale nel Cestino: si dice prima di premere, non dopo
    const cestino = s.stato.impostazioni.cestinoDaSolo && elementi.some((e) => formato(e.formato)?.sostituisce);
    piede.replaceChildren(
      h('div.destinazione', null,
        n ? h('span.freccia', { html: icone.freccia }) : null,
        h('span.dest-nome', { title: dest }, n ? dest : 'Aggiungi qualcosa da convertire'),
        n === 1 ? h('span.dest-dove', null, 'nella stessa cartella') : null,
        n && cestino ? h('span.dest-cestino', { title: 'Si cambia in Impostazioni → L\'originale nel Cestino da solo' }, h('span', { html: icone.cestino }), n > 1 ? 'originali nel Cestino' : 'originale nel Cestino') : null),
      h('button.converti', { disabled: !n, onclick: () => void converti() }, h('i.led'), h('span', null, 'CONVERTI'), n > 1 ? h('small', null, String(n)) : null),
    );
  } else if (s.vista === 'lavoro') {
    piede.replaceChildren(
      h('div.destinazione', null, h('span.dest-nome#piede-stato', null, statoGiro())),
      h('button.annulla', { onclick: () => { clac(true); void chiedi('annulla', {}); } }, h('span', { html: icone.ferma }), 'ANNULLA'),
    );
  } else {
    // più originali da buttare: un tasto solo per tutti (con la conferma al secondo clic)
    const daButtare = s.giro.map((i) => s.lavori.get(i)!).filter((l) => l && l.stato === 'fatto' && !l.nelCestino && l.sorgenti.length === 1);
    piede.replaceChildren(...[
      h('div.destinazione', null, h('span.dest-nome', null, 'Fatto. I file sono accanto agli originali.')),
      daButtare.length > 1 ? tastoCestino('ORIGINALI NEL CESTINO', daButtare, 'secondario') : null,
      h('button.secondario', { onclick: () => { clac(); nuovoGiro(); } }, h('span', { html: icone.indietro }), 'CONVERTI ALTRO'),
      h('button.secondario', { onclick: () => chiedi('finestra', { azione: 'chiudi' }) }, 'CHIUDI'),
    ].filter((x): x is HTMLElement => !!x));
  }
}

function elementiDaConvertire() {
  const el: { ids: string[]; formato: string; opzioni: Opzioni }[] = [];
  for (const c of categoriePresenti()) {
    const f = sceltaPer(c);
    if (!f || f.manca) continue;
    const ids = fileDi(c).filter((x) => x.formati.includes(f.id)).map((x) => x.id);
    if (!ids.length) continue;
    if (f.unisce) el.push({ ids, formato: f.id, opzioni: structuredClone(s.opzioni) });
    else for (const id of ids) {
      const o = structuredClone(s.opzioni);
      if (o.video.modo === 'auto') o.video.modo = 'peso';
      o.video.pesoByte = pesoObiettivo(id);
      // la foto: se si è scelto il peso, il motore cerca la qualità più alta che ci sta (stessa ricerca della stima)
      const peso = c === 'immagine' && conQualita(f) && o.immagine.modo === 'peso' ? pesoVolutoFoto(id) : 0;
      o.immagine.pesoMaxByte = peso;
      if (peso) o.immagine.qualita = QUALITA_PER_PESO;
      el.push({ ids: [id], formato: f.id, opzioni: o });
    }
  }
  return el;
}

async function converti() {
  const elementi = elementiDaConvertire();
  if (!elementi.length) return;
  clac(true);
  for (const c of categoriePresenti()) { const f = sceltaPer(c); if (f) s.stato.impostazioni.formati[c] = f.id; }
  void chiedi('impostazioni', { formati: s.stato.impostazioni.formati, scelte: { video: { ...s.opzioni.video, pesoByte: 0 }, audio: s.opzioni.audio, immagine: s.opzioni.immagine, pdf: s.opzioni.pdf } });
  const ids = await chiedi<string[]>('converti', { elementi });
  s.giro = ids;
  s.vista = 'lavoro';
  disegnaTutto();
}

function nuovoGiro() {
  // si tolgono i file già convertiti, restano quelli andati storti
  const ok = new Set([...s.lavori.values()].filter((l) => s.giro.includes(l.id) && l.stato === 'fatto').map((l) => l.fileId));
  for (const id of ok) if (id) { s.file.delete(id); s.ordine = s.ordine.filter((x) => x !== id); void chiedi('togli', { id }); }
  s.giro = [];
  s.vista = 'scegli';
  s.piano = null;
  void chiedi('pulisci');
  disegnaTutto();
}

// ————————————————————————————————— tutto insieme —————————————————————————————————

function disegnaTutto() {
  if (s.stato.modo === 'rapido') { disegnaTesta(); disegnaRapido(); return; }
  app.classList.toggle('lavora', s.vista !== 'scegli');
  disegnaTesta();
  disegnaCoda();
  if (s.vista === 'scegli') disegnaScegli();
  else if (s.vista === 'lavoro') disegnaLavoro();
  else disegnaFatto();
  disegnaPiede();
  aggiornaAnteprima();
}

function opzioniSalvate() {
  const sc = s.stato.impostazioni.scelte as Partial<Opzioni> | null | undefined;
  if (!sc) return;
  for (const k of ['video', 'audio', 'immagine', 'pdf'] as const) if (sc[k]) Object.assign(s.opzioni[k], sc[k]);
  if (s.opzioni.video.modo === 'auto') s.opzioni.video.modo = 'peso';
}

async function avvia() {
  Object.assign(ui, { tutto: disegnaTutto, scegli: disegnaScegli, piede: disegnaPiede, coda: disegnaCoda, fatto: disegnaFatto, rapido: disegnaRapido, impostazioni: apriImpostazioni, nuovoGiro });
  await avviaFinto();
  avviaIncolla();
  ascolta('file', (f: InfoFile) => {
    if (!s.file.has(f.id)) s.ordine.push(f.id);
    s.file.set(f.id, f);
    // arriva un file nuovo (dal lettore, dal tasto destro) mentre la piastra dice «FATTO»: si riparte con lui
    if (s.vista === 'fatto' && s.stato.modo === 'finestra') { s.attiva = f.categoria; s.fileAttivo = f.id; nuovoGiro(); return; }
    if (s.vista !== 'scegli' && s.stato.modo === 'finestra') { disegnaCoda(); return; }
    if (!s.attiva) s.attiva = f.categoria;
    disegnaTutto();
  });
  ascolta('dettagli', (d: { id: string; dettagli: any }) => {
    const f = s.file.get(d.id);
    if (!f) return;
    f.dettagli = d.dettagli;
    if (s.stato.modo === 'finestra') { disegnaCoda(); if (d.id === s.fileAttivo) { s.piano = null; if (s.vista === 'scegli') disegnaScegli(); } }
  });
  ascolta('anteprima', (d: { id: string; url: string }) => {
    const f = s.file.get(d.id);
    if (!f) return;
    f.anteprima = d.url;
    if (s.stato.modo === 'finestra') disegnaCoda();
  });
  ascolta('lavoro', (l: Lavoro) => {
    s.lavori.set(l.id, l);
    if (s.stato.modo === 'rapido') {
      if (!s.giro.includes(l.id)) s.giro.push(l.id);
      disegnaRapido();
      return;
    }
    if (!s.giro.includes(l.id) && s.vista !== 'scegli') s.giro.push(l.id);
    if (s.vista === 'lavoro') aggiornaLavoro();
    else if (s.vista === 'fatto' && s.giro.includes(l.id)) { disegnaFatto(); disegnaPiede(); }
  });
  ascolta('aggiornamento', (a: { versione: string }) => { s.aggiornamento = a.versione; if (s.stato.modo === 'finestra') disegnaTesta(); });
  ascolta('hardware', (hw) => {
    if (!hw) return;
    s.stato.hardware = hw;
    disegnaTesta();
    if (s.vista === 'scegli' && s.stato.modo === 'finestra') { s.piano = null; disegnaScegli(); }
  });
  ascolta('carico', (c: Carico) => {
    s.carico = c;
    strumenti.cpu?.valore(c.cpu / 100);
    strumenti.enc?.valore(c.encoder / 100);
    strumenti.dec?.valore(c.decoder / 100);
  });
  ascolta('avatar', (d: { si: boolean }) => {
    s.stato.impostazioni.avatarAttivo = d.si;
    disegnaTesta();
    if (document.querySelector('.velo.impostazioni')) apriImpostazioni();
  });
  ascolta('compatto', (d: { si: boolean; scelta?: { formato: string; preset?: string | null } | null }) => {
    app.classList.toggle('compatto', d.si);
    s.sceltaRichiesta = d.scelta ?? null;
    s.piano = null;
    disegnaTutto();
  });
  ascolta('modo', (m: { modo: 'finestra' | 'rapido' }) => {
    s.stato.modo = m.modo;
    app.classList.toggle('rapido', m.modo === 'rapido');
    if (m.modo === 'finestra') {
      app.replaceChildren(testa, corpo, piede);
      s.vista = s.giro.length && [...s.lavori.values()].some((l) => s.giro.includes(l.id) && (l.stato === 'corre' || l.stato === 'attesa')) ? 'lavoro' : s.giro.length ? 'fatto' : 'scegli';
    }
    disegnaTutto();
  });

  s.stato = await chiedi<Stato>('stato');
  s.aggiornamento = s.stato.aggiornamento ?? null;
  accendiSuoni(s.stato.impostazioni.suoniAccesi);
  // dopo un aggiornamento le novità si aprono da sole, una volta
  if (s.stato.modo === 'finestra') void chiedi<DatiNovita>('novita').then((n) => { if (n?.nuova) setTimeout(() => void apriNovita(n), 1200); }).catch(() => {});
  opzioniSalvate();
  for (const f of s.stato.file) { s.file.set(f.id, f); s.ordine.push(f.id); }
  for (const l of s.stato.lavori) { s.lavori.set(l.id, l); s.giro.push(l.id); }
  app.classList.toggle('rapido', s.stato.modo === 'rapido');
  app.classList.toggle('compatto', !!s.stato.compatto);
  s.sceltaRichiesta = s.stato.scelta ?? null;
  if (s.stato.modo === 'finestra' && s.giro.length) s.vista = 'lavoro';
  document.body.classList.toggle('nel-browser', !dentroApp);
  disegnaTutto();
  // trascina i file dentro: WebView2 passa i percorsi veri a C#
  document.addEventListener('dragover', (e) => { e.preventDefault(); app.classList.add('sopra'); });
  document.addEventListener('dragleave', (e) => { if (!e.relatedTarget) app.classList.remove('sopra'); });
  document.addEventListener('drop', (e) => {
    e.preventDefault();
    app.classList.remove('sopra');
    const w = (window as any).chrome?.webview;
    if (w?.postMessageWithAdditionalObjects && e.dataTransfer?.files.length) w.postMessageWithAdditionalObjects({ cmd: 'lasciati' }, e.dataTransfer.files);
  });
  document.addEventListener('keydown', (e) => {
    // Esc chiude quello sopra (il velo più in alto), non quello sotto
    if (e.key === 'Escape') [...document.querySelectorAll('.velo')].pop()?.remove();
    // Canc toglie il file scelto dalla coda; Ctrl+Canc la svuota. I file sul disco non si toccano.
    // (Non dall'anteprima a destra: lì Canc è sul testo che stai leggendo.)
    const nellAnteprima = e.target instanceof Element && !!e.target.closest('.anteprima');
    if (e.key === 'Delete' && s.vista === 'scegli' && !nellAnteprima && !document.querySelector('.velo') && !(e.target instanceof HTMLInputElement)) {
      if (e.ctrlKey) svuota(); else if (s.fileAttivo) togli(s.fileAttivo);
    }
    // Invio converte solo se non sei su un tasto o su un file della coda (lì Invio sceglie); Ctrl+Invio converte da qualunque punto
    const suUnComando = e.target instanceof Element && !!e.target.closest('button, a, select, textarea, input, [role="option"], [role="slider"]');
    if (e.key === 'Enter' && (!suUnComando || (e.ctrlKey && !(e.target instanceof HTMLTextAreaElement))) && s.vista === 'scegli' && !nellAnteprima && !document.querySelector('.velo') && !(e.target instanceof HTMLInputElement && !e.ctrlKey)) void converti();
  });
  // le frecce passano da un file all'altro, da una categoria all'altra, da un formato all'altro (e il fuoco resta dov'è dopo il ridisegno)
  app.addEventListener('keydown', (e) => {
    if (e.altKey || e.ctrlKey || e.metaKey || e.shiftKey || !(e.target instanceof HTMLElement) || document.querySelector('.velo')) return;
    const gruppo = e.target.closest<HTMLElement>('.cassette, .schede-cat, .bottoni, .lista');
    if (!gruppo) return;
    const dellaLista = gruppo.classList.contains('lista');
    const voci = [...gruppo.querySelectorAll<HTMLElement>(dellaLista ? '.scheda' : 'button:not(:disabled)')];
    const i = voci.indexOf(e.target);
    if (i < 0) return;
    const avanti = e.key === 'ArrowDown' || (!dellaLista && e.key === 'ArrowRight');
    const indietro = e.key === 'ArrowUp' || (!dellaLista && e.key === 'ArrowLeft');
    const j = avanti ? Math.min(voci.length - 1, i + 1) : indietro ? Math.max(0, i - 1) : e.key === 'Home' ? 0 : e.key === 'End' ? voci.length - 1 : -1;
    if (j < 0) return;
    e.preventDefault();
    voci[j].focus();
    if (dellaLista && j !== i) voci[j].click();
  });
  void chiedi('pronto');
}

void avvia();
