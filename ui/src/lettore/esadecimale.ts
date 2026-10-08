// Il laboratorio: per i file che non sono niente di conosciuto. Cos'è (dai primi byte, la «firma»), quanto pesa,
// l'impronta SHA-256 per controllare che sia integro, e dentro byte per byte, sedici per riga.
import { chiedi } from '../ponte';
import { h, peso } from '../util';
import { dataItaliana, ic, type Contesto, type Vista } from './comune';

const firme: [number[], string][] = [
  [[0x4d, 0x5a], 'Programma o libreria di Windows (EXE, DLL)'],
  [[0x7f, 0x45, 0x4c, 0x46], 'Programma Linux (ELF)'],
  [[0x50, 0x4b, 0x03, 0x04], 'Archivio ZIP (o un file Office, APK, JAR… che dentro è uno ZIP)'],
  [[0x37, 0x7a, 0xbc, 0xaf], 'Archivio 7-Zip'],
  [[0x52, 0x61, 0x72, 0x21], 'Archivio RAR'],
  [[0x1f, 0x8b], 'Compresso GZIP'],
  [[0x25, 0x50, 0x44, 0x46], 'Documento PDF'],
  [[0x53, 0x51, 0x4c, 0x69, 0x74, 0x65], 'Database SQLite'],
  [[0x89, 0x50, 0x4e, 0x47], 'Immagine PNG'],
  [[0xff, 0xd8, 0xff], 'Immagine JPEG'],
  [[0x47, 0x49, 0x46, 0x38], 'Immagine GIF'],
  [[0x49, 0x44, 0x33], 'Audio MP3'],
  [[0x66, 0x4c, 0x61, 0x43], 'Audio FLAC'],
  [[0x4f, 0x67, 0x67, 0x53], 'Audio o video OGG'],
  [[0x1a, 0x45, 0xdf, 0xa3], 'Video MKV o WEBM'],
  [[0xd0, 0xcf, 0x11, 0xe0], 'Documento Office vecchio (DOC, XLS, PPT) o MSI'],
  [[0x4c, 0x00, 0x00, 0x00, 0x01, 0x14, 0x02, 0x00], 'Collegamento di Windows (LNK)'],
  [[0x43, 0x44, 0x30, 0x30, 0x31], 'Immagine disco ISO'],
  [[0x00, 0x00, 0x01, 0x00], 'Icona di Windows (ICO)'],
  [[0x4d, 0x53, 0x43, 0x46], 'Archivio CAB di Windows'],
  [[0xca, 0xfe, 0xba, 0xbe], 'Programma Java o macOS'],
  [[0x00, 0x61, 0x73, 0x6d], 'WebAssembly'],
];

export async function monta(c: Contesto): Promise<Vista> {
  const f = c.scheda;
  const RIGA = 22, PEZZO = 64 * 1024;
  const totaleRighe = Math.ceil(f.peso / 16);
  const pezzi = new Map<number, Uint8Array>();
  const chiesti = new Set<number>();
  let pronta = false;
  const prendi = async (n: number) => {
    if (chiesti.has(n)) return;
    chiesti.add(n);
    const b64 = await chiedi<string>('byte', { da: n * PEZZO, quanti: PEZZO });
    pezzi.set(n, Uint8Array.from(atob(b64), (ch) => ch.charCodeAt(0)));
    if (pronta) disegna(true);
  };
  await prendi(0);
  if (!c.viva()) return { tasti: [], smonta() {} };
  const inizio = pezzi.get(0) ?? new Uint8Array();
  const cosa = firme.find(([m]) => m.every((b, i) => inizio[i] === b))?.[1] ?? (f.tipoWindows || 'Non lo so riconoscere dai primi byte');

  const impronta = h('code.x-impronta', null, '—');
  const corpo = h('div.x-righe');
  const spazio = h('div.x-spazio', null, corpo);
  const scorre = h('div.x-scorre', null, spazio);
  c.palco.replaceChildren(h('div.x-box', null,
    h('div.x-scheda', null,
      h('div.x-firma', null, ...[...inizio.slice(0, 4)].map((b) => h('span', null, b.toString(16).padStart(2, '0').toUpperCase()))),
      h('div', null,
        h('b', null, cosa),
        h('dl', null,
          h('div', null, h('dt', null, 'Peso'), h('dd', null, `${peso(f.peso)} · ${f.peso.toLocaleString('it-IT')} byte`)),
          h('div', null, h('dt', null, 'Modificato'), h('dd', null, dataItaliana(f.modificato))),
          h('div', null, h('dt', null, 'SHA-256'), h('dd', null, impronta,
            h('button.l-tasto.piccolo', { onclick: async (e: Event) => {
              const b = e.currentTarget as HTMLButtonElement;
              b.disabled = true; b.textContent = 'Calcolo…';
              try { const x = await chiedi<string>('impronta'); impronta.textContent = x; b.textContent = 'Copia'; b.disabled = false; b.onclick = () => void navigator.clipboard.writeText(x).then(() => c.hud('Impronta copiata', ic.copia)); }
              catch (err) { b.textContent = (err as Error).message; }
            } }, 'Calcola')))))),
    h('div.x-intestazione', null, h('span', null, 'POSIZIONE'), h('span', null, '00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F'), h('span', null, 'TESTO')),
    scorre));

  let da = -1, a = -1;
  function disegna(forza = false) {
    spazio.style.height = `${totaleRighe * RIGA + 20}px`;
    const primo = Math.max(0, Math.floor(scorre.scrollTop / RIGA) - 30);
    const ultimo = Math.min(totaleRighe, Math.ceil((scorre.scrollTop + scorre.clientHeight) / RIGA) + 30);
    if (!forza && primo >= da && ultimo <= a) return;
    da = Math.max(0, primo - 60); a = Math.min(totaleRighe, ultimo + 60);
    let html = '';
    for (let r = da; r < a; r++) {
      const pos = r * 16;
      const n = Math.floor(pos / PEZZO);
      const p = pezzi.get(n);
      if (!p) { void prendi(n); html += `<div class="x-r"><i>${pos.toString(16).padStart(8, '0').toUpperCase()}</i><span class="x-h">…</span><span></span></div>`; continue; }
      let hex = '', txt = '';
      for (let k = 0; k < 16; k++) {
        const i = pos - n * PEZZO + k;
        if (pos + k >= f.peso) { hex += '   '; continue; }
        const b = p[i];
        hex += `<b class="${b === 0 ? 'z' : b < 32 || b > 126 ? 'n' : 't'}">${b.toString(16).padStart(2, '0').toUpperCase()}</b>${k === 7 ? '  ' : ' '}`;
        txt += b >= 32 && b <= 126 ? String.fromCharCode(b).replace(/&/, '&amp;').replace(/</, '&lt;') : '<u>·</u>';
      }
      html += `<div class="x-r"><i>${pos.toString(16).padStart(8, '0').toUpperCase()}</i><span class="x-h">${hex}</span><span class="x-t">${txt}</span></div>`;
    }
    corpo.innerHTML = html;
    corpo.style.transform = `translateY(${da * RIGA}px)`;
  }
  scorre.addEventListener('scroll', () => disegna(), { passive: true });
  pronta = true;
  disegna(true);

  return {
    tasti: [
      { k: ['home', 'ctrl+home'], etichetta: 'Inizio', cosa: 'All\'inizio', fai: () => { scorre.scrollTop = 0; } },
      { k: ['end', 'ctrl+end'], etichetta: 'Fine', cosa: 'Alla fine', fai: () => { scorre.scrollTop = scorre.scrollHeight; } },
    ],
    info: () => cosa.split(' (')[0],
    smonta() {},
  };
}
