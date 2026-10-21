// Le prove della minimappa (ui/src/app/minimappa.ts): i conti di miniatura, a capo, segni della ricerca, riquadro e clic.
// Node 23+ legge il TypeScript da solo. Uso: node scripts/prova-minimappa.mjs
import assert from 'node:assert/strict';
import { calcolaMappa, segniMappa, riquadro, frazioneDaY, frazioneDaRiga, rigaDaFrazione, MAX_A_CAPO } from '../ui/src/app/minimappa.ts';
import { indicizza, cerca, rigaVideoDi } from '../ui/src/app/righe.ts';

let fatte = 0;
const prova = (nome, fn) => { fn(); fatte++; console.log(`ok  ${nome}`); };
const fonteDi = (righe) => ({ n: righe.length, riga: (i, max) => righe[i].slice(0, max), lung: (i) => righe[i].length });
const inchiostro = (m, y0, y1) => { let t = 0; for (let y = y0; y < y1; y++) for (let x = 0; x < m.w; x++) t += m.px[y * m.w + x] ? 1 : 0; return t; };

prova('una riga ogni due pixel, un pixel per carattere, gli spazi restano vuoti', () => {
  const m = calcolaMappa(fonteDi(['ab cd', '', '    x']), 90, 600);
  assert.equal(m.h, 6);                      // 3 righe × 2 px
  assert.equal(m.pxRiga, 2);
  assert.deepEqual([...m.px.slice(0, 6)].map((v) => (v ? 1 : 0)), [1, 1, 0, 1, 1, 0]);
  assert.equal(inchiostro(m, 2, 4), 0);      // la riga vuota non lascia niente
  assert.equal(m.px[4 * m.w + 4] > 0, true); // la «x» dopo 4 spazi sta in colonna 4
  assert.equal(m.px[5 * m.w + 4] < m.px[4 * m.w + 4], true); // il pixel di sotto è più chiaro
});

prova('sugli schermi ad alta densità ogni carattere fa 2×2 pixel', () => {
  const m = calcolaMappa(fonteDi(['a']), 90, 600, 2);
  assert.equal(m.h, 4);
  assert.equal(m.w, 90);
  assert.equal(inchiostro(m, 0, 4), 8);      // 2 colonne × 4 righe di pixel
});

prova('una riga da 1 KB: senza a capo è una riga sola, con l\'a capo fa più righe', () => {
  const kb = 'parola '.repeat(150);           // ~1 KB
  const piatta = calcolaMappa(fonteDi([kb]), 90, 600);
  assert.equal(piatta.righe, 1);
  assert.equal(piatta.h, 2);
  const capo = calcolaMappa(fonteDi([kb]), 90, 600, 1, true);
  assert.equal(capo.righe, Math.ceil(kb.length / 90));
  assert.ok(capo.h >= 22 && capo.righe >= 11);
  assert.deepEqual([...capo.prima], [0, capo.righe]);
  assert.ok(inchiostro(capo, 0, capo.h) > 500);
});

prova('l\'a capo ha un tetto: un JSON da 6 milioni di caratteri non fa sessantamila righe', () => {
  const m = calcolaMappa(fonteDi(['x'.repeat(6_000_000), 'fine']), 90, 4000, 1, true);
  assert.equal(m.righe, MAX_A_CAPO + 1);
});

prova('un documento più alto dello spazio si schiaccia e si vede ancora dove c\'è scritto', () => {
  const righe = [];
  for (let i = 0; i < 100_000; i++) righe.push(i < 50_000 ? `riga piena numero ${i}` : '');
  const t0 = performance.now();
  const m = calcolaMappa(fonteDi(righe), 90, 700);
  const ms = performance.now() - t0;
  assert.equal(m.h, 700);
  assert.ok(m.pxRiga < 0.01);
  assert.ok(inchiostro(m, 0, 340) > 340 * 10, 'la prima metà è scritta');
  assert.equal(inchiostro(m, 360, 700), 0, 'la seconda metà è vuota');
  console.log(`    100.000 righe in ${ms.toFixed(0)} ms`);
  assert.ok(ms < 1500, 'troppo lento');
});

prova('i risultati della ricerca sono segni sulla riga giusta, quello scelto vale 2', () => {
  const s = segniMappa(1000, 200, [0, 500, 999], 500);
  assert.equal(s[0], 1);
  assert.equal(s[100], 2);
  assert.equal(s[199], 1);
  assert.equal(s.reduce((a, v) => a + (v ? 1 : 0), 0), 3);
  // con le righe a capo il segno va alla prima riga della miniatura di quella riga del documento
  const sc = segniMappa(10, 100, [1], -1, Int32Array.from([0, 5, 10]));
  assert.equal(sc[50], 1);
  assert.deepEqual([...segniMappa(0, 10, [1], -1)], new Array(10).fill(0));
});

prova('il riquadro sta dentro la colonna e ha un\'altezza minima da afferrare', () => {
  let r = riquadro(700, 0.5, 0.1);
  assert.equal(r.y, 350);
  assert.equal(r.alto, 70);
  r = riquadro(700, 0.999, 0.00001);          // un registro enorme: il riquadro sarebbe un pixel
  assert.equal(r.alto, 14);
  assert.equal(r.y, 700 - 14);                // e non esce dal fondo
  assert.deepEqual(riquadro(700, 0, 1), { y: 0, alto: 700 });
});

prova('il clic diventa un punto del documento (0…1), anche fuori dai bordi', () => {
  assert.equal(frazioneDaY(700, 350), 0.5);
  assert.equal(frazioneDaY(700, -20), 0);
  assert.equal(frazioneDaY(700, 9999), 1);
  assert.equal(frazioneDaY(0, 5), 0);
});

prova('l\'anteprima vera (Doc) e la minimappa vanno d\'accordo sulle righe e sui trovati', () => {
  const d = indicizza('alfa\nbeta\ngamma\nalfa beta');
  const f = { n: d.vRiga.length, riga: (i, max) => d.testo.slice(d.vDa[i], Math.min(d.vA[i], d.vDa[i] + max)) };
  const m = calcolaMappa(f, 90, 600);
  assert.equal(m.righe, 4);
  const tr = cerca(d, 'alfa');
  const righe = [...tr.pos].map((p) => rigaVideoDi(d, p));
  assert.deepEqual(righe, [0, 3]);
  const s = segniMappa(m.righe, m.h, righe, righe[1]);
  assert.equal(s[0], 1);
  assert.equal(s[Math.floor((3 * m.h) / 4)], 2);
});

prova('righe lunghe a capo: da riga a punto della miniatura e ritorno', () => {
  // tre righe del documento: la prima fa 1 riga nella miniatura, la seconda 8, la terza 1 (10 in tutto)
  const prima = Int32Array.from([0, 1, 9, 10]);
  assert.equal(frazioneDaRiga(0, 3, 10, prima), 0);
  assert.equal(frazioneDaRiga(1, 3, 10, prima), 0.1);
  assert.equal(frazioneDaRiga(1.5, 3, 10, prima), 0.5);   // a metà della riga lunga: a metà dei suoi pixel
  assert.equal(frazioneDaRiga(2, 3, 10, prima), 0.9);
  assert.equal(frazioneDaRiga(3, 3, 10, prima), 1);
  assert.equal(rigaDaFrazione(0.5, 3, 10, prima), 1.5);
  assert.equal(rigaDaFrazione(0.95, 3, 10, prima), 2.5);
  for (const r of [0, 0.25, 1, 1.75, 2.5]) assert.ok(Math.abs(rigaDaFrazione(frazioneDaRiga(r, 3, 10, prima), 3, 10, prima) - r) < 1e-9, `andata e ritorno a ${r}`);
  // senza a capo è semplicemente proporzionale
  assert.equal(frazioneDaRiga(50, 100, 100, null), 0.5);
  assert.equal(rigaDaFrazione(0.25, 100, 100, null), 25);
  assert.equal(frazioneDaRiga(0, 0, 0, null), 0);
});

console.log(`\n${fatte} prove superate`);
