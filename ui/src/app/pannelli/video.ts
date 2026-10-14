// Il pannello del video: peso finale (o qualità, o copia veloce), codec, risoluzione, motore. Il peso e il giudizio
// vengono dal piano che calcola il motore (PianoVideo.cs).

import { chiedi } from '../../ponte';
import type { Formato, Piano } from '../../ponte';
import { h, peso, pesoDisplay, numero, clamp, MB, GB } from '../../util';
import { icone } from '../../icone';
import { Lancetta } from '../../grafica';
import { clac } from '../../suoni';
import { s, ui, fileDi, sceltaPer, eVideoVero } from '../stato';
import { gruppo, bottoni, cursore, faderPeso, mostraPeso } from '../controlli';

let lancettaQualita: Lancetta | null = null;

export const presetPeso = [
  { v: 10 * MB, testo: '10 MB', nota: 'Discord' },
  { v: 25 * MB, testo: '25 MB', nota: 'Email' },
  { v: 100 * MB, testo: '100 MB', nota: 'WhatsApp' },
  { v: 500 * MB, testo: '500 MB', nota: 'Telegram' },
  { v: 2 * GB, testo: '2 GB', nota: 'WeTransfer' },
  { v: 4 * GB - 1, testo: '4 GB', nota: 'Chiavetta FAT32' },
  // il DVD da «4,7 GB» conta a 1000: sono 4,37 GB come li conta Windows
  { v: 4.7e9, testo: '4,37 GB', nota: 'DVD' },
  { v: 25e9, testo: '23,3 GB', nota: 'Blu-ray' },
];

export function pesoObiettivo(id: string | null) {
  if (!id) return 0;
  const f = s.file.get(id)!;
  return s.pesoPerFile.get(id) ?? Math.round(f.peso / 2);
}

export function regolazioniVideo(f: Formato) {
  const o = s.opzioni.video;
  const fa = s.fileAttivo ? s.file.get(s.fileAttivo) : undefined;
  const p = s.piano;
  const quanti = s.attiva ? fileDi(s.attiva).length : 1;
  const modo = o.modo === 'auto' ? 'peso' : o.modo;

  const modi = h('div.modi', null,
    ...([
      { v: 'peso', testo: 'PESO FINALE', nota: 'deve starci' },
      { v: 'qualita', testo: 'QUALITÀ', nota: 'il peso viene da sé' },
      { v: 'copia', testo: 'VELOCE', nota: 'cambia solo la scatola', spento: p ? !p.puoCopiare : false },
    ] as const).map((x) => h(`button.modo${modo === x.v ? '.su' : ''}`, {
      disabled: 'spento' in x && x.spento, title: 'spento' in x && x.spento ? 'Questo video non si può copiare così com\'è in questo formato' : x.nota,
      onclick: () => { clac(); o.modo = x.v; s.piano = null; ui.scegli(); ui.piede(); },
    }, h('i.led'), x.testo, h('small', null, x.nota))));

  const quadro = h('div.quadro');
  if (modo === 'copia') {
    quadro.append(h('div.copia', null, h('span.fulmine', { html: icone.fulmine }), h('div', null, h('b', null, 'Niente ricodifica.'), ' Il video e l\'audio passano tali e quali in un contenitore nuovo: ci mette pochi secondi e la qualità resta identica.')));
  } else {
    // display a sette segmenti + strumento a lancetta
    const valore = modo === 'peso' ? pesoObiettivo(fa?.id ?? null) : p?.pesoStimato ?? 0;
    const dsp = pesoDisplay(valore || 0);
    const display = h('div.display', null,
      h('div.display-etichetta', null, modo === 'peso' ? (quanti > 1 ? 'PESO FINALE · questo video' : 'PESO FINALE') : 'PESO STIMATO'),
      h('div.sette', null, h('span.spenti', null, '88888'), h('span.accesi', { id: 'dsp-numero' }, modo === 'qualita' ? `~${dsp.numero}` : dsp.numero)),
      h('div.display-unita', { id: 'dsp-unita' }, dsp.unita),
      h('div.display-sotto', { id: 'dsp-sotto' }, fa ? confrontoPeso(valore, fa.peso) : ''),
    );
    const vu = h('canvas.vu-qualita') as HTMLCanvasElement;
    const strumento = h('div.strumento', null, vu, h('div.giudizio', { id: 'giudizio' }, p?.giudizio ?? '…'));
    quadro.append(display, strumento);
    requestAnimationFrame(() => {
      lancettaQualita = new Lancetta(vu, { etichetta: 'QUALITÀ', rossoDa: 0, rossoA: 0.25,
        tacche: [{ v: 0.02, testo: 'BUTTARE' }, { v: 0.3, testo: 'BASSA' }, { v: 0.55, testo: 'BUONA' }, { v: 0.8, testo: 'OTTIMA' }, { v: 0.98, testo: 'ORIG.' }] });
      if (p) lancettaQualita.valore(p.punteggio);
    });
  }

  const comandi = h('div.comandi');
  if (modo === 'peso' && fa) {
    const min = p?.pesoMin ?? Math.max(1e6, fa.peso / 200);
    const max = p?.pesoMax ?? fa.peso;
    const valore = clamp(pesoObiettivo(fa.id), min, max);
    const fader = faderPeso(min, max, valore, (b) => {
      s.pesoPerFile.set(fa.id, b);
      mostraPeso(b, confrontoPeso(b, fa.peso));
      document.querySelectorAll('.preset .tasto.su').forEach((e) => e.classList.remove('su'));
      aggiornaPiano(true);
    }, 'originale ' + peso(max, 1));
    const chips = presetPeso.filter((x) => x.v >= min && x.v < max * 0.98);
    comandi.append(
      fader,
      h('div.bottoni.preset', null,
        ...[{ v: Math.round(fa.peso / 2), testo: 'Metà', nota: '½' }, { v: Math.round(fa.peso / 4), testo: 'Un quarto', nota: '¼' }, ...chips]
          .filter((x) => x.v >= min)
          .map((x) => h(`button.tasto.piccolo${Math.abs(valore - x.v) < x.v * 0.01 ? '.su' : ''}`, { onclick: () => { clac(); s.pesoPerFile.set(fa.id, x.v); s.piano = null; ui.scegli(); } }, x.testo, h('small', null, x.nota)))),
    );
  } else if (modo === 'qualita') {
    comandi.append(cursore(0, 100, o.qualita, (v) => { o.qualita = v; aggiornaPiano(true); }, ['più leggero', 'perfetto']));
  }

  const ammessi = p?.codecAmmessi ?? (f.estensione === '.webm' ? ['av1', 'vp9'] : f.estensione === '.mov' ? ['h264', 'hevc', 'prores'] : ['h264', 'hevc', 'av1']);
  const codecAttuale = ammessi.includes(o.codec) ? o.codec : ammessi[0];
  const nomiCodec: Record<string, [string, string]> = { h264: ['H.264', 'va ovunque'], hevc: ['H.265', 'metà peso'], av1: ['AV1', 'il più efficiente'], vp9: ['VP9', 'web'], prores: ['ProRes', 'montaggio'] };
  const hw = s.stato.hardware;
  const haNvidia = !!hw?.encoder.some((e) => e.endsWith('_nvenc'));
  const haAmd = !!hw?.encoder.some((e) => e.endsWith('_amf'));
  const haIntel = !!hw?.encoder.some((e) => e.endsWith('_qsv'));
  const latoOrig = fa?.dettagli?.larghezza ? Math.min(fa.dettagli.larghezza, fa.dettagli.altezza ?? 0) : 2160;

  const dettagliTecnici = modo !== 'copia'
    ? gruppo('REGOLAZIONI',
        h('div.fila-regolazioni', null,
          h('div.sotto-gruppo', null, h('label', null, 'CODEC'), bottoni(ammessi.map((c) => ({ v: c, testo: nomiCodec[c][0], nota: nomiCodec[c][1] })), codecAttuale, (v) => { o.codec = v; s.piano = null; })),
          h('div.sotto-gruppo', null, h('label', null, 'RISOLUZIONE'), bottoni([0, 2160, 1440, 1080, 720, 480].filter((v) => v === 0 || v <= latoOrig).map((v) => ({ v, testo: v ? (v === 2160 ? '4K' : `${v}p`) : 'Auto' })), o.lato, (v) => { o.lato = v; s.piano = null; })),
          h('div.sotto-gruppo', null, h('label', null, 'MOTORE'), bottoni([
            { v: 'auto', testo: 'Auto' },
            ...(haNvidia ? [{ v: 'nvidia', testo: 'NVIDIA' }] : []),
            ...(haAmd ? [{ v: 'amd', testo: 'AMD' }] : []),
            ...(haIntel ? [{ v: 'intel', testo: 'Intel' }] : []),
            { v: 'cpu', testo: 'CPU', nota: 'più lenta' },
          ], o.motore, (v) => { o.motore = v; s.piano = null; })),
          h('div.sotto-gruppo', null, h('label', null, 'AUDIO'), bottoni([{ v: 'si', testo: 'Tienilo' }, { v: 'no', testo: 'Toglilo' }], o.senzaAudio ? 'no' : 'si', (v) => { o.senzaAudio = v === 'no'; s.piano = null; })),
        ))
    : null;

  const note = h('div.note', { id: 'note-piano' });
  riempiNote(note);

  return h('div.regolazioni.video', null, modi, h('div.pannello-peso', null, quadro, comandi), dettagliTecnici, note);
}

export function confrontoPeso(b: number, orig: number) {
  if (!orig) return '';
  const r = (b / orig - 1) * 100;
  return `originale ${peso(orig)} · ${r <= 0 ? '−' : '+'}${numero(Math.abs(r))}%`;
}

export function riempiNote(el: HTMLElement) {
  const p = s.piano;
  if (!p) { el.replaceChildren(); return; }
  const encoder = p.encoder.endsWith('_nvenc') ? `NVENC ${p.codec.toUpperCase()}` : p.encoder.endsWith('_amf') ? 'AMD AMF' : p.encoder.endsWith('_qsv') ? 'Intel Quick Sync' : p.encoder === 'copy' ? 'copia' : `CPU · ${p.encoder}`;
  const righe: (string | Node)[] = [
    h('span.nota-tec', null, h('i', { html: p.hardware ? icone.fulmine : icone.chip }), `${encoder} · ${p.larghezza}×${p.altezza}${p.modo === 'peso' ? ` · ${numero(p.bitrateVideo / 1e6, 1)} Mbit/s` : ''}${p.audioKbps ? ` · audio ${p.audioKbps} kbit/s` : ''}`),
    ...p.note.map((n) => h('span.nota', null, n)),
  ];
  el.replaceChildren(...righe);
}

export let pianoInVolo = false;
export let pianoDiNuovo = false;
export async function aggiornaPiano(soloNumeri = false) {
  const att = s.attiva;
  if (!att || !s.fileAttivo) return;
  const f = sceltaPer(att);
  if (!eVideoVero(f)) return;
  if (pianoInVolo) { pianoDiNuovo = true; return; }
  pianoInVolo = true;
  try {
    const o = structuredClone(s.opzioni);
    o.video.pesoByte = pesoObiettivo(s.fileAttivo);
    const p = await chiedi<Piano>('piano', { id: s.fileAttivo, formato: f!.id, opzioni: o });
    const primo = !s.piano;
    s.piano = p;
    if (primo && !soloNumeri) { ui.scegli(); return; }
    lancettaQualita?.valore(p.punteggio);
    const g = document.getElementById('giudizio');
    if (g) { g.textContent = p.giudizio; g.dataset.livello = p.punteggio >= 0.72 ? 'alto' : p.punteggio >= 0.38 ? 'medio' : 'basso'; }
    if (s.opzioni.video.modo === 'qualita') {
      const d = pesoDisplay(p.pesoStimato);
      const n = document.getElementById('dsp-numero');
      if (n) n.textContent = `~${d.numero}`;
      const u = document.getElementById('dsp-unita');
      if (u) u.textContent = d.unita;
      const fa = s.file.get(s.fileAttivo!);
      const sotto = document.getElementById('dsp-sotto');
      if (sotto && fa) sotto.textContent = confrontoPeso(p.pesoStimato, fa.peso);
    }
    const note = document.getElementById('note-piano');
    if (note) riempiNote(note);
  } catch (e) {
    console.warn(e);
  } finally {
    pianoInVolo = false;
    if (pianoDiNuovo) { pianoDiNuovo = false; void aggiornaPiano(true); }
  }
}
