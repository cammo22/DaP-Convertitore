// Le regolazioni di ogni formato: il pannello giusto per il formato scelto.

import type { Categoria, Formato } from '../../ponte';
import { h } from '../../util';
import { s, eVideoVero } from '../stato';
import { gruppo, bottoni, interruttore } from '../controlli';
import { regolazioniVideo } from './video';
import { regolazioniImmagine } from './immagine';
import { eFormatoArchivio, regolazioniArchivio } from './archivio';

export function regolazioni(c: Categoria, f?: Formato): Node {
  if (!f) return h('div');
  const o = s.opzioni;
  if (eVideoVero(f)) return regolazioniVideo(f);
  if (f.id === 'video.gif') {
    return h('div.regolazioni', null,
      gruppo('LARGHEZZA', bottoni([320, 480, 640, 800].map((v) => ({ v, testo: `${v} px` })), o.video.gifLarghezza, (v) => (o.video.gifLarghezza = v))),
      gruppo('FOTOGRAMMI AL SECONDO', bottoni([10, 15, 20, 25].map((v) => ({ v, testo: String(v) })), o.video.gifFps, (v) => (o.video.gifFps = v))),
      h('p.spiega', null, 'La tavolozza dei 256 colori si calcola sul video vero, fotogramma per fotogramma.'));
  }
  if (c === 'audio' || f.id === 'video.mp3' || f.id === 'video.wav') {
    const senzaPerdite = ['.flac', '.wav', '.aiff'].includes(f.estensione);
    const kbps = { '.mp3': [128, 192, 256, 320], '.m4a': [128, 192, 256, 320], '.opus': [64, 96, 128, 160, 192], '.ogg': [128, 192, 256, 320], '.wma': [128, 192, 256] }[f.estensione] ?? [];
    const partenza = { '.mp3': 256, '.m4a': 256, '.opus': 160, '.ogg': 192, '.wma': 192 }[f.estensione] ?? 0;
    return h('div.regolazioni', null,
      senzaPerdite
        ? gruppo('PROFONDITÀ', bottoni([{ v: 16, testo: '16 bit', nota: 'CD' }, { v: 24, testo: '24 bit', nota: 'studio' }], o.audio.bit, (v) => (o.audio.bit = v)))
        : gruppo('QUALITÀ', bottoni(kbps.map((v) => ({ v, testo: `${v}`, nota: 'kbit/s' })), o.audio.kbps || partenza, (v) => (o.audio.kbps = v))),
      gruppo('RITOCCHI',
        h('div.fila', null,
          interruttore('Volume uniforme', o.audio.normalizza, (v) => (o.audio.normalizza = v), '-14 LUFS, come Spotify'),
          interruttore('Mono', o.audio.mono, (v) => (o.audio.mono = v)))));
  }
  if (c === 'immagine' && !['img.pdf', 'img.txt', 'img.mp4', 'img.webm'].includes(f.id)) {
    return regolazioniImmagine(f);
  }
  if (f.id === 'pdf.jpg' || f.id === 'pdf.png') {
    return h('div.regolazioni', null,
      gruppo('DEFINIZIONE', bottoni([{ v: 100, testo: '100', nota: 'schermo' }, { v: 150, testo: '150', nota: 'leggera' }, { v: 200, testo: '200', nota: 'buona' }, { v: 300, testo: '300', nota: 'stampa' }], o.pdf.dpi, (v) => (o.pdf.dpi = v))),
      h('p.spiega', null, 'Una immagine per pagina, in una cartella accanto al PDF. Se la pagina è una sola, esce un file solo.'));
  }
  if ((c === 'archivio' || c === 'cartella') && eFormatoArchivio(f)) return regolazioniArchivio(c, f);
  const spiegazioni: Record<string, string> = {
    'pdf.txt': 'Il testo si legge dal PDF. Se è una scansione, lo legge l\'OCR di Windows, nella lingua del PC.',
    'img.txt': 'L\'OCR di Windows legge quello che c\'è scritto nella foto.',
    'pdf.unisci': 'Tutti i PDF scelti, uno dopo l\'altro, in un PDF solo. L\'ordine è quello della coda.',
    'img.pdf': 'Tutte le foto scelte in un PDF solo, una per pagina A4, girate come la foto.',
    'arch.estrai': 'In una cartella accanto, col nome dell\'archivio. Se dentro c\'è già una cartella sola, non la metto dentro un\'altra.',
  };
  const chi = c === 'documento' || c === 'foglio' || c === 'presentazione' || f.id === 'pdf.docx'
    ? (s.stato.office.word ? 'Ci pensa Microsoft Office, senza aprire finestre.' : 'Ci pensa LibreOffice, senza aprire finestre.')
    : c === 'archivio' || c === 'cartella' ? 'Ci pensa 7-Zip, dentro il programma.' : '';
  return h('div.regolazioni', null, h('p.spiega', null, spiegazioni[f.id] ?? (chi || 'Nessuna regolazione: si converte e basta.')));
}
