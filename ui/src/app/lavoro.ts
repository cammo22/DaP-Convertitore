// Il banco mentre si lavora: le bobine che girano, i display a sette segmenti, le lancette del carico.


import { h, orologio } from '../util';
import { icone, coloreCategoria } from '../icone';
import { Bobine, Lancetta } from '../grafica';
import { bu, dinDon } from '../suoni';
import { s, ui } from './stato';
import { banco } from './scheletro';

export function statoGiro() {
  const l = s.giro.map((i) => s.lavori.get(i)!).filter(Boolean);
  const fatti = l.filter((x) => x.stato === 'fatto').length;
  return `${fatti} di ${l.length} fatti`;
}

export let bobine: Bobine | null = null;
export const strumenti: { cpu?: Lancetta; enc?: Lancetta; dec?: Lancetta } = {};

export function disegnaLavoro() {
  const canvas = h('canvas.bobine') as HTMLCanvasElement;
  const cpu = h('canvas.vu') as HTMLCanvasElement;
  const enc = h('canvas.vu') as HTMLCanvasElement;
  const dec = h('canvas.vu') as HTMLCanvasElement;
  banco.replaceChildren(
    h('div.titolo-sezione', null, h('span', null, 'IN LAVORAZIONE'), h('span.conta#conta-giro', null, statoGiro())),
    h('div.piastra', null,
      canvas,
      h('div.finestrella', null,
        h('div.riga-sette', null,
          dsp('AVANZAMENTO', 'avz', '000', '%'),
          dsp('MANCA', 'eta', '--:--:--', ''),
          dsp('VELOCITÀ', 'vel', '--.-', '×'),
          dsp('FOTOGRAMMI/S', 'fps', '---', ''),
        ),
        h('div.fase', null, h('i.led.lampeggia'), h('span#fase', null, 'Preparo…')),
        h('div.barra-grande', null, h('i#barra')),
      ),
    ),
    h('div.strumenti', null, h('div', null, cpu), h('div', null, enc), h('div', null, dec)),
    h('div.lista-lavori#lista-lavori'),
  );
  requestAnimationFrame(() => {
    bobine = new Bobine(canvas, coloreCategoria[s.attiva ?? 'video']);
    const tacche = [0, 25, 50, 75, 100].map((v) => ({ v: v / 100, testo: String(v) }));
    strumenti.cpu = new Lancetta(cpu, { etichetta: 'CPU', tacche, rossoDa: 0.85, rossoA: 1, picco: true });
    strumenti.enc = new Lancetta(enc, { etichetta: 'ENCODER', tacche, rossoDa: 0.85, rossoA: 1, picco: true, sotto: s.stato.hardware?.acceleratore?.split('·')[0]?.trim() ?? 'GPU' });
    strumenti.dec = new Lancetta(dec, { etichetta: 'DECODER', tacche, rossoDa: 0.85, rossoA: 1, picco: true });
    aggiornaLavoro();
  });
}

export function dsp(etichetta: string, id: string, spento: string, unita: string) {
  return h('div.dsp', null,
    h('div.dsp-etichetta', null, etichetta),
    h('div.sette.piccolo', null, h('span.spenti', null, spento.replace(/[0-9-]/g, '8').replace(/[^8:.]/g, '8')), h(`span.accesi#dsp-${id}`, null, spento)),
    unita ? h('span.dsp-unita', null, unita) : null);
}

export function aggiornaLavoro() {
  const giro = s.giro.map((i) => s.lavori.get(i)!).filter(Boolean);
  if (!giro.length) return;
  const corrente = giro.find((l) => l.stato === 'corre') ?? giro.find((l) => l.stato === 'attesa') ?? giro[giro.length - 1];
  // il display parla del lavoro in corso (velocità, tempo che manca); il totale sta in «2 di 3 fatti»
  const indeterminato = corrente.frazione < 0;
  const fraz = corrente.stato === 'fatto' ? 1 : Math.max(0, corrente.frazione);
  const set = (id: string, t: string) => { const e = document.getElementById(id); if (e) e.textContent = t; };
  set('dsp-avz', String(Math.floor(fraz * 100)).padStart(3, ' '));
  set('dsp-eta', orologio(corrente.eta));
  set('dsp-vel', corrente.velocita ? corrente.velocita.toFixed(1).padStart(4, ' ') : '--.-');
  set('dsp-fps', corrente.fps ? String(Math.round(corrente.fps)).padStart(3, ' ') : '---');
  set('fase', corrente.stato === 'attesa' ? 'In coda…' : corrente.fase ?? 'Lavoro…');
  set('conta-giro', statoGiro());
  set('piede-stato', statoGiro());
  const barra = document.getElementById('barra');
  if (barra) { barra.style.width = `${(indeterminato ? 100 : fraz * 100).toFixed(1)}%`; barra.classList.toggle('indeterminata', indeterminato); }
  bobine?.imposta(indeterminato ? 0.5 : fraz, corrente.velocita, corrente.stato === 'corre');

  const lista = document.getElementById('lista-lavori');
  if (lista) {
    lista.replaceChildren(...giro.map((l) => {
      const f = l.fileId ? s.file.get(l.fileId) : undefined;
      const nome = l.sorgenti.length > 1 ? `${l.sorgenti.length} file` : f?.nome ?? l.sorgenti[0].split('\\').pop();
      return h(`div.riga-lavoro.${l.stato}`, null,
        h('span.rl-nome', null, nome ?? ''),
        h('span.rl-freccia', { html: icone.freccia }),
        h('span.rl-formato', null, l.etichetta),
        h('div.mini-barra', null, h('i', { style: { width: `${Math.round((l.stato === 'fatto' ? 1 : Math.max(0, l.frazione)) * 100)}%` } })),
        h('span.rl-stato', null, ({ attesa: 'in coda', corre: `${Math.floor(Math.max(0, l.frazione) * 100)}%`, fatto: 'fatto', errore: 'errore', annullato: 'annullato' } as const)[l.stato]),
      );
    }));
  }
  ui.coda();
  if (giro.every((l) => l.stato === 'fatto' || l.stato === 'errore' || l.stato === 'annullato')) {
    bobine?.imposta(1, 0, false, true);
    setTimeout(() => { if (s.vista === 'lavoro') { s.vista = 'fatto'; ui.tutto(); } }, 700);
    if (giro.some((l) => l.stato === 'fatto')) dinDon(); else bu();
  }
}
