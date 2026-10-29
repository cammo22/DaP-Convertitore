// La home: quello che si vede a coda vuota. Il logo che gira e il riassunto di sempre, in più le cose che servono:
// da dove cominciare (un tipo di file alla volta), come sta il PC, gli ultimi file convertiti e il sistema.

import { chiedi } from '../ponte';
import type { Categoria } from '../ponte';
import { h, peso } from '../util';
import { icone, iconaCategoria, coloreCategoria } from '../icone';
import { clac } from '../suoni';
import { s } from './stato';
import { invitoMenu11 } from './menu11';
import { apriImpostazioni } from './impostazioni';
import { logoSvg, mostraAvviso } from './scheletro';

interface VoceRecente { quando: string; nome: string; formato: string; uscita: string; pesoPrima: number; pesoDopo: number; categoria: string }
interface Recenti { recenti: VoceRecente[]; totali: { conversioni: number; byteRisparmiati: number } }

/** Da dove si comincia: le sei strade principali, ognuna con i formati che il motore sa fare davvero. */
const STRADE: { cat: Categoria; titolo: string; nota: string }[] = [
  { cat: 'video', titolo: 'Video', nota: 'peso su misura per Discord e WhatsApp' },
  { cat: 'audio', titolo: 'Musica e audio', nota: 'MP3, FLAC, OPUS…' },
  { cat: 'immagine', titolo: 'Foto', nota: 'anche HEIC e RAW' },
  { cat: 'pdf', titolo: 'PDF', nota: 'unisci, spezza, leggi con l\'OCR' },
  { cat: 'documento', titolo: 'Documenti', nota: 'Word, fogli, presentazioni' },
  { cat: 'archivio', titolo: 'Archivi', nota: 'ZIP, 7Z, RAR… con 7-Zip' },
];

const quando = (iso: string) => {
  const d = new Date(iso);
  const sec = (Date.now() - d.getTime()) / 1000;
  if (sec < 90) return 'adesso';
  if (sec < 3600) return `${Math.round(sec / 60)} min fa`;
  if (sec < 86400) return `${Math.round(sec / 3600)} h fa`;
  return d.toLocaleDateString('it-IT', { day: 'numeric', month: 'short' });
};

function strada(x: (typeof STRADE)[number]) {
  const formati = s.stato.formati.filter((f) => f.categoria === x.cat).slice(0, 4).map((f) => f.etichetta).join(' · ');
  return h('button.strada', { style: { '--tinta': coloreCategoria[x.cat] }, title: `Scegli ${x.titolo.toLowerCase()} da convertire`,
    onclick: () => { clac(); void chiedi('scegli', { categoria: x.cat }); } },
    h('span.strada-icona', { html: iconaCategoria[x.cat] }),
    h('b', null, x.titolo),
    h('small', null, formati || x.nota),
    h('em', null, x.nota));
}

function scheda(titolo: string, valore: string, nota?: string, azione?: { testo: string; fai: () => void }, ok = true) {
  return h(`div.scheda-pc${ok ? '' : '.da-fare'}`, null,
    h('span.scheda-titolo', null, h('i.puntino'), titolo),
    h('b', null, valore),
    nota ? h('small', null, nota) : null,
    azione ? h('button.link', { onclick: () => { clac(); azione.fai(); } }, azione.testo) : null);
}

/** Come sta il PC: quello che serve sapere prima di cominciare (scheda video, archivi, documenti, sistema, versione). */
function statoPc(): HTMLElement {
  const st = s.stato;
  const gpu = st.hardware?.gpu?.[0];
  const imp = st.impostazioni;
  const reg = st.regolaPredefiniti;
  const m = st.menu11;
  const voci = [imp.menu, imp.apriCon, m?.supportato ? m.registrato : true, reg.totale > 0 && reg.impostati === reg.totale];
  const aPosto = voci.filter(Boolean).length;
  const doc = st.office.word ? 'Microsoft Office' : st.office.libreOffice ? 'LibreOffice' : 'Serve LibreOffice';
  return h('div.stato-pc', null,
    scheda('Scheda video', st.hardware?.acceleratore ?? 'Solo CPU', gpu ? `${gpu.nome}${gpu.vram ? ` · ${peso(gpu.vram)}` : ''}` : 'Nessuna scheda con encoder: lavora la CPU'),
    scheda('Archivi', st.office.setteZip ? '7-Zip dentro' : 'Manca 7-Zip', st.office.setteZip ? 'ZIP, 7Z, RAR, TAR, ISO… anche con gli accenti' : 'Reinstalla il programma', undefined, !!st.office.setteZip),
    scheda('Documenti', doc, st.office.word || st.office.libreOffice ? 'Word, fogli e presentazioni in PDF e viceversa' : 'Per Word, Excel e PowerPoint',
      st.office.word || st.office.libreOffice ? undefined : { testo: 'Scarica LibreOffice', fai: () => void chiedi('link', { url: 'https://it.libreoffice.org/download/download/' }) }, !!(st.office.word || st.office.libreOffice)),
    scheda('Sistema', aPosto === voci.length ? 'Tutto a posto' : `${aPosto} su ${voci.length} a posto`,
      reg.totale ? `${reg.impostati} tipi su ${reg.totale} si aprono col lettore` : 'Tasto destro, «Apri con», app predefinite',
      { testo: aPosto === voci.length ? 'Impostazioni' : 'Aggiusta', fai: () => apriImpostazioni() }, aPosto === voci.length),
    scheda('Versione', st.versione, s.aggiornamento ? `Aggiornamento ${s.aggiornamento} disponibile` : 'Aggiornamenti da soli',
      s.aggiornamento ? { testo: 'Installa', fai: () => { void chiedi('aggiorna', { installa: true }); mostraAvviso('Scarico e riapro il programma…'); } } : undefined));
}

function recenti(): HTMLElement {
  const box = h('div.recenti', null);
  void chiedi<Recenti>('storico', { quanti: 6 }).then((r) => {
    if (!box.isConnected) return;
    if (!r?.recenti?.length) {
      box.replaceChildren(h('p.recenti-vuoto', null, 'Qui compaiono gli ultimi file convertiti, con quanto hanno guadagnato.'));
      return;
    }
    const risparmio = r.totali.byteRisparmiati > 0 ? ` · ${peso(r.totali.byteRisparmiati)} risparmiati` : '';
    box.replaceChildren(
      h('div.titolo-sezione', null, h('span', null, 'ULTIMI CONVERTITI'), h('span.conta', null, `${r.totali.conversioni.toLocaleString('it-IT')} in tutto${risparmio}`)),
      ...r.recenti.map((v) => {
        const cat = (v.categoria in iconaCategoria ? v.categoria : 'altro') as Categoria;
        const delta = v.pesoPrima && v.pesoDopo ? Math.round((1 - v.pesoDopo / v.pesoPrima) * 100) : null;
        return h('div.recente', { style: { '--tinta': coloreCategoria[cat] } },
          h('span.recente-icona', { html: iconaCategoria[cat] }),
          h('div.recente-testo', null,
            h('b', { title: v.uscita }, v.nome, h('span.recente-freccia', null, ` → ${v.formato}`)),
            h('small', null, `${quando(v.quando)} · ${v.pesoPrima ? `${peso(v.pesoPrima)} → ` : ''}${peso(v.pesoDopo)}${delta !== null && Math.abs(delta) >= 1 ? ` (${delta > 0 ? '−' : '+'}${Math.abs(delta)}%)` : ''}`)),
          h('button.guarda', { title: 'Guarda nel lettore', html: icone.occhio, onclick: () => { clac(); void chiedi('guarda', { percorso: v.uscita }); } }),
          h('button.guarda', { title: 'Mostra nella cartella', html: icone.cartella, onclick: () => { clac(); void chiedi('mostra', { percorso: v.uscita }); } }));
      }));
  }).catch(() => { /* senza storico la home resta com'è */ });
  return box;
}

export function disegnaHome(): HTMLElement {
  const scritta = s.stato.impostazioni.scritta || 'convertito';
  return h('div.home', null,
    h('div.benvenuto', null,
      h('div.logo-grande', { html: logoSvg }),
      h('h1', null, 'Qualsiasi file, in qualsiasi formato.'),
      h('p', null, 'Tasto destro su un file → ', h('b', null, 'DaP Convertitore'), '. Oppure trascinali qui, o premi ', h('b', null, 'Ctrl+V'), ' per incollare quelli copiati.'),
      h('p.piccolo', null, 'Il risultato finisce accanto all\'originale, col nome ', h('code', null, `(${scritta})`), '. Tutto resta sul tuo PC.')),
    invitoMenu11(),
    h('div.titolo-sezione', null, h('span', null, 'DA DOVE COMINCI?'),
      h('button.link', { title: 'Guarda un file qualsiasi nel lettore, senza convertire', onclick: () => { clac(); void chiedi('scegli', { lettore: true }); } }, 'oppure apri un file nel lettore')),
    h('div.strade', null, ...STRADE.map(strada)),
    recenti(),
    h('div.titolo-sezione', null, h('span', null, 'COME STA IL PC'), h('span.conta', null, 'Impostazioni in alto a destra')),
    statoPc());
}
