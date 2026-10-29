// La scatola: cosa c'è dentro un archivio senza estrarlo. Un albero di cartelle con i pesi. Si scelgono cartelle e file
// (clic, Ctrl per più) e si trascinano: sulla zona in alto escono in una cartella accanto all'archivio; fuori dalla
// finestra (Esplora file, il desktop, un'altra app) escono dove li lasci, come file veri.
import { chiedi } from '../ponte';
import { h, peso } from '../util';
import { iconaCategoria, coloreCategoria } from '../icone';
import { attesa, errore, ic, type Contesto, type Vista } from './comune';

interface Voce { percorso: string; peso: number; cartella: boolean; data: string | null }
interface Nodo { nome: string; percorso: string; peso: number; file: number; figli: Map<string, Nodo>; cartella: boolean; data: string | null }

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

/** Le figurine dei file dentro l'archivio (foto, video, PDF): si chiedono solo quando la riga entra in vista. */
function miniature(c: Contesto) {
  const attesa = new WeakMap<Element, { percorso: string; peso: number }>();
  const fatte = new Map<string, string | null>();
  const metti = (icona: Element, url: string) => {
    icona.classList.add('con-mini');
    icona.replaceChildren(h('img', { src: url, alt: '', draggable: 'false' }));
  };
  const osservatore = new IntersectionObserver((righe) => {
    for (const r of righe) {
      if (!r.isIntersecting) continue;
      osservatore.unobserve(r.target);
      const voce = attesa.get(r.target);
      if (!voce || !c.viva()) continue;
      const nota = fatte.get(voce.percorso);
      if (nota) { metti(r.target, nota); continue; }
      void chiedi<{ url: string | null }>('miniaturaVoce', { voce: voce.percorso, peso: voce.peso, lato: 96 })
        .then((x) => { if (x.url) { fatte.set(voce.percorso, x.url); if (c.viva()) metti(r.target, x.url); } })
        .catch(() => { /* resta l'icona */ });
    }
  }, { rootMargin: '160px' });
  return {
    /** Segna l'icona di una riga: se il file ha una figurina, arriverà quando la riga si vede. */
    segna(icona: Element, percorso: string, pesoVoce: number) {
      if (pesoVoce <= 0 || !['immagine', 'video', 'pdf'].includes(categoria(percorso))) return;
      attesa.set(icona, { percorso, peso: pesoVoce });
      osservatore.observe(icona);
    },
    ferma() { osservatore.disconnect(); },
  };
}

export async function monta(c: Contesto): Promise<Vista> {
  c.palco.append(attesa('Guardo dentro la scatola…'));
  let voci: Voce[];
  try { voci = await chiedi<Voce[]>('archivio'); }
  catch (e) { c.palco.replaceChildren(errore((e as Error).message)); return { tasti: [], smonta() {} }; }
  if (!c.viva()) return { tasti: [], smonta() {} };

  const mini = miniature(c);

  // l'albero dalle voci "a/b/c.txt"; ogni nodo sa il suo percorso dentro l'archivio ("a/", "a/b/c.txt")
  const radice: Nodo = { nome: '', percorso: '', peso: 0, file: 0, figli: new Map(), cartella: true, data: null };
  for (const v of voci) {
    const parti = v.percorso.split('/').filter(Boolean);
    let n = radice;
    parti.forEach((p, i) => {
      const ultimo = i === parti.length - 1;
      const cartella = !ultimo || v.cartella;
      if (!n.figli.has(p)) n.figli.set(p, { nome: p, percorso: n.percorso + p + (cartella ? '/' : ''), peso: 0, file: 0, figli: new Map(), cartella, data: ultimo ? v.data : null });
      n = n.figli.get(p)!;
    });
    if (!v.cartella) { n.peso = v.peso; n.file = 1; }
  }
  const somma = (n: Nodo): void => { if (!n.cartella) return; n.peso = 0; n.file = 0; for (const f of n.figli.values()) { somma(f); n.peso += f.peso; n.file += f.file; } };
  somma(radice);

  // ——— la scelta: clic per sceglierne una, Ctrl per aggiungerne, trascina sulla zona per estrarre ———
  const scelte = new Set<string>();
  const righe = new Map<string, Set<HTMLElement>>();   // ogni voce può stare nell'albero e nella ricerca
  const bEstrai = h('button.l-converti', { onclick: () => void estraiScelte() }, h('span', { html: ic.estrai }), 'Estrai qui');

  const aggiornaScelte = () => {
    for (const [p, els] of righe) for (const el of els) el.classList.toggle('scelta', scelte.has(p));
    const n = scelte.size;
    bEstrai.lastChild!.textContent = n ? (n === 1 ? 'Estrai la scelta' : `Estrai le ${n} scelte`) : 'Estrai qui';
  };

  const selezionaRiga = (p: string, e: MouseEvent) => {
    if (e.ctrlKey) { if (scelte.has(p)) scelte.delete(p); else scelte.add(p); }
    else { scelte.clear(); scelte.add(p); }
    aggiornaScelte();
  };

  /** Una riga che si sceglie e si trascina: porta con sé la scelta (o solo sé, se non era scelta). */
  const fila = (p: string, el: HTMLElement) => {
    if (!righe.has(p)) righe.set(p, new Set());
    righe.get(p)!.add(el);
    if (scelte.has(p)) el.classList.add('scelta');
    el.addEventListener('pointerdown', (e) => { if (e.button === 0) iniziaTrascinamento(p, el, e); });
  };

  // ——— il trascinamento: lo guida il mouse, non il «drag and drop» della pagina (quello non sa portare file fuori) ———
  // Dopo qualche pixel i file si estraggono in una cartella temporanea. Se il mouse lascia sopra la zona in alto, si
  // estrae accanto all'archivio; se esce dalla finestra con il tasto premuto, parte il trascinamento di Windows coi file
  // veri (Esplora file, il desktop, un'altra app).
  let appenaTrascinato = false;
  function iniziaTrascinamento(p: string, el: HTMLElement, giu: PointerEvent) {
    const x0 = giu.clientX, y0 = giu.clientY;
    let attivo = false, fuori = false;
    let voci: string[] = [];
    let fantasma: HTMLElement | null = null;
    let pronto: Promise<{ id: string }> | null = null;

    const sopraLaZona = (m: PointerEvent) => { const r = zona.getBoundingClientRect(); return m.clientX >= r.left && m.clientX <= r.right && m.clientY >= r.top && m.clientY <= r.bottom; };
    const finisci = () => {
      el.removeEventListener('pointermove', muovi);
      el.removeEventListener('pointerup', lascia);
      el.removeEventListener('pointercancel', finisci);
      try { el.releasePointerCapture(giu.pointerId); } catch { /* già rilasciato */ }
      fantasma?.remove();
      zona.classList.remove('sopra', 'trascina');
      if (attivo) { appenaTrascinato = true; setTimeout(() => (appenaTrascinato = false), 0); }
    };
    async function portaFuori() {
      c.stato('Preparo i file da portare fuori…');
      try {
        const { id } = await pronto!;
        c.stato(null);
        await chiedi('trascinaFuori', { id });
      } catch (e) { c.stato(null); c.hud((e as Error).message, ic.estrai); }
      finally { finisci(); }
    }
    function muovi(m: PointerEvent) {
      if (!attivo) {
        if (Math.hypot(m.clientX - x0, m.clientY - y0) < 6) return;
        attivo = true;
        try { el.setPointerCapture(giu.pointerId); } catch { /* senza cattura funziona lo stesso dentro la finestra */ }
        if (!scelte.has(p)) { scelte.clear(); scelte.add(p); aggiornaScelte(); }
        voci = [...scelte];
        fantasma = h('div.z-fantasma', null, h('span', { html: ic.estrai }), voci.length === 1 ? '1 voce' : `${voci.length} voci`);
        document.body.append(fantasma);
        zona.classList.add('trascina');
        pronto = chiedi<{ id: string }>('preparaTrascinamento', { voci });
        pronto.catch(() => { /* lo dice portaFuori, se serve */ });
      }
      fantasma!.style.transform = `translate(${m.clientX + 14}px, ${m.clientY + 12}px)`;
      zona.classList.toggle('sopra', sopraLaZona(m));
      const dentro = m.clientX > 1 && m.clientY > 1 && m.clientX < window.innerWidth - 1 && m.clientY < window.innerHeight - 1;
      if (!dentro && !fuori && (m.buttons & 1)) { fuori = true; void portaFuori(); }
    }
    function lascia(m: PointerEvent) {
      if (!attivo) { finisci(); return; }
      const suZona = !fuori && sopraLaZona(m);
      finisci();
      if (suZona && voci.length) void estrai(voci);
    }
    el.addEventListener('pointermove', muovi);
    el.addEventListener('pointerup', lascia);
    el.addEventListener('pointercancel', finisci);
  }

  const filtro = h<HTMLInputElement>('input.t-cerca', { placeholder: 'Cerca dentro…' });
  const albero = h('div.z-albero');
  const riga = (n: Nodo, livello: number, max: number): HTMLElement => {
    const cat = n.cartella ? 'cartella' : categoria(n.nome);
    const figli = h('div.z-figli');
    const icona = h('span.z-icona', { html: iconaCategoria[cat] });
    if (!n.cartella) mini.segna(icona, n.percorso, n.peso);
    const bottone = h('button.z-riga', { style: { '--liv': String(livello), '--tinta': coloreCategoria[cat] }, title: 'Clic per scegliere · Ctrl per più · trascina fuori dalla finestra o sulla zona in alto per estrarre' },
      n.cartella ? h('i.j-freccia', { title: 'Apri / chiudi' }) : h('i.z-vuoto'),
      icona,
      h('span.z-nome', { title: n.nome }, n.nome),
      n.cartella ? h('span.z-conta', null, `${n.file} file`) : null,
      h('span.z-barra', null, h('i', { style: { width: `${max ? Math.max(1, (n.peso / max) * 100) : 0}%` } })),
      h('span.z-peso', null, peso(n.peso)));
    const r = h(`div.z-voce${n.cartella ? '.cartella' : ''}${livello < 1 ? '.aperta' : ''}`, { dataset: { nome: n.nome.toLowerCase() } }, bottone, figli);
    let fatto = false;
    const riempi = () => {
      if (fatto) return;
      fatto = true;
      const ord = [...n.figli.values()].sort((a, b) => (a.cartella === b.cartella ? a.nome.localeCompare(b.nome, 'it', { numeric: true }) : a.cartella ? -1 : 1));
      const m = Math.max(...ord.map((x) => x.peso), 1);
      figli.append(...ord.map((x) => riga(x, livello + 1, m)));
    };
    if (n.cartella && livello < 1) riempi();
    fila(n.percorso, bottone);
    bottone.addEventListener('click', (e) => {
      if (appenaTrascinato) return;
      // la freccia apre, il resto della riga sceglie
      if (n.cartella && (e.target as HTMLElement).closest('.j-freccia')) { riempi(); r.classList.toggle('aperta'); return; }
      selezionaRiga(n.percorso, e);
    });
    return r;
  };
  const primi = [...radice.figli.values()].sort((a, b) => (a.cartella === b.cartella ? a.nome.localeCompare(b.nome, 'it', { numeric: true }) : a.cartella ? -1 : 1));
  const max = Math.max(...primi.map((x) => x.peso), 1);
  albero.append(...primi.map((x) => riga(x, 0, max)));

  // la ricerca guarda tutti i nomi, e mostra i percorsi trovati (si scelgono e si trascinano anche da lì)
  const trovati = h('div.z-trovati');
  const rigaFile = (v: Voce) => {
    const icona = h('span.z-icona', { html: iconaCategoria[categoria(v.percorso)] });
    mini.segna(icona, v.percorso, v.peso);
    const el = h('button.z-riga.piatta', { style: { '--tinta': coloreCategoria[categoria(v.percorso)] }, title: 'Clic per scegliere · trascina fuori dalla finestra o sulla zona in alto per estrarre' },
      icona, h('span.z-nome', { title: v.percorso }, v.percorso), h('span.z-peso', null, peso(v.peso)));
    fila(v.percorso, el);
    el.addEventListener('click', (e) => { if (!appenaTrascinato) selezionaRiga(v.percorso, e); });
    return el;
  };
  filtro.addEventListener('input', () => {
    const q = filtro.value.toLowerCase().trim();
    albero.hidden = !!q;
    trovati.hidden = !q;
    if (!q) return;
    const tutti = voci.filter((v) => !v.cartella && v.percorso.toLowerCase().includes(q)).slice(0, 400);
    trovati.replaceChildren(...tutti.map(rigaFile));
    if (!tutti.length) trovati.append(h('p.c-nota', null, 'Niente con questo nome.'));
    aggiornaScelte();
  });
  trovati.hidden = true;

  // ——— l'estrazione: la scelta, o tutto se non si è scelto niente ———
  const zona = h('div.z-zona', null, h('span', { html: ic.scarica }), h('span.z-zona-testo', null, `Trascina cartelle e file qui per estrarli accanto all'archivio, o fuori dalla finestra (Esplora file, desktop) per portarli dove vuoi`));

  async function estrai(voci: string[]) {
    c.stato(voci.length === 1 ? 'Estraggo la scelta…' : `Estraggo le ${voci.length} scelte…`);
    try {
      const r = await chiedi<{ cartella: string }>('estraiVoci', { voci });
      c.hud(`Estratto in «${r.cartella.split('\\').pop()}»`, ic.estrai);
      scelte.clear();
      aggiornaScelte();
    } catch (e) { c.hud((e as Error).message, ic.estrai); }
    finally { c.stato(null); }
  }

  async function estraiScelte() {
    if (scelte.size === 0) { void chiedi('estrai'); c.hud('Estraggo tutto accanto all\'archivio…', ic.estrai); return; }
    await estrai([...scelte]);
  }

  const rapporto = c.scheda.peso && radice.peso ? c.scheda.peso / radice.peso : 0;
  c.palco.replaceChildren(h('div.z-box', null,
    h('div.z-testa', null,
      h('div.z-scatola', { html: iconaCategoria.archivio }),
      h('div', null,
        h('b', null, `${radice.file.toLocaleString('it-IT')} file · ${peso(radice.peso)} estratti`),
        h('span.c-nota', null, rapporto && rapporto < 1 ? `Compresso al ${Math.round(rapporto * 100)}%: risparmia ${peso(radice.peso - c.scheda.peso)}` : '')),
      h('span.l-spazio'),
      h('div.t-cerca-box', null, h('span', { html: ic.cerca }), filtro),
      bEstrai),
    zona,
    h('div.z-scorre', null, albero, trovati)));

  return {
    tasti: [
      { k: ['ctrl+f', '/'], etichetta: 'Ctrl F', cosa: 'Cerca dentro', fai: () => filtro.focus() },
      { k: ['e'], etichetta: 'E', cosa: 'Estrai (la scelta, o tutto)', fai: () => void estraiScelte() },
    ],
    info: () => `${radice.file.toLocaleString('it-IT')} file dentro${scelte.size ? ` · ${scelte.size} scelte` : ''}`,
    smonta() { mini.ferma(); },
  };
}
