// I documenti. Word lo legge il lettore da sé, subito, su un foglio bianco; con Office o LibreOffice si può
// avere l'impaginato vero (diventa un PDF in cache, la prima volta ci vuole qualche secondo). Le presentazioni:
// impaginate se c'è chi lo fa, se no diapositiva per diapositiva coi loro testi.
import { chiedi } from '../ponte';
import { h } from '../util';
import { ambiente, attesa, errore, ic, memoria, type Contesto, type Tasto, type Vista } from './comune';
import { scrivania, type DatiPdf } from './pdf';

export async function monta(c: Contesto): Promise<Vista> {
  const est = c.scheda.estensione;
  const docx = ['.docx', '.docm', '.dotx'].includes(est);
  const pptx = ['.pptx', '.ppsx'].includes(est);
  if (docx && !(ambiente.office && memoria.leggi<boolean>('impaginato'))) return leggero(c);
  if (ambiente.office) return impaginato(c, docx);
  if (pptx) return diapositive(c);
  c.palco.replaceChildren(h('div.l-errore', null,
    h('b', null, 'Per questo documento serve LibreOffice'),
    h('p', null, `I file ${est.replace('.', '').toUpperCase()} li impagina LibreOffice (è gratis) o Microsoft Office. Intanto puoi aprirlo con un altro programma.`),
    h('div.l-dialogo-tasti', null, h('button.l-tasto.oro', { onclick: () => void chiedi('apriCon') }, h('span', { html: ic.apriCon }), 'Apri con…'))));
  return { tasti: [], smonta() {} };
}

/** Word letto da noi: titoli, paragrafi, grassetti, elenchi, tabelle e foto, su un foglio A4. */
async function leggero(c: Contesto): Promise<Vista> {
  c.palco.append(attesa('Leggo il documento…'));
  let html: string;
  try { html = (await chiedi<{ html: string }>('docx')).html; }
  catch (e) { c.palco.replaceChildren(errore((e as Error).message)); return { tasti: [], smonta() {} }; }
  if (!c.viva()) return { tasti: [], smonta() {} };
  const carta = h('article.c-carta', { html });
  const scorre = h('div.c-scorre', null, carta);
  let corpo = memoria.leggi<number>('carta-corpo') ?? 16;
  carta.style.fontSize = `${corpo}px`;
  const barra = h('div.p-comandi', null,
    h('span.c-nota', null, 'Lettura veloce: il testo com\'è, senza l\'impaginazione di Word'),
    ambiente.office ? h('button.l-tasto', { onclick: () => { memoria.scrivi('impaginato', true); c.palco.replaceChildren(); void impaginato(c, true).then((v) => Object.assign(vista, v)); } }, h('span', { html: ic.impaginato }), 'Impaginato vero') : null);
  c.palco.replaceChildren(h('div.c-tavolo', null, barra, scorre));
  const parole = (carta.textContent ?? '').trim().split(/\s+/).filter(Boolean).length;
  const dimensione = (d: number) => { corpo = Math.min(28, Math.max(11, corpo + d)); carta.style.fontSize = `${corpo}px`; memoria.scrivi('carta-corpo', corpo); c.hud(`Testo ${corpo} px`); };
  const vista: Vista = {
    tasti: [
      { k: ['+', '='], etichetta: '+', cosa: 'Testo più grande', fai: () => dimensione(1) },
      { k: ['-'], etichetta: '−', cosa: 'Testo più piccolo', fai: () => dimensione(-1) },
      { k: ['home'], etichetta: 'Inizio', cosa: 'All\'inizio', fai: () => scorre.scrollTo({ top: 0, behavior: 'smooth' }) },
      { k: ['end'], etichetta: 'Fine', cosa: 'Alla fine', fai: () => scorre.scrollTo({ top: scorre.scrollHeight, behavior: 'smooth' }) },
    ],
    info: () => `${parole.toLocaleString('it-IT')} parole · circa ${Math.max(1, Math.round(parole / 230))} min di lettura`,
    smonta() {},
  };
  return vista;
}

/** L'impaginato vero: Office o LibreOffice lo fanno diventare PDF, e lo si sfoglia come un PDF. */
async function impaginato(c: Contesto, docx: boolean): Promise<Vista> {
  const lento = setTimeout(() => c.stato('Impagino con ' + 'LibreOffice o Office: la prima volta ci vuole qualche secondo…'), 600);
  c.palco.append(attesa('Impagino il documento…'));
  let d: DatiPdf;
  try { d = await chiedi<DatiPdf>('impagina'); }
  catch (e) {
    clearTimeout(lento);
    c.stato(null);
    if (docx) { memoria.scrivi('impaginato', false); return leggero(c); }
    if (['.pptx', '.ppsx'].includes(c.scheda.estensione)) return diapositive(c);
    c.palco.replaceChildren(errore((e as Error).message));
    return { tasti: [], smonta() {} };
  }
  clearTimeout(lento);
  c.stato(null);
  if (!c.viva()) return { tasti: [], smonta() {} };
  const torna = docx ? h('button.l-tasto', { onclick: () => { memoria.scrivi('impaginato', false); c.palco.replaceChildren(); void leggero(c).then((v) => Object.assign(vista, v)); } }, h('span', { html: ic.libro }), 'Lettura veloce') : undefined;
  const vista = scrivania(c, d, torna);
  return vista;
}

/** Le diapositive senza PowerPoint: una carta per diapositiva, titolo e punti. */
async function diapositive(c: Contesto): Promise<Vista> {
  c.palco.append(attesa('Leggo le diapositive…'));
  let dd: { titolo: string; punti: string[] }[];
  try { dd = await chiedi('pptx'); }
  catch (e) { c.palco.replaceChildren(errore((e as Error).message)); return { tasti: [], smonta() {} }; }
  if (!c.viva()) return { tasti: [], smonta() {} };
  let i = 0;
  const schermo = h('div.d-schermo');
  const conta = h('span.d-conta');
  const mostra = (n: number) => {
    i = Math.max(0, Math.min(dd.length - 1, n));
    const x = dd[i];
    const nuova = h('div.d-diapo', null, h('h2', null, x.titolo || `Diapositiva ${i + 1}`), h('ul', null, ...x.punti.map((p) => h('li', null, p))));
    schermo.replaceChildren(nuova);
    conta.textContent = `${i + 1} / ${dd.length}`;
  };
  c.palco.replaceChildren(h('div.d-sala', null, schermo, h('div.d-piede', null,
    h('button.l-icona', { html: ic.sx, onclick: () => mostra(i - 1) }), conta, h('button.l-icona', { html: ic.dx, onclick: () => mostra(i + 1) }),
    h('span.c-nota', null, 'Solo i testi: per le diapositive vere serve LibreOffice o PowerPoint'))));
  mostra(0);
  const tasti: Tasto[] = [
    { k: ['arrowright', ' ', 'pagedown'], etichetta: '→', cosa: 'Diapositiva dopo', fai: () => mostra(i + 1) },
    { k: ['arrowleft', 'pageup'], etichetta: '←', cosa: 'Diapositiva prima', fai: () => mostra(i - 1) },
    { k: ['home'], etichetta: 'Inizio', cosa: 'La prima', fai: () => mostra(0) },
    { k: ['end'], etichetta: 'Fine', cosa: 'L\'ultima', fai: () => mostra(dd.length - 1) },
  ];
  return { tasti, info: () => `${dd.length} diapositive`, smonta() {} };
}
