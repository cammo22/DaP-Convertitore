// La finestrella in basso a destra dal sottomenu del tasto destro: niente scelte, le bobine e un «fatto».

import { chiedi } from '../ponte';
import { h, peso, durata } from '../util';
import { icone } from '../icone';
import { Bobine } from '../grafica';
import { clac } from '../suoni';
import { s } from './stato';
import { app, testa } from './scheletro';
import { tastoCestino } from './risultati';

export let bobineRapide: Bobine | null = null;
export let chiusuraRapida: number | undefined;
/** Il lavoro è finito e la finestrella aspetta di chiudersi da sola. */
let aChiudere = false;

/** La finestrella si chiude da sola a lavoro finito: dopo qualche secondo, e se il mouse c'è sopra aspetta che lo tolga. */
function chiudiTra(ms: number) {
  clearTimeout(chiusuraRapida);
  chiusuraRapida = window.setTimeout(() => void chiedi('finestra', { azione: 'chiudi' }), ms);
}

export function disegnaRapido() {
  const giro = s.giro.map((i) => s.lavori.get(i)!).filter(Boolean);
  const l = giro.find((x) => x.stato === 'corre') ?? giro.find((x) => x.stato === 'attesa') ?? giro[giro.length - 1];
  if (!document.querySelector('.rapido-corpo')) {
    const canvas = h('canvas.bobine-mini') as HTMLCanvasElement;
    app.replaceChildren(testa, h('div.rapido-corpo', null, canvas, h('div.rapido-testo#rapido-testo')));
    requestAnimationFrame(() => { bobineRapide = new Bobine(canvas, '#ff3df2'); disegnaRapido(); });
    app.addEventListener('pointerenter', () => { if (chiusuraRapida) { clearTimeout(chiusuraRapida); chiusuraRapida = undefined; } });
    app.addEventListener('pointerleave', () => { if (aChiudere) chiudiTra(4000); });
    return;
  }
  const t = document.getElementById('rapido-testo')!;
  if (!l) { t.replaceChildren(h('div.r-nome', null, 'Preparo…')); return; }
  const f = l.fileId ? s.file.get(l.fileId) : undefined;
  const finiti = giro.every((x) => x.stato === 'fatto' || x.stato === 'errore' || x.stato === 'annullato');
  const nome = giro.length > 1 ? `${giro.filter((x) => x.stato === 'fatto').length} di ${giro.length} · ${f?.nome ?? ''}` : f?.nome ?? l.sorgenti[0].split('\\').pop() ?? '';
  if (!finiti) {
    // un altro lavoro è partito: la chiusura da sola si annulla
    aChiudere = false;
    clearTimeout(chiusuraRapida);
    chiusuraRapida = undefined;
    const fr = Math.max(0, l.frazione);
    bobineRapide?.imposta(l.frazione < 0 ? 0.5 : fr, l.velocita, l.stato === 'corre');
    t.replaceChildren(
      h('div.r-nome', { title: nome }, nome),
      h('div.r-fase', null, h('span.chip-formato', null, l.etichetta), l.stato === 'attesa' ? 'In coda…' : l.fase ?? 'Lavoro…'),
      h('div.barra-grande.piccola', null, h(`i${l.frazione < 0 ? '.indeterminata' : ''}`, { style: { width: `${l.frazione < 0 ? 100 : fr * 100}%` } })),
      h('div.r-sotto', null,
        h('span.sette-mini', null, `${Math.floor(fr * 100)}%`),
        l.eta != null ? h('span', null, `manca ${durata(l.eta)}`) : null,
        l.velocita ? h('span', null, `×${l.velocita.toFixed(1)}`) : null,
        h('button.link', { onclick: () => { clac(true); void chiedi('annulla', {}); } }, 'Annulla')),
    );
  } else {
    const ok = giro.filter((x) => x.stato === 'fatto');
    bobineRapide?.imposta(1, 0, false, ok.length > 0);
    const ultimo = ok[ok.length - 1];
    t.replaceChildren(
      h('div.r-nome', null, ok.length ? (ultimo.uscita?.split('\\').pop() ?? '') : nome),
      ok.length
        ? h('div.r-fase.ok', null, h('span', { html: icone.ok }), ok.length > 1 ? `${ok.length} file fatti` : 'Fatto', ` · ${peso(ok.reduce((a, x) => a + x.pesoPrima, 0))} → ${peso(ok.reduce((a, x) => a + (x.pesoDopo ?? 0), 0))}`,
            ok.some((x) => x.nelCestino) ? h('span.r-cestino', { html: icone.cestino, title: 'L\'originale è nel Cestino' }) : null)
        : h('div.r-fase.ko', null, h('span', { html: icone.attenzione }), giro[0].errore ?? 'Annullato'),
      h('div.r-azioni', null,
        ultimo ? h('button.tasto.piccolo', { onclick: () => chiedi('guarda', { percorso: ultimo.uscita }) }, 'Guarda') : null,
        ultimo && ok.length === 1 && !ultimo.nelCestino && ultimo.sorgenti.length === 1 ? tastoCestino(null, [ultimo], 'tasto-icona') : null,
        ultimo ? h('button.tasto.piccolo', { onclick: () => chiedi('mostra', { percorso: ultimo.uscita }) }, 'Mostra nella cartella') : null,
        h('button.tasto.piccolo', { onclick: () => chiedi('finestra', { azione: 'espandi' }) }, 'Altro…')),
    );
    // tutto fatto: si chiude da sola dopo 6 secondi (se ci sono errori, dopo 20, il tempo di leggerli); il mouse sopra la ferma
    if (!aChiudere) {
      aChiudere = true;
      if (!app.matches(':hover')) chiudiTra(ok.length === giro.length ? 6000 : 20000);
    }
  }
}
