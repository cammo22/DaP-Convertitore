// Il pannello della foto: come per il video, o si dice quanto deve pesare («Peso finale», e la qualità viene da sé)
// o si sceglie la qualità e si legge quanto peserà. Il peso non è un'indovinata: il motore codifica davvero la foto
// (StimaImmagini.cs) e dice anche a che qualità ci si arriva e se la foto va rimpicciolita per starci.

import { chiedi } from '../../ponte';
import type { Formato, InfoFile, StimaImmagine } from '../../ponte';
import { h, peso, clamp, MB, KB } from '../../util';
import { Lancetta } from '../../grafica';
import { clac } from '../../suoni';
import { s, ui } from '../stato';
import { gruppo, bottoni, cursore, faderPeso, mostraPeso } from '../controlli';
import { confrontoPeso } from './video';

/** Le foto con la qualità regolabile (le altre — PNG, BMP, TIFF… — si scrivono e basta). */
export const conQualita = (f: Formato) => ['.jpg', '.webp', '.avif', '.jxl', '.heic'].includes(f.estensione);


/** La qualità massima quando si chiede un peso (come StimaImmagini.QualitaPerPeso nel motore). */
export const QUALITA_PER_PESO = 95;

/** Il peso voluto per questa foto, se è stato scelto (0 = non scelto: vale la qualità di partenza). */
export const pesoVolutoFoto = (id: string): number => s.pesoPerFile.get(id) ?? 0;

let lancetta: Lancetta | null = null;
let timer: number | undefined;
let numeroRichiesta = 0;

const giudizio = (q: number, scala: number) =>
  scala < 0.97 ? 'Va rimpicciolita' : q >= 92 ? 'Identica a occhio' : q >= 82 ? 'Ottima' : q >= 70 ? 'Buona' : q >= 55 ? 'Si nota un po\'' : 'Molto compressa';

/** Da qualità e scala a un numero fra 0 e 1 per la lancetta. */
const punteggio = (q: number, scala: number) => clamp((q - 30) / 68, 0, 1) * clamp(scala, 0.45, 1);

export function regolazioniImmagine(f: Formato): Node {
  const o = s.opzioni.immagine;
  const lato = gruppo('LATO LUNGO', bottoni([0, 3840, 2560, 1920, 1280, 800].map((v) => ({ v, testo: v ? `${v}` : 'Originale', nota: v ? 'px' : undefined })), o.lato, (v) => { o.lato = v; s.stimaFoto = null; }));
  const spiega = h('p.spiega', null, 'La foto si gira da sola come la vede il telefono. La posizione GPS e i dati nascosti non passano; la data del file sì.');
  const fa = s.fileAttivo ? s.file.get(s.fileAttivo) : undefined;
  if (!conQualita(f) || !fa) return h('div.regolazioni', null, lato, spiega);

  const chiave = `${fa.id}|${f.id}|${o.lato}`;
  const st = s.stimaFoto?.chiave === chiave ? s.stimaFoto : null;
  if (!st) chiediStima(fa, f, true);
  const modo = o.modo === 'qualita' ? 'qualita' : 'peso';
  const quanti = s.attiva ? [...s.file.values()].filter((x) => x.categoria === s.attiva).length : 1;

  const modi = h('div.modi.due', null,
    ...([
      { v: 'peso', testo: 'PESO FINALE', nota: 'deve starci' },
      { v: 'qualita', testo: 'QUALITÀ', nota: 'il peso viene da sé' },
    ] as const).map((x) => h(`button.modo${modo === x.v ? '.su' : ''}`, {
      title: x.nota,
      onclick: () => { clac(); o.modo = x.v; s.stimaFoto = null; ui.scegli(); ui.piede(); },
    }, h('i.led'), x.testo, h('small', null, x.nota))));

  const bersaglio = pesoVolutoFoto(fa.id) || st?.peso || 0;
  const valore = modo === 'peso' ? bersaglio : st?.peso ?? 0;
  const display = h('div.display', null,
    h('div.display-etichetta', null, modo === 'peso' ? (quanti > 1 ? 'PESO FINALE · questa foto' : 'PESO FINALE') : 'PESO STIMATO'),
    h('div.sette', null, h('span.spenti', null, '88888'), h('span.accesi', { id: 'dsp-numero' }, '')),
    h('div.display-unita', { id: 'dsp-unita' }, ''),
    h('div.display-sotto', { id: 'dsp-sotto' }, ''));
  const vu = h('canvas.vu-qualita') as HTMLCanvasElement;
  const strumento = h('div.strumento', null, vu, h('div.giudizio', { id: 'giudizio' }, '…'));
  const quadro = h('div.quadro', null, display, strumento);
  requestAnimationFrame(() => {
    lancetta = new Lancetta(vu, { etichetta: 'QUALITÀ', rossoDa: 0, rossoA: 0.25,
      tacche: [{ v: 0.02, testo: 'BUTTARE' }, { v: 0.3, testo: 'BASSA' }, { v: 0.55, testo: 'BUONA' }, { v: 0.8, testo: 'OTTIMA' }, { v: 0.98, testo: 'ORIG.' }] });
    if (st) mostra(st, fa, modo);
  });
  if (st && valore) requestAnimationFrame(() => mostraPeso(valore, confrontoPeso(valore, fa.peso), modo === 'qualita' && !st.esatta));

  const comandi = h('div.comandi');
  if (st && modo === 'peso') {
    const min = st.min, max = Math.max(st.max, st.min * 1.5);
    const v = clamp(bersaglio, min, max);
    const preset = [
      { v: Math.round(fa.peso / 2), testo: 'Metà', nota: '½' },
      { v: Math.round(fa.peso / 4), testo: 'Un quarto', nota: '¼' },
      ...[100 * KB, 300 * KB, 500 * KB, 1 * MB, 2 * MB, 5 * MB].map((b) => ({ v: b, testo: peso(b, 0), nota: b === 300 * KB ? 'chat' : b === 1 * MB ? 'email' : '' })),
    ].filter((x, i, a) => x.v >= min && x.v <= max * 0.98 && a.findIndex((y) => Math.abs(y.v - x.v) < x.v * 0.05) === i);
    comandi.append(
      faderPeso(min, max, v, (b) => {
        s.pesoPerFile.set(fa.id, b);
        mostraPeso(b, confrontoPeso(b, fa.peso));
        document.querySelectorAll('.preset .tasto.su').forEach((e) => e.classList.remove('su'));
        chiediStima(fa, f, false);
      }, 'qualità massima ' + peso(max, 1)),
      h('div.bottoni.preset', null, ...preset.map((x) =>
        h(`button.tasto.piccolo${Math.abs(v - x.v) < x.v * 0.01 ? '.su' : ''}`, { onclick: () => { clac(); s.pesoPerFile.set(fa.id, x.v); ui.scegli(); chiediStima(fa, f, false, true); } }, x.testo, x.nota ? h('small', null, x.nota) : null))),
    );
  } else if (modo === 'qualita') {
    comandi.append(cursore(30, 100, o.qualita, (q) => { o.qualita = q; chiediStima(fa, f, false); }, ['più leggera', 'perfetta']));
  } else comandi.append(h('div.fader-box', null, h('p.spiega', null, 'Guardo la foto…')));

  const note = h('div.note', { id: 'note-piano' });
  if (st) scriviNote(note, st);
  return h('div.regolazioni.video', null, modi, h('div.pannello-peso', null, quadro, comandi), lato, note, spiega);
}

/** Chiede al motore quanto pesa (o a che qualità ci si arriva): una richiesta ogni tanto, la nuova vince sulla vecchia. */
function chiediStima(fa: InfoFile, f: Formato, primaVolta: boolean, subito = false) {
  clearTimeout(timer);
  const vai = async () => {
    const mia = ++numeroRichiesta;
    const o = s.opzioni.immagine;
    const modo = o.modo === 'qualita' ? 'qualita' : 'peso';
    const voluto = modo === 'peso' ? pesoVolutoFoto(fa.id) : 0;
    try {
      const r = await chiedi<StimaImmagine>('stimaImmagine', { id: fa.id, formato: f.id, lato: o.lato, qualita: o.qualita, pesoMax: voluto, intervallo: primaVolta });
      if (mia !== numeroRichiesta || s.fileAttivo !== fa.id) return;
      const chiave = `${fa.id}|${f.id}|${o.lato}`;
      const vecchia = s.stimaFoto?.chiave === chiave ? s.stimaFoto : null;
      s.stimaFoto = { chiave, ...r, min: r.min || vecchia?.min || 0, max: r.max || vecchia?.max || 0 };
      if (primaVolta) {
        // il peso di partenza è quello che la qualità solita dà: cambia solo se lo sposti tu
        if (modo === 'peso' && !pesoVolutoFoto(fa.id)) s.pesoPerFile.set(fa.id, clamp(r.peso, s.stimaFoto.min, Math.max(s.stimaFoto.max, s.stimaFoto.min * 1.5)));
        ui.scegli();
        return;
      }
      mostra(s.stimaFoto, fa, modo);
    } catch { /* superata da una più nuova, o una foto che non si legge: il pannello resta com'è */ }
  };
  if (subito || primaVolta) void vai(); else timer = window.setTimeout(vai, 220);
}

/** Mette a posto lancetta, giudizio, note e (in modo qualità) il numero del peso. */
function mostra(st: StimaImmagine, fa: InfoFile, modo: 'peso' | 'qualita') {
  const p = punteggio(st.qualita, st.scala);
  lancetta?.valore(p);
  const g = document.getElementById('giudizio');
  if (g) { g.textContent = giudizio(st.qualita, st.scala); g.dataset.livello = p >= 0.72 ? 'alto' : p >= 0.38 ? 'medio' : 'basso'; }
  if (modo === 'qualita') mostraPeso(st.peso, confrontoPeso(st.peso, fa.peso), !st.esatta);
  const note = document.getElementById('note-piano');
  if (note) scriviNote(note, st);
}

function scriviNote(el: HTMLElement, st: StimaImmagine) {
  el.replaceChildren(...[
    h('span.nota-tec', null, `${st.larghezza}×${st.altezza} px · qualità ${st.qualita}`),
    st.scala < 0.97 ? h('span.nota', null, `Per stare in quel peso la foto si rimpicciolisce (${Math.round(st.scala * 100)}%): alza un po' il peso o scegli un formato più leggero.`) : null,
    !st.esatta ? h('span.nota', null, 'Il peso è una stima, fatta su un pezzo della foto.') : null,
  ].filter((x): x is HTMLElement => !!x));
}
