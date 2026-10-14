// I controlli delle regolazioni: gruppo con titolo, bottoni a scelta, interruttore, cursore.
import { h, peso, pesoDisplay } from '../util';
import { clac } from '../suoni';
import { ui } from './stato';

export function gruppo(titolo: string, ...figli: (Node | null)[]) {
  return h('div.gruppo', null, h('div.gruppo-titolo', null, titolo), ...figli.filter(Boolean) as Node[]);
}

export function bottoni<T extends string | number>(valori: { v: T; testo: string; nota?: string; spento?: boolean }[], attuale: T, cambia: (v: T) => void) {
  return h('div.bottoni', null, ...valori.map((x) => h(`button.tasto${x.v === attuale ? '.su' : ''}`, {
    disabled: x.spento, title: x.nota,
    onclick: () => { clac(); cambia(x.v); ui.scegli(); ui.piede(); },
  }, x.testo, x.nota ? h('small', null, x.nota) : null)));
}

export function interruttore(testo: string, acceso: boolean, cambia: (v: boolean) => void, nota?: string) {
  return h(`button.interruttore${acceso ? '.su' : ''}`, { onclick: () => { clac(); cambia(!acceso); ui.scegli(); } }, h('i.levetta'), h('span', null, testo, nota ? h('small', null, nota) : null));
}

export function cursore(min: number, max: number, valore: number, cambia: (v: number) => void, etichette?: [string, string], passo = 1) {
  const input = h('input.cursore', { type: 'range', min, max, step: passo, value: valore }) as HTMLInputElement;
  input.style.setProperty('--p', `${((valore - min) / (max - min)) * 100}%`);
  input.addEventListener('input', () => {
    input.style.setProperty('--p', `${((+input.value - min) / (max - min)) * 100}%`);
    cambia(+input.value);
  });
  return h('div.cursore-box', null, input, etichette ? h('div.cursore-etichette', null, h('span', null, etichette[0]), h('span', null, etichette[1])) : null);
}

/**
 * Lo slider del peso: scala logaritmica fra il più leggero e il più pesante (fra 10 MB e 10 GB deve servire tutto).
 * `cambia` riceve i byte mentre si trascina. Lo usano il video e la foto.
 */
export function faderPeso(min: number, max: number, valore: number, cambia: (byte: number) => void, etichettaMax: string) {
  const aPos = (b: number) => Math.log(Math.max(b, min) / min) / Math.log(max / min);
  const daPos = (x: number) => min * Math.pow(max / min, x);
  const fader = h('input.fader', { type: 'range', min: 0, max: 1000, value: Math.round(aPos(valore) * 1000) }) as HTMLInputElement;
  const segna = () => fader.style.setProperty('--p', `${(+fader.value / 10).toFixed(1)}%`);
  segna();
  fader.addEventListener('input', () => { segna(); cambia(Math.round(daPos(+fader.value / 1000))); });
  fader.addEventListener('change', () => clac());
  return h('div.fader-box', null, fader, h('div.fader-scala', null, h('span', null, peso(min, 0)), h('span', null, etichettaMax)));
}

/** Aggiorna il display a sette segmenti del peso (numero, unità, riga sotto) senza ridisegnare il pannello. */
export function mostraPeso(byte: number, sotto: string, approssimato = false) {
  const d = pesoDisplay(byte);
  const n = document.getElementById('dsp-numero');
  if (n) n.textContent = approssimato ? `~${d.numero}` : d.numero;
  const u = document.getElementById('dsp-unita');
  if (u) u.textContent = d.unita;
  const t = document.getElementById('dsp-sotto');
  if (t) t.textContent = sotto;
}
