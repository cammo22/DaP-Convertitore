// Le prove dell'anteprima a destra (ui/src/app/righe.ts): righe, righe lunghe spezzate, ricerca, evidenziature.
// Node 23+ legge il TypeScript da solo. Uso: node scripts/prova-anteprima.mjs
import assert from 'node:assert/strict';
import { indicizza, cerca, rigaDi, rigaVideoDi, pezzoHtml, primoDopo, vicino, PEZZO, MAX_TROVATI } from '../ui/src/app/righe.ts';

let fatte = 0;
const prova = (nome, fn) => { fn(); fatte++; console.log(`ok  ${nome}`); };

prova('CRLF, CR e LF diventano righe giuste', () => {
  const d = indicizza('uno\r\ndue\rtre\nquattro');
  assert.equal(d.crlf, true);
  assert.equal(d.testo, 'uno\ndue\ntre\nquattro');
  assert.deepEqual([...d.inizi], [0, 4, 8, 12]);
  assert.equal(d.vRiga.length, 4);
  assert.equal(rigaDi(d, 9), 2);
});

prova('un file vuoto e uno che finisce con a capo', () => {
  assert.equal(indicizza('').vRiga.length, 1);
  const d = indicizza('a\n');
  assert.equal(d.inizi.length, 2);
  assert.equal(d.vDa[1], 2);
  assert.equal(d.vA[1], 2);
});

prova('una riga lunghissima si spezza in pezzi, il numero resta uno', () => {
  const lunga = 'x'.repeat(PEZZO * 2 + 5);
  const d = indicizza(`prima\n${lunga}\nultima`);
  assert.equal(d.inizi.length, 3);
  assert.equal(d.vRiga.length, 5);            // prima + 3 pezzi + ultima
  assert.deepEqual([...d.vRiga], [0, 1, 1, 1, 2]);
  assert.equal(d.prima[2], 4);
  assert.equal(d.lungMax, PEZZO);
  assert.equal(rigaVideoDi(d, 6 + PEZZO + 3), 2); // dentro il secondo pezzo
});

prova('cerca senza maiuscole, senza sovrapposizioni', () => {
  const d = indicizza('Napoli napoli NAPOLI\naaaa');
  assert.deepEqual([...cerca(d, 'napoli').pos], [0, 7, 14]);
  assert.deepEqual([...cerca(d, 'napoli', true).pos], [7]);
  assert.deepEqual([...cerca(d, 'aa').pos], [21, 23]);
  assert.equal(cerca(d, '').pos.length, 0);
});

prova('i caratteri speciali si cercano per quello che sono', () => {
  const d = indicizza('costo (2 €) * 3 [ok] a.b');
  assert.deepEqual([...cerca(d, '(2 €)').pos], [6]);
  assert.deepEqual([...cerca(d, '[ok]').pos], [16]);
  assert.deepEqual([...cerca(d, '.').pos], [22]);
  assert.deepEqual([...cerca(d, 'CITTÀ').pos], [], 'non c\'è');
  assert.deepEqual([...cerca(indicizza('Città città'), 'CITTÀ').pos], [0, 6]);
});

prova('le evidenziature nel pezzo, anche a cavallo di due pezzi', () => {
  const d = indicizza('a <b> c <b>');
  const t = cerca(d, '<b>');
  assert.equal(pezzoHtml(d, 0, d.testo.length, t, 1), 'a <mark>&lt;b&gt;</mark> c <mark class="su">&lt;b&gt;</mark>');
  // il trovato «cd» spezzato fra il pezzo [0,3) e [3,6)
  const d2 = indicizza('abcdef');
  const t2 = cerca(d2, 'cd');
  assert.equal(pezzoHtml(d2, 0, 3, t2, 0), 'ab<mark class="su">c</mark>');
  assert.equal(pezzoHtml(d2, 3, 6, t2, 0), '<mark class="su">d</mark>ef');
  assert.equal(primoDopo(t2, 3), 0);
  assert.equal(primoDopo(t2, 4), 1);
});

prova('Invio parte dal trovato più vicino a dove guardi', () => {
  const d = indicizza('x\nx\nx\nx');
  const t = cerca(d, 'x');
  assert.equal(vicino(t, 3), 2);
  assert.equal(vicino(t, 100), 0); // oltre l'ultimo: si ricomincia
});

prova('un registro da 6 MB: si indicizza e si cerca in fretta, e i trovati si fermano a 100.000', () => {
  const righe = [];
  for (let i = 0; i < 120_000; i++) righe.push(`2026-10-21 09:00:00 INFO riga numero ${i} con un po' di testo dentro`);
  const testo = righe.join('\r\n');
  const t0 = performance.now();
  const d = indicizza(testo);
  const t1 = performance.now();
  const tr = cerca(d, 'info');
  const t2 = performance.now();
  assert.equal(d.inizi.length, 120_000);
  assert.equal(tr.pos.length, MAX_TROVATI);
  assert.equal(tr.troppi, true);
  console.log(`    ${(testo.length / 1e6).toFixed(1)} M caratteri: righe in ${(t1 - t0).toFixed(0)} ms, ricerca in ${(t2 - t1).toFixed(0)} ms`);
  assert.ok(t1 - t0 < 1500 && t2 - t1 < 1500, 'troppo lento');
});

console.log(`\n${fatte} prove superate`);
