// La scatola: cosa c'è dentro un archivio senza estrarlo. Albero di cartelle, pesi con la barretta, il totale,
// e «Estrai qui» che lo fa il convertitore (la finestrella in basso a destra).
import { chiedi } from '../ponte';
import { h, peso } from '../util';
import { iconaCategoria, coloreCategoria } from '../icone';
import { attesa, errore, ic, type Contesto, type Vista } from './comune';

interface Voce { percorso: string; peso: number; cartella: boolean; data: string | null }
interface Nodo { nome: string; peso: number; file: number; figli: Map<string, Nodo>; cartella: boolean; data: string | null }

const categoria = (nome: string) => {
  const e = nome.split('.').pop()?.toLowerCase() ?? '';
  if (/^(mp4|mkv|mov|avi|webm|wmv|m4v)$/.test(e)) return 'video';
  if (/^(mp3|flac|wav|m4a|ogg|opus|aac|wma)$/.test(e)) return 'audio';
  if (/^(jpe?g|png|gif|webp|heic|bmp|tiff?|avif|svg|raw|cr2|nef|arw|dng)$/.test(e)) return 'immagine';
  if (e === 'pdf') return 'pdf';
  if (/^(docx?|odt|rtf|pages)$/.test(e)) return 'documento';
  if (/^(xlsx?|ods|csv)$/.test(e)) return 'foglio';
  if (/^(zip|7z|rar|tar|gz|xz|iso)$/.test(e)) return 'archivio';
  if (/^(txt|md|json|xml|html?|js|ts|cs|py|log|ini)$/.test(e)) return 'testo';
  return 'altro';
};

export async function monta(c: Contesto): Promise<Vista> {
  c.palco.append(attesa('Guardo dentro la scatola…'));
  let voci: Voce[];
  try { voci = await chiedi<Voce[]>('archivio'); }
  catch (e) { c.palco.replaceChildren(errore((e as Error).message)); return { tasti: [], smonta() {} }; }
  if (!c.viva()) return { tasti: [], smonta() {} };

  // l'albero dalle voci "a/b/c.txt"
  const radice: Nodo = { nome: '', peso: 0, file: 0, figli: new Map(), cartella: true, data: null };
  for (const v of voci) {
    const parti = v.percorso.split('/').filter(Boolean);
    let n = radice;
    parti.forEach((p, i) => {
      const ultimo = i === parti.length - 1;
      if (!n.figli.has(p)) n.figli.set(p, { nome: p, peso: 0, file: 0, figli: new Map(), cartella: !ultimo || v.cartella, data: ultimo ? v.data : null });
      n = n.figli.get(p)!;
    });
    if (!v.cartella) { n.peso = v.peso; n.file = 1; }
  }
  const somma = (n: Nodo): void => { if (!n.cartella) return; n.peso = 0; n.file = 0; for (const f of n.figli.values()) { somma(f); n.peso += f.peso; n.file += f.file; } };
  somma(radice);

  const filtro = h<HTMLInputElement>('input.t-cerca', { placeholder: 'Cerca dentro…' });
  const albero = h('div.z-albero');
  const riga = (n: Nodo, livello: number, max: number): HTMLElement => {
    const cat = n.cartella ? 'cartella' : categoria(n.nome);
    const figli = h('div.z-figli');
    const r = h(`div.z-voce${n.cartella ? '.cartella' : ''}${livello < 1 ? '.aperta' : ''}`, { dataset: { nome: n.nome.toLowerCase() } },
      h('button.z-riga', { style: { '--liv': String(livello), '--tinta': coloreCategoria[cat] } },
        n.cartella ? h('i.j-freccia') : h('i.z-vuoto'),
        h('span.z-icona', { html: iconaCategoria[cat] }),
        h('span.z-nome', { title: n.nome }, n.nome),
        n.cartella ? h('span.z-conta', null, `${n.file} file`) : null,
        h('span.z-barra', null, h('i', { style: { width: `${max ? Math.max(1, (n.peso / max) * 100) : 0}%` } })),
        h('span.z-peso', null, peso(n.peso))),
      figli);
    let fatto = false;
    const riempi = () => {
      if (fatto) return;
      fatto = true;
      const ord = [...n.figli.values()].sort((a, b) => (a.cartella === b.cartella ? a.nome.localeCompare(b.nome, 'it', { numeric: true }) : a.cartella ? -1 : 1));
      const m = Math.max(...ord.map((x) => x.peso), 1);
      figli.append(...ord.map((x) => riga(x, livello + 1, m)));
    };
    if (n.cartella) {
      if (livello < 1) riempi();
      r.firstElementChild!.addEventListener('click', () => { riempi(); r.classList.toggle('aperta'); });
    }
    return r;
  };
  const primi = [...radice.figli.values()].sort((a, b) => (a.cartella === b.cartella ? a.nome.localeCompare(b.nome, 'it', { numeric: true }) : a.cartella ? -1 : 1));
  const max = Math.max(...primi.map((x) => x.peso), 1);
  albero.append(...primi.map((x) => riga(x, 0, max)));

  // la ricerca guarda tutti i nomi, e mostra i percorsi trovati
  const trovati = h('div.z-trovati');
  filtro.addEventListener('input', () => {
    const q = filtro.value.toLowerCase().trim();
    albero.hidden = !!q;
    trovati.hidden = !q;
    if (!q) return;
    const tutti = voci.filter((v) => !v.cartella && v.percorso.toLowerCase().includes(q)).slice(0, 400);
    trovati.replaceChildren(...tutti.map((v) => h('div.z-riga.piatta', { style: { '--tinta': coloreCategoria[categoria(v.percorso)] } },
      h('span.z-icona', { html: iconaCategoria[categoria(v.percorso)] }), h('span.z-nome', { title: v.percorso }, v.percorso), h('span.z-peso', null, peso(v.peso)))));
    if (!tutti.length) trovati.append(h('p.c-nota', null, 'Niente con questo nome.'));
  });
  trovati.hidden = true;

  const rapporto = c.scheda.peso && radice.peso ? c.scheda.peso / radice.peso : 0;
  c.palco.replaceChildren(h('div.z-box', null,
    h('div.z-testa', null,
      h('div.z-scatola', { html: iconaCategoria.archivio }),
      h('div', null,
        h('b', null, `${radice.file.toLocaleString('it-IT')} file · ${peso(radice.peso)} estratti`),
        h('span.c-nota', null, rapporto ? `Compresso al ${Math.round(rapporto * 100)}%: risparmia ${peso(radice.peso - c.scheda.peso)}` : '')),
      h('span.l-spazio'),
      h('div.t-cerca-box', null, h('span', { html: ic.cerca }), filtro),
      h('button.l-converti', { onclick: () => { void chiedi('estrai'); c.hud('Estraggo qui accanto…', ic.estrai); } }, h('span', { html: ic.estrai }), 'Estrai qui')),
    h('div.z-scorre', null, albero, trovati)));

  return {
    tasti: [
      { k: ['ctrl+f', '/'], etichetta: 'Ctrl F', cosa: 'Cerca dentro', fai: () => filtro.focus() },
      { k: ['e'], etichetta: 'E', cosa: 'Estrai qui', fai: () => { void chiedi('estrai'); c.hud('Estraggo qui accanto…', ic.estrai); } },
    ],
    info: () => `${radice.file.toLocaleString('it-IT')} file dentro`,
    smonta() {},
  };
}
