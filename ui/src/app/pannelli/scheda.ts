// La sezione richiudibile «Info del file»: quello che si corregge finisce nel file convertito.
// L'originale non si tocca. I campi sono solo quelli che il formato di arrivo sa tenere.

import { chiedi } from '../../ponte';
import type { Categoria, Formato, OpzioniScheda } from '../../ponte';
import { h } from '../../util';
import { icone } from '../../icone';
import { clac } from '../../suoni';
import { s } from '../stato';

interface Letta {
  titolo: string; artista: string; album: string; anno: string; traccia: string;
  commento: string; autore: string; descrizione: string; dataScatto: string;
  copertina: string | null; campi: string[];
}

const ETICHETTE: Record<string, string> = {
  titolo: 'Titolo', artista: 'Artista', album: 'Album', anno: 'Anno', traccia: 'Traccia',
  commento: 'Commento', autore: 'Autore', descrizione: 'Descrizione', data: 'Data dello scatto',
};

/** Resta aperta anche quando la piastra si ridisegna (un clic su un formato). */
let aperta = false;

function va(c: Categoria, f: Formato) {
  if (c === 'audio') return true;
  if (c === 'video' && ['.mp4', '.mkv', '.mov', '.webm', '.mp3', '.wav', '.flac', '.m4a', '.ogg', '.opus', '.wma'].includes(f.estensione)) return true;
  if (c === 'immagine' && ['.jpg', '.jpeg', '.tif', '.tiff', '.png', '.webp', '.heic', '.bmp'].includes(f.estensione)) return true;
  return false;
}

export function sezioneScheda(c: Categoria, f: Formato): HTMLElement | null {
  if (!va(c, f)) return null;
  const corpo = h('div.corpo-scheda');
  const el = h(`div.scheda-file${aperta ? '.aperta' : ''}`);
  el.append(
    h('button.testa-scheda', { type: 'button', title: 'Apri o chiudi le info del file', onclick: () => {
      clac();
      aperta = !el.classList.contains('aperta');
      el.classList.toggle('aperta', aperta);
      if (aperta) void riempi();
    } }, 'INFO DEL FILE', h('span.freccia', { html: icone.giu })),
    corpo,
  );
  if (aperta) void riempi();
  return el;

  async function riempi() {
    const fa = s.fileAttivo ? s.file.get(s.fileAttivo) : undefined;
    if (!fa) { corpo.replaceChildren(h('p.scheda-nota', null, 'Scegli un file nella coda.')); return; }
    const id = fa.id;
    corpo.replaceChildren(h('p.scheda-nota', null, 'Leggo…'));
    let letti: Letta | null = null;
    try { letti = await chiedi<Letta>('schedaFile', { id, formato: f.id }); }
    catch (e) { corpo.replaceChildren(h('p.scheda-nota', null, (e as Error).message)); return; }
    if (s.fileAttivo !== id) return;
    const campi = letti.campi ?? [];
    const gia = s.schede.get(id);
    const nota = h('p.scheda-nota', null, campi.length
      ? 'Queste info finiscono nel file convertito. L\'originale resta com\'è.'
      : 'Questo formato non conserva titolo, artista, data o copertina. Non si inventano campi che poi si perdono.');
    if (!campi.length) { corpo.replaceChildren(nota); return; }

    const valori: Record<string, string> = {
      titolo: gia?.scrivi ? gia.titolo ?? '' : letti.titolo,
      artista: gia?.scrivi ? gia.artista ?? '' : letti.artista,
      album: gia?.scrivi ? gia.album ?? '' : letti.album,
      anno: gia?.scrivi ? gia.anno ?? '' : letti.anno,
      traccia: gia?.scrivi ? gia.traccia ?? '' : letti.traccia,
      commento: gia?.scrivi ? gia.commento ?? '' : letti.commento,
      autore: gia?.scrivi ? gia.autore ?? '' : letti.autore,
      descrizione: gia?.scrivi ? gia.descrizione ?? '' : letti.descrizione,
      data: gia?.scrivi ? gia.dataScatto ?? '' : letti.dataScatto,
    };
    const campiTesto = campi.filter((x) => x !== 'copertina');
    const griglia = h('div.scheda-campi');
    const input: Record<string, HTMLInputElement> = {};
    for (const nome of campiTesto) {
      const campo = h<HTMLInputElement>('input.campo', { value: valori[nome] ?? '', spellcheck: false, 'aria-label': ETICHETTE[nome] ?? nome });
      campo.addEventListener('input', () => tocca());
      input[nome] = campo;
      griglia.append(h('label', null, ETICHETTE[nome] ?? nome, campo));
    }

    let copertina = gia?.scrivi ? gia.copertina ?? null : null;
    const anteprima = h<HTMLImageElement>('img', { alt: '' });
    const dida = h('span.scheda-dida');
    function mostraAnteprima() {
      const url = copertina && copertina !== 'nessuna' && !copertina.startsWith('generata:') && !copertina.includes('\\') && !copertina.includes('/')
        ? null
        : copertina && (copertina.startsWith('data:') ? copertina : null);
      if (copertina === 'nessuna') { anteprima.hidden = true; dida.textContent = 'Senza copertina'; }
      else if (copertina?.startsWith('generata:')) { anteprima.hidden = true; dida.textContent = 'Una disegnata, scelta qui sotto'; }
      else if (url) { anteprima.src = url; anteprima.hidden = false; dida.textContent = ''; }
      else if (copertina && (copertina.includes('\\') || copertina.includes('/'))) { anteprima.hidden = true; dida.textContent = copertina.split(/[/\\]/).pop() ?? 'Immagine scelta'; }
      else if (letti?.copertina) { anteprima.src = letti.copertina; anteprima.hidden = false; dida.textContent = 'Quella del file'; }
      else { anteprima.hidden = true; dida.textContent = 'Nessuna copertina'; }
    }
    mostraAnteprima();

    const rigaCover = campi.includes('copertina') ? h('div.scheda-cover', null,
      anteprima,
      h('div', null,
        dida,
        h('div.scheda-cover-tasti', null,
          h('button.tasto.piccolo', { type: 'button', onclick: () => { clac(); copertina = null; mostraAnteprima(); tocca(); segnaVariante(); } }, 'Tieni'),
          h('button.tasto.piccolo', { type: 'button', onclick: () => void scegliFile() }, 'Scegli…'),
          h('button.tasto.piccolo', { type: 'button', onclick: () => { clac(); copertina = 'nessuna'; mostraAnteprima(); tocca(); segnaVariante(); } }, 'Togli'),
        ))) : null;

    const varianti = campi.includes('copertina') ? h('div.scheda-varianti') : null;
    corpo.replaceChildren(...[nota, campiTesto.length ? griglia : null, rigaCover, varianti].filter((x): x is HTMLElement => !!x));

    function stato(): OpzioniScheda {
      return {
        scrivi: true,
        titolo: input.titolo?.value ?? '',
        artista: input.artista?.value ?? '',
        album: input.album?.value ?? '',
        anno: input.anno?.value ?? '',
        traccia: input.traccia?.value ?? '',
        commento: input.commento?.value ?? '',
        autore: input.autore?.value ?? '',
        descrizione: input.descrizione?.value ?? '',
        dataScatto: input.data?.value ?? '',
        copertina,
      };
    }
    function tocca() { s.schede.set(id, stato()); }
    function segnaVariante() {
      varianti?.querySelectorAll('button').forEach((b) => b.classList.toggle('su', copertina === `generata:${(b as HTMLElement).dataset.n}`));
    }

    async function scegliFile() {
      clac();
      try {
        const percorso = await chiedi<string | null>('scegliImmagine');
        if (!percorso) return;
        copertina = percorso;
        mostraAnteprima();
        tocca();
        segnaVariante();
      } catch (e) { dida.textContent = (e as Error).message; }
    }

    if (varianti) {
      try {
        const urls = await chiedi<string[]>('copertine', { titolo: input.titolo?.value || fa.nome.replace(/\.[^.]+$/, '') });
        if (s.fileAttivo !== id) return;
        urls.forEach((url, n) => {
          varianti.append(h('button', {
            type: 'button', title: 'Usa questa copertina disegnata', dataset: { n: String(n) },
            style: { backgroundImage: `url(${url})` },
            onclick: () => { clac(); copertina = `generata:${n}`; mostraAnteprima(); tocca(); segnaVariante(); },
          }));
        });
        segnaVariante();
      } catch { /* le disegnate non sono arrivate: restano tieni, scegli e togli */ }
    }
  }
}
