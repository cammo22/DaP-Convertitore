// Il pannello degli archivi: lo slider della compressione. Dal più veloce (quasi solo archiviare) al più piccolo.
// Vale per ZIP, 7Z, TAR.GZ e TAR.XZ, anche partendo da un archivio già fatto (uno ZIP debole si ricomprime più forte).

import type { Categoria, Formato } from '../../ponte';
import { h } from '../../util';
import { s } from '../stato';
import { cursore, gruppo } from '../controlli';

interface Livello { v: number; nome: string; nota: string }

/** I livelli dello slider: il numero è quello di 7-Zip (-1 = solo archiviare). */
const LIVELLI: Livello[] = [
  { v: -1, nome: 'NESSUNA', nota: 'solo archivia: istantaneo, il peso resta quello' },
  { v: 1, nome: 'VELOCE', nota: 'pochi secondi, comprime poco' },
  { v: 3, nome: 'LEGGERA', nota: 'veloce e già una bella differenza' },
  { v: 5, nome: 'NORMALE', nota: 'il giusto mezzo' },
  { v: 7, nome: 'FORTE', nota: 'più lento, file più piccolo' },
  { v: 9, nome: 'MASSIMA', nota: 'il più piccolo possibile: ci mette di più e usa più memoria' },
];

/** Dove sta lo slider quando non è stato toccato: il livello di partenza del formato (come SetteZip.LivelloVero). */
const partenza = (f: Formato) => f.id.endsWith('.zip') ? 5 : 7;

const spiegazioni: Record<string, string> = {
  'arch.zip': 'Lo ZIP lo apre qualsiasi computer, anche senza programmi. Al massimo usa il Deflate più paziente.',
  'cartella.zip': 'Lo ZIP lo apre qualsiasi computer, anche senza programmi. Al massimo usa il Deflate più paziente.',
  'arch.7z': 'Il 7Z (LZMA2) è il più piccolo. Per aprirlo serve 7-Zip o un programma che lo conosca, come questo.',
  'cartella.7z': 'Il 7Z (LZMA2) è il più piccolo. Per aprirlo serve 7-Zip o un programma che lo conosca, come questo.',
  'arch.tgz': 'TAR.GZ: lo standard di Linux e Mac. Si apre ovunque tranne che con un doppio clic su Windows vecchi.',
  'arch.txz': 'TAR.XZ: come TAR.GZ ma più compresso, più lento a farsi.',
};

export const eFormatoArchivio = (f: Formato) => f.id in spiegazioni;

export function regolazioniArchivio(c: Categoria, f: Formato): Node {
  const o = s.opzioni.archivio;
  const posizione = () => {
    const v = o.livello === 0 ? partenza(f) : o.livello;
    const i = LIVELLI.findIndex((x) => x.v >= v);
    return i < 0 ? LIVELLI.length - 1 : i;
  };
  const nome = h('b.liv-nome', null, LIVELLI[posizione()].nome);
  const nota = h('span.liv-nota', null, LIVELLI[posizione()].nota);
  const mostra = (i: number) => { nome.textContent = LIVELLI[i].nome; nota.textContent = LIVELLI[i].nota; };
  const slider = cursore(0, LIVELLI.length - 1, posizione(), (i) => { o.livello = LIVELLI[i].v; mostra(i); }, ['più veloce', 'più piccolo']);
  return h('div.regolazioni', null,
    gruppo('COMPRESSIONE', h('div.liv-box', null, h('div.liv-testa', null, nome, nota), slider)),
    h('p.spiega', null, spiegazioni[f.id] ?? ''),
    c === 'archivio'
      ? h('p.spiega', null, 'Hai già un archivio? Sceglilo con un livello più alto e ottieni un file più piccolo con dentro le stesse cose: l\'originale non si tocca.')
      : null);
}
