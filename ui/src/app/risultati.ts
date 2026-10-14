// Il banco a lavoro finito: il timbro FATTO, un risultato per file, l'originale nel Cestino, il confronto prima/dopo.

import { chiedi } from '../ponte';
import type { Lavoro } from '../ponte';
import { h, peso, durata, numero, clamp } from '../util';
import { icone, iconaCategoria } from '../icone';
import { clac } from '../suoni';
import { s, ui } from './stato';
import { banco, mostraAvviso } from './scheletro';


export function disegnaFatto() {
  const giro = s.giro.map((i) => s.lavori.get(i)!).filter(Boolean);
  const ok = giro.filter((l) => l.stato === 'fatto');
  const prima = ok.reduce((a, l) => a + l.pesoPrima, 0);
  const dopo = ok.reduce((a, l) => a + (l.pesoDopo ?? 0), 0);
  const secondi = Math.max(...giro.map((l) => l.secondi), 0);
  const risparmio = prima - dopo;
  banco.replaceChildren(
    h('div.timbro', null,
      h('div.timbro-scritta', null, ok.length === giro.length ? 'FATTO' : ok.length ? 'QUASI' : 'NIENTE'),
      h('div.timbro-sotto', null,
        `${ok.length} ${ok.length === 1 ? 'file' : 'file'} · ${peso(prima)} → ${peso(dopo)}`,
        risparmio > 0 ? h('b', null, ` · risparmiati ${peso(risparmio)}`) : null,
        secondi > 0 ? ` · in ${durata(secondi)}` : null)),
    h('div.risultati', null, ...giro.map(schedaRisultato)),
  );
}

export function schedaRisultato(l: Lavoro) {
  const f = l.fileId ? s.file.get(l.fileId) : undefined;
  const nomeUscitaFile = l.uscita?.split('\\').pop() ?? '';
  if (l.stato !== 'fatto') {
    const dettaglio = h('pre.dettaglio', { hidden: true }, l.dettaglio ?? '');
    return h(`div.risultato.${l.stato}`, null,
      h('div.ris-icona', { html: l.stato === 'annullato' ? icone.ferma : icone.attenzione }),
      h('div.ris-testo', null,
        h('div.ris-nome', null, f?.nome ?? l.sorgenti[0]),
        h('div.ris-errore', null, l.stato === 'annullato' ? 'Annullato: non è rimasto niente a metà.' : l.errore ?? 'Errore'),
        l.dettaglio ? h('button.link', { onclick: () => { dettaglio.hidden = !dettaglio.hidden; } }, 'Dettagli tecnici') : null,
        l.errore?.includes('LibreOffice') ? h('button.link', { onclick: () => chiedi('link', { url: 'https://it.libreoffice.org/download/download/' }) }, 'Scarica LibreOffice') : null,
        dettaglio),
    );
  }
  const rapporto = l.pesoPrima > 0 && l.pesoDopo != null ? l.pesoDopo / l.pesoPrima : 1;
  // senza l'originale (andato nel Cestino) il confronto non ha da cosa leggere
  const confrontabile = !l.nelCestino && f && (f.categoria === 'video' || f.categoria === 'immagine') && l.sorgenti.length === 1 && !['video.mp3', 'video.wav', 'video.srt', 'img.pdf', 'img.txt'].includes(l.formato);
  return h('div.risultato.fatto', null,
    h('div.ris-figura', null, f?.anteprima ? h('img', { src: f.anteprima }) : h('span', { html: iconaCategoria[f?.categoria ?? 'altro'] }), h('i.ok', { html: icone.ok })),
    h('div.ris-testo', null,
      h('div.ris-nome', { title: l.uscita ?? '' }, nomeUscitaFile),
      h('div.ris-pesi', null,
        h('span', null, peso(l.pesoPrima)), h('span.ris-freccia', { html: icone.freccia }), h('b', null, peso(l.pesoDopo)),
        l.pesoDopo != null && l.pesoPrima > 0 ? h(`span.ris-delta${rapporto <= 1 ? '.giu' : '.su'}`, null, `${rapporto <= 1 ? '−' : '+'}${numero(Math.abs(1 - rapporto) * 100)}%`) : null,
        l.nelCestino ? h('span.ris-cestino', { title: 'Se ti serve, lo ripeschi dal Cestino' }, h('span', { html: icone.cestino }), 'originale nel Cestino') : null,
        !l.nelCestino && l.notaCestino ? h('span.ris-cestino.tenuto', { title: l.notaCestino }, 'originale tenuto: ' + l.notaCestino) : null),
      h('div.ris-barre', null, h('i.prima'), h('i.dopo', { style: { width: `${Math.min(100, rapporto * 100)}%` } })),
    ),
    h('div.ris-azioni', null,
      !l.nelCestino && l.sorgenti.length === 1 ? tastoCestino(null, [l], 'tasto-icona') : null,
      h('button.tasto-icona', { title: 'Guarda nel lettore', onclick: () => chiedi('guarda', { percorso: l.uscita }), html: icone.occhio }),
      h('button.tasto-icona', { title: 'Apri col programma di Windows', onclick: () => chiedi('apri', { percorso: l.uscita }), html: icone.apri }),
      h('button.tasto-icona', { title: 'Mostra nella cartella', onclick: () => chiedi('mostra', { percorso: l.uscita }), html: icone.cartella }),
      confrontabile ? h('button.tasto-icona', { title: 'Confronta prima e dopo', onclick: () => void confronta(l), html: icone.confronta }) : null,
    ),
  );
}

/**
 * L'originale nel Cestino, a mano: il primo clic chiede «Sicuro?», il secondo lo fa. Si ripesca dal Cestino.
 * Su chiavette e dischi di rete Windows il Cestino non ce l'ha: lì resta, e si dice perché.
 */
export function tastoCestino(testo: string | null, lavori: Lavoro[], classe: 'tasto-icona' | 'secondario') {
  let armato: number | undefined;
  const b = h<HTMLButtonElement>(`button.${classe}.cestina`, { title: lavori.length > 1 ? 'Gli originali nel Cestino' : 'L\'originale nel Cestino (si ripesca da lì)' },
    h('span', { html: icone.cestino }), testo ? h('span.t', null, testo) : null);
  b.addEventListener('click', async () => {
    if (!armato) {
      clac();
      b.classList.add('armato');
      if (testo) (b.querySelector('.t') as HTMLElement).textContent = 'SICURO? CLICCA ANCORA';
      else b.title = 'Clicca ancora per buttarlo nel Cestino';
      armato = window.setTimeout(() => { armato = undefined; b.classList.remove('armato'); if (testo) (b.querySelector('.t') as HTMLElement).textContent = testo; }, 3500);
      return;
    }
    clearTimeout(armato);
    clac(true);
    b.disabled = true;
    const no: string[] = [];
    for (const l of lavori) {
      const r = await chiedi<{ ok: boolean; perche: string | null }>('cestino', { lavoro: l.id }).catch((e) => ({ ok: false, perche: (e as Error).message }));
      if (r.ok) l.nelCestino = true;
      else if (r.perche) no.push(r.perche);
    }
    if (no.length) mostraAvviso(`Rimasto dov'era: ${no[0]}`);
    if (s.vista === 'fatto') { disegnaFatto(); ui.piede(); }
    if (s.stato.modo === 'rapido') ui.rapido();
  });
  return b;
}

// ——— confronto prima / dopo ———

export async function confronta(l: Lavoro, posizione = 0.35) {
  clac();
  const velo = h('div.velo', { onclick: (e: Event) => { if (e.target === velo) velo.remove(); } });
  const scatola = h('div.confronto');
  velo.append(scatola);
  document.body.append(velo);
  scatola.append(h('div.attendi', null, h('i.led.lampeggia'), 'Prendo lo stesso fotogramma dai due file…'));
  try {
    const r = await chiedi<{ prima: string; dopo: string; tempo?: number }>('confronta', { lavoro: l.id, posizione });
    const divisore = h('div.divisore', null, h('span.maniglia', { html: icone.confronta }));
    const dopo = h('img.dopo', { src: r.dopo });
    const prima = h<HTMLImageElement>('img.prima', { src: r.prima });
    // il riquadro prende le proporzioni dell'immagine: niente bande nere ai lati di una foto 4:3
    prima.addEventListener('load', () => {
      const rapporto = prima.naturalWidth / prima.naturalHeight;
      const largo = scatola.clientWidth - 24, alto = window.innerHeight - 140;
      quadro.style.width = `${Math.min(largo, alto * rapporto)}px`;
      quadro.style.aspectRatio = `${prima.naturalWidth} / ${prima.naturalHeight}`;
      requestAnimationFrame(() => { const b = quadro.getBoundingClientRect(); muovi(b.left + b.width / 2); });
    });
    const quadro = h('div.quadro-confronto', null, prima, dopo, divisore,
      h('span.cartellino.sx', null, `ORIGINALE · ${peso(l.pesoPrima)}`), h('span.cartellino.dx', null, `CONVERTITO · ${peso(l.pesoDopo)}`));
    const muovi = (x: number) => {
      const b = quadro.getBoundingClientRect();
      const p = clamp((x - b.left) / b.width, 0, 1);
      dopo.style.clipPath = `inset(0 0 0 ${p * 100}%)`;
      divisore.style.left = `${p * 100}%`;
    };
    let tieni = false;
    quadro.addEventListener('pointerdown', (e) => { tieni = true; quadro.setPointerCapture(e.pointerId); muovi(e.clientX); });
    quadro.addEventListener('pointermove', (e) => { if (tieni) muovi(e.clientX); });
    quadro.addEventListener('pointerup', () => (tieni = false));
    const video = l.formato.startsWith('video');
    scatola.replaceChildren(
      h('div.confronto-testa', null, h('b', null, 'PRIMA · DOPO'), h('span', null, 'trascina la linea'),
        video ? h('div.bottoni', null, ...[0.1, 0.35, 0.6, 0.85].map((p) => h(`button.tasto.piccolo${p === posizione ? '.su' : ''}`, { onclick: () => { velo.remove(); void confronta(l, p); } }, `${Math.round(p * 100)}%`))) : null,
        h('button.btn-fin', { html: icone.chiudi, onclick: () => velo.remove() })),
      quadro);
    requestAnimationFrame(() => { const b = quadro.getBoundingClientRect(); muovi(b.left + b.width / 2); });
  } catch (e) {
    scatola.replaceChildren(h('div.attendi', null, 'Il confronto non è riuscito: ', String((e as Error).message)), h('button.secondario', { onclick: () => velo.remove() }, 'CHIUDI'));
  }
}
