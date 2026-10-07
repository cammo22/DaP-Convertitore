// DaP Convertitore — la piastra di conversione. Vecchio stile, moderno dentro:
// a sinistra la cassetta d'origine (i file), a destra cosa diventano, sotto il tasto grosso.
import '@fontsource/orbitron/700.css';
import '@fontsource/orbitron/900.css';
import '@fontsource/rajdhani/500.css';
import '@fontsource/rajdhani/600.css';
import '@fontsource/rajdhani/700.css';
import './stile.css';
import { ascolta, avviaFinto, chiedi, dentroApp } from './ponte';
import type { Carico, Categoria, Formato, InfoFile, Lavoro, Opzioni, Piano, Stato } from './ponte';
import { h, peso, pesoDisplay, durata, orologio, numero, risoluzione, clamp, MB, GB } from './util';
import { icone, iconaCategoria, coloreCategoria } from './icone';
import { Bobine, Lancetta } from './grafica';
import { accendiSuoni, bu, clac, dinDon } from './suoni';

// ————————————————————————————————— stato —————————————————————————————————

const opzioniDiPartenza = (): Opzioni => ({
  video: { modo: 'peso', pesoByte: 0, qualita: 70, codec: 'h264', lato: 0, motore: 'auto', senzaAudio: false, audioKbps: 0, fps: 0, gifLarghezza: 480, gifFps: 15 },
  audio: { kbps: 0, normalizza: false, mono: false, bit: 16 },
  immagine: { qualita: 88, lato: 0, pesoMaxByte: 0 },
  pdf: { dpi: 200 },
});

const s = {
  stato: null as unknown as Stato,
  file: new Map<string, InfoFile>(),
  ordine: [] as string[],
  attiva: null as Categoria | null,
  fileAttivo: null as string | null,
  scelta: {} as Record<string, string>,
  opzioni: opzioniDiPartenza(),
  pesoPerFile: new Map<string, number>(),
  lavori: new Map<string, Lavoro>(),
  vista: 'scegli' as 'scegli' | 'lavoro' | 'fatto',
  piano: null as Piano | null,
  carico: { cpu: 0, encoder: 0, decoder: 0, grafica: 0 } as Carico,
  giro: [] as string[],
  impostazioniAperte: false,
};

const nomeCat = (c: string) => s.stato.categorie.find((x) => x.id === c)?.nome ?? c;
const formato = (id: string) => s.stato.formati.find((f) => f.id === id);
const fileDi = (c: Categoria) => s.ordine.map((i) => s.file.get(i)!).filter((f) => f && f.categoria === c);
const categoriePresenti = () => {
  const ordine = s.stato.categorie.map((c) => c.id as string);
  const cc = [...new Set(s.ordine.map((i) => s.file.get(i)?.categoria).filter(Boolean))] as Categoria[];
  return cc.sort((a, b) => ordine.indexOf(a) - ordine.indexOf(b));
};
const sceltaPer = (c: Categoria): Formato | undefined => {
  const disponibili = s.stato.formati.filter((f) => f.categoria === c && fileDi(c).some((x) => x.formati.includes(f.id)));
  const id = s.scelta[c] ?? s.stato.impostazioni.formati[c];
  return disponibili.find((f) => f.id === id && !f.manca) ?? disponibili.find((f) => !f.manca) ?? disponibili[0];
};
const eVideoVero = (f?: Formato) => !!f && f.categoria === 'video' && ['.mp4', '.mkv', '.webm', '.mov'].includes(f.estensione);

// ————————————————————————————————— scheletro —————————————————————————————————

const app = document.getElementById('app')!;
const testa = h('header.testa');
const coda = h('aside.coda');
const banco = h('section.banco');
const piede = h('footer.piede');
const corpo = h('main.corpo', null, coda, banco);
app.append(testa, corpo, piede);

// ————————————————————————————————— testa —————————————————————————————————

function cpuCorta(n: string) {
  return n.replace(/\(R\)|\(TM\)|CPU|Processor/gi, '').replace(/with Radeon.*$/i, '').replace(/@.*$/, '').replace(/^(AMD|Intel)\s+/i, '').replace(/Core\s+/i, '').replace(/\s+/g, ' ').trim();
}

function disegnaTesta() {
  const hw = s.stato.hardware;
  testa.replaceChildren(
    h('div.marchio', null,
      h('span.logo', { html: logoSvg }),
      h('span.nome', null, h('b', null, 'DaP'), h('span', null, 'CONVERTITORE')),
    ),
    h('div.trascina', { 'data-regione': 'trascina' }),
    h('div.chip-hw', null,
      hw?.acceleratore ? h('span.chip.gpu', { title: hw.gpu.map((g) => g.nome).join(' · ') }, h('i.led.verde'), hw.acceleratore) : h('span.chip', null, h('i.led'), 'Solo CPU'),
      hw ? h('span.chip', { title: hw.cpu }, h('i.led.ambra'), `${cpuCorta(hw.cpu)} · ${hw.thread} thread`) : h('span.chip', null, h('i.led.lampeggia'), 'Guardo la macchina…'),
    ),
    h('div.finestra', null,
      h('button.btn-fin', { title: 'Impostazioni', onclick: () => apriImpostazioni() , html: icone.ingranaggio }),
      h('button.btn-fin', { title: 'Riduci', onclick: () => chiedi('finestra', { azione: 'riduci' }), html: icone.riduci }),
      s.stato.modo === 'finestra' ? h('button.btn-fin', { title: 'Ingrandisci', onclick: () => chiedi('finestra', { azione: 'massimizza' }), html: icone.ingrandisci }) : null,
      h('button.btn-fin.chiudi', { title: 'Chiudi', onclick: () => chiedi('finestra', { azione: 'chiudi' }), html: icone.chiudi }),
    ),
  );
}

const logoSvg = `<svg viewBox="0 0 64 64"><defs>
<linearGradient id="lo" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fffbe0"/><stop offset=".45" stop-color="#ffd54a"/><stop offset="1" stop-color="#b87a14"/></linearGradient>
<linearGradient id="lm" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#ff7bf6"/><stop offset="1" stop-color="#8a2cff"/></linearGradient></defs>
<circle cx="32" cy="32" r="28" fill="#150e24" stroke="url(#lo)" stroke-width="4"/>
<path d="M20 26a13 13 0 0 1 23-6" fill="none" stroke="url(#lm)" stroke-width="4" stroke-linecap="round"/><path d="M45 13v8h-8" fill="none" stroke="url(#lm)" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"/>
<path d="M44 38a13 13 0 0 1-23 6" fill="none" stroke="url(#lo)" stroke-width="4" stroke-linecap="round"/><path d="M19 51v-8h8" fill="none" stroke="url(#lo)" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"/>
</svg>`;

// ————————————————————————————————— coda (sinistra) —————————————————————————————————

function metaDi(f: InfoFile): string {
  const d = f.dettagli;
  const p: string[] = [];
  if (d?.errore) p.push('non si legge');
  if (f.categoria === 'video' && d) {
    if (d.larghezza) p.push(risoluzione(d.larghezza, d.altezza));
    if (d.durata) p.push(durata(d.durata));
    if (d.codec) p.push(`${d.codec.toUpperCase()}${d.hdr ? ' HDR' : ''}`);
  } else if (f.categoria === 'audio' && d) {
    if (d.durata) p.push(durata(d.durata));
    if (d.audio) p.push(d.audio.toUpperCase());
  } else if (f.categoria === 'immagine' && d?.larghezza) p.push(`${d.larghezza}×${d.altezza}`);
  else if (f.categoria === 'pdf' && d?.pagine) p.push(`${d.pagine} ${d.pagine === 1 ? 'pagina' : 'pagine'}`);
  else if (f.categoria === 'cartella' && d?.file != null) p.push(`${d.file} file`);
  p.push(peso(f.eDirectory ? d?.peso ?? 0 : f.peso));
  return p.join(' · ');
}

function disegnaCoda() {
  const tot = s.ordine.reduce((a, i) => a + (s.file.get(i)?.peso ?? 0), 0);
  const lista = h('div.lista');
  for (const id of s.ordine) {
    const f = s.file.get(id)!;
    const lav = [...s.lavori.values()].reverse().find((l) => l.fileId === id);
    const scheda = h(`div.scheda${s.fileAttivo === id ? '.attiva' : ''}${f.categoria !== s.attiva ? '.spenta' : ''}`,
      { dataset: { id }, style: { '--tinta': coloreCategoria[f.categoria] }, onclick: () => { clac(); s.attiva = f.categoria; s.fileAttivo = id; s.piano = null; disegnaTutto(); } },
      h('div.figurina', null,
        f.anteprima ? h('img', { src: f.anteprima, alt: '' }) : h('span.icona-cat', { html: iconaCategoria[f.categoria] }),
        h('span.est', null, (f.estensione || 'cartella').replace('.', '').toUpperCase()),
      ),
      h('div.testo', null,
        h('div.nome', { title: f.percorso }, f.nome),
        h('div.meta', null, metaDi(f)),
        lav && lav.stato !== 'attesa' ? h(`div.mini-barra.${lav.stato}`, null, h('i', { style: { width: `${Math.round((lav.stato === 'fatto' ? 1 : Math.max(0, lav.frazione)) * 100)}%` } })) : null,
      ),
      s.vista === 'scegli' ? h('button.togli', { title: 'Togli dalla coda', html: icone.chiudi, onclick: (e: Event) => { e.stopPropagation(); togli(id); } }) : null,
    );
    lista.append(scheda);
  }
  coda.replaceChildren(...[
    h('div.titolo-sezione', null, h('span', null, 'SORGENTE'), h('span.conta', null, s.ordine.length ? `${s.ordine.length} · ${peso(tot)}` : '')),
    s.ordine.length ? lista : h('div.vuota', null, h('div.icona-grande', { html: icone.scarica }), h('p', null, 'Trascina qui i file da convertire'), h('p.piccolo', null, 'o tasto destro su un file → DaP Convertitore')),
    s.vista === 'scegli' ? h('button.aggiungi', { onclick: () => { clac(); void chiedi('scegli'); } }, h('span', { html: icone.aggiungi }), 'Aggiungi file') : null,
  ].filter((x): x is HTMLElement => !!x));
}

function togli(id: string) {
  clac();
  s.file.delete(id);
  s.ordine = s.ordine.filter((x) => x !== id);
  s.pesoPerFile.delete(id);
  void chiedi('togli', { id });
  if (s.fileAttivo === id) s.fileAttivo = null;
  sistemaAttiva();
  disegnaTutto();
}

function sistemaAttiva() {
  const cc = categoriePresenti();
  if (!s.attiva || !cc.includes(s.attiva)) s.attiva = cc[0] ?? null;
  if (!s.fileAttivo || s.file.get(s.fileAttivo)?.categoria !== s.attiva) s.fileAttivo = s.attiva ? fileDi(s.attiva)[0]?.id ?? null : null;
}

// ————————————————————————————————— banco: scegli —————————————————————————————————

let lancettaQualita: Lancetta | null = null;

function disegnaScegli() {
  sistemaAttiva();
  if (!s.attiva) {
    banco.replaceChildren(h('div.benvenuto', null,
      h('div.logo-grande', { html: logoSvg }),
      h('h1', null, 'Qualsiasi file, in qualsiasi formato.'),
      h('p', null, 'Tasto destro su un file → ', h('b', null, 'DaP Convertitore'), '. Oppure trascinali qui.'),
      h('p.piccolo', null, 'Il risultato finisce accanto all\'originale, col nome ', h('code', null, `(${s.stato.impostazioni.scritta || 'convertito'})`), '.'),
      h('div.righe-motori', null,
        rigaMotore('Video, audio e foto', 'FFmpeg 9 · ' + (s.stato.hardware?.acceleratore ?? 'CPU')),
        rigaMotore('HEIC, RAW, PDF, OCR', 'Windows'),
        rigaMotore('Documenti', s.stato.office.word ? 'Microsoft Office' : s.stato.office.libreOffice ? 'LibreOffice' : 'serve LibreOffice'),
        rigaMotore('Archivi 7Z, ZIP, RAR', 'tar di Windows'),
      ),
    ));
    return;
  }
  const cc = categoriePresenti();
  const att = s.attiva;
  const scelto = sceltaPer(att);
  const fmt = s.stato.formati.filter((f) => f.categoria === att && fileDi(att).some((x) => x.formati.includes(f.id)));

  const schede = cc.length > 1
    ? h('div.schede-cat', null, ...cc.map((c) => h(`button.scheda-cat${c === att ? '.su' : ''}`, { style: { '--tinta': coloreCategoria[c] }, onclick: () => { clac(); s.attiva = c; s.fileAttivo = null; s.piano = null; disegnaTutto(); } },
      h('i.led'), h('span', { html: iconaCategoria[c] }), nomeCat(c), h('small', null, String(fileDi(c).length)))))
    : null;

  const griglia = h('div.cassette', null, ...fmt.map((f) => h(`button.cassetta${scelto?.id === f.id ? '.scelta' : ''}${f.manca ? '.manca' : ''}`,
    { style: { '--tinta': coloreCategoria[att] }, title: f.manca ?? f.descrizione, disabled: !!f.manca && f.manca.includes('FFmpeg'),
      onclick: () => {
        if (f.manca) { mostraAvviso(f.manca); return; }
        clac(); s.scelta[att] = f.id; s.piano = null; disegnaTutto();
      } },
    h('span.striscia'),
    h('span.etichetta', null, f.etichetta),
    h('span.descr', null, f.descrizione),
    h('i.led'),
  )));

  banco.replaceChildren(
    schede ?? h('div'),
    h('div.titolo-sezione', null, h('span', null, 'IN COSA LO TRASFORMO?'), h('span.conta', null, nomeCat(att))),
    griglia,
    regolazioni(att, scelto),
  );
  if (eVideoVero(scelto) && s.opzioni.video.modo !== 'copia') aggiornaPiano();
}

function rigaMotore(cosa: string, chi: string) {
  return h('div.riga-motore', null, h('span', null, cosa), h('b', null, chi));
}

function mostraAvviso(testo: string) {
  bu();
  const a = h('div.avviso', null, h('span', { html: icone.attenzione }), testo,
    testo.includes('LibreOffice') ? h('button.link', { onclick: () => chiedi('link', { url: 'https://it.libreoffice.org/download/download/' }) }, 'Scarica LibreOffice') : null);
  banco.append(a);
  setTimeout(() => a.remove(), 5000);
}

// ——— le regolazioni, per categoria ———

function gruppo(titolo: string, ...figli: (Node | null)[]) {
  return h('div.gruppo', null, h('div.gruppo-titolo', null, titolo), ...figli.filter(Boolean) as Node[]);
}

function bottoni<T extends string | number>(valori: { v: T; testo: string; nota?: string; spento?: boolean }[], attuale: T, cambia: (v: T) => void) {
  return h('div.bottoni', null, ...valori.map((x) => h(`button.tasto${x.v === attuale ? '.su' : ''}`, {
    disabled: x.spento, title: x.nota,
    onclick: () => { clac(); cambia(x.v); disegnaScegli(); disegnaPiede(); },
  }, x.testo, x.nota ? h('small', null, x.nota) : null)));
}

function interruttore(testo: string, acceso: boolean, cambia: (v: boolean) => void, nota?: string) {
  return h(`button.interruttore${acceso ? '.su' : ''}`, { onclick: () => { clac(); cambia(!acceso); disegnaScegli(); } }, h('i.levetta'), h('span', null, testo, nota ? h('small', null, nota) : null));
}

function cursore(min: number, max: number, valore: number, cambia: (v: number) => void, etichette?: [string, string], passo = 1) {
  const input = h('input.cursore', { type: 'range', min, max, step: passo, value: valore }) as HTMLInputElement;
  input.style.setProperty('--p', `${((valore - min) / (max - min)) * 100}%`);
  input.addEventListener('input', () => {
    input.style.setProperty('--p', `${((+input.value - min) / (max - min)) * 100}%`);
    cambia(+input.value);
  });
  return h('div.cursore-box', null, input, etichette ? h('div.cursore-etichette', null, h('span', null, etichette[0]), h('span', null, etichette[1])) : null);
}

function regolazioni(c: Categoria, f?: Formato): Node {
  if (!f) return h('div');
  const o = s.opzioni;
  if (eVideoVero(f)) return regolazioniVideo(f);
  if (f.id === 'video.gif') {
    return h('div.regolazioni', null,
      gruppo('LARGHEZZA', bottoni([320, 480, 640, 800].map((v) => ({ v, testo: `${v} px` })), o.video.gifLarghezza, (v) => (o.video.gifLarghezza = v))),
      gruppo('FOTOGRAMMI AL SECONDO', bottoni([10, 15, 20, 25].map((v) => ({ v, testo: String(v) })), o.video.gifFps, (v) => (o.video.gifFps = v))),
      h('p.spiega', null, 'La tavolozza dei 256 colori si calcola sul video vero, fotogramma per fotogramma.'));
  }
  if (c === 'audio' || f.id === 'video.mp3' || f.id === 'video.wav') {
    const senzaPerdite = ['.flac', '.wav', '.aiff'].includes(f.estensione);
    const kbps = { '.mp3': [128, 192, 256, 320], '.m4a': [128, 192, 256, 320], '.opus': [64, 96, 128, 160, 192], '.ogg': [128, 192, 256, 320], '.wma': [128, 192, 256] }[f.estensione] ?? [];
    const partenza = { '.mp3': 256, '.m4a': 256, '.opus': 160, '.ogg': 192, '.wma': 192 }[f.estensione] ?? 0;
    return h('div.regolazioni', null,
      senzaPerdite
        ? gruppo('PROFONDITÀ', bottoni([{ v: 16, testo: '16 bit', nota: 'CD' }, { v: 24, testo: '24 bit', nota: 'studio' }], o.audio.bit, (v) => (o.audio.bit = v)))
        : gruppo('QUALITÀ', bottoni(kbps.map((v) => ({ v, testo: `${v}`, nota: 'kbit/s' })), o.audio.kbps || partenza, (v) => (o.audio.kbps = v))),
      gruppo('RITOCCHI',
        h('div.fila', null,
          interruttore('Volume uniforme', o.audio.normalizza, (v) => (o.audio.normalizza = v), '-14 LUFS, come Spotify'),
          interruttore('Mono', o.audio.mono, (v) => (o.audio.mono = v)))));
  }
  if (c === 'immagine' && !['img.pdf', 'img.txt', 'img.mp4', 'img.webm'].includes(f.id)) {
    const conQualita = ['.jpg', '.webp', '.avif', '.jxl', '.heic'].includes(f.estensione);
    return h('div.regolazioni', null,
      conQualita ? gruppo(`QUALITÀ · ${o.immagine.qualita}`, cursore(30, 100, o.immagine.qualita, (v) => { o.immagine.qualita = v; (document.querySelector('.regolazioni .gruppo-titolo') as HTMLElement).textContent = `QUALITÀ · ${v}`; }, ['più leggera', 'perfetta'])) : null,
      gruppo('LATO LUNGO', bottoni([0, 3840, 2560, 1920, 1280, 800].map((v) => ({ v, testo: v ? `${v}` : 'Originale', nota: v ? 'px' : undefined })), o.immagine.lato, (v) => (o.immagine.lato = v))),
      conQualita ? gruppo('PESO MASSIMO', bottoni([0, 300 * 1024, 1 * MB, 2 * MB, 5 * MB].map((v) => ({ v, testo: v ? peso(v, 0) : 'Libero' })), o.immagine.pesoMaxByte, (v) => (o.immagine.pesoMaxByte = v))) : null,
      h('p.spiega', null, 'La foto si gira da sola come la vede il telefono. La posizione GPS e i dati nascosti non passano; la data del file sì.'));
  }
  if (f.id === 'pdf.jpg' || f.id === 'pdf.png') {
    return h('div.regolazioni', null,
      gruppo('DEFINIZIONE', bottoni([{ v: 100, testo: '100', nota: 'schermo' }, { v: 150, testo: '150', nota: 'leggera' }, { v: 200, testo: '200', nota: 'buona' }, { v: 300, testo: '300', nota: 'stampa' }], o.pdf.dpi, (v) => (o.pdf.dpi = v))),
      h('p.spiega', null, 'Una immagine per pagina, in una cartella accanto al PDF. Se la pagina è una sola, esce un file solo.'));
  }
  const spiegazioni: Record<string, string> = {
    'pdf.txt': 'Il testo si legge dal PDF. Se è una scansione, lo legge l\'OCR di Windows, nella lingua del PC.',
    'img.txt': 'L\'OCR di Windows legge quello che c\'è scritto nella foto.',
    'pdf.unisci': 'Tutti i PDF scelti, uno dopo l\'altro, in un PDF solo. L\'ordine è quello della coda.',
    'img.pdf': 'Tutte le foto scelte in un PDF solo, una per pagina A4, girate come la foto.',
    'arch.estrai': 'In una cartella accanto, col nome dell\'archivio. Se dentro c\'è già una cartella sola, non la metto dentro un\'altra.',
  };
  const chi = c === 'documento' || c === 'foglio' || c === 'presentazione' || f.id === 'pdf.docx'
    ? (s.stato.office.word ? 'Ci pensa Microsoft Office, senza aprire finestre.' : 'Ci pensa LibreOffice, senza aprire finestre.')
    : c === 'archivio' || c === 'cartella' ? 'Ci pensa il tar di Windows (lo stesso motore di Esplora file).' : '';
  return h('div.regolazioni', null, h('p.spiega', null, spiegazioni[f.id] ?? (chi || 'Nessuna regolazione: si converte e basta.')));
}

// ——— il video: peso, qualità, codec, motore ———

const presetPeso = [
  { v: 10 * MB, testo: '10 MB', nota: 'Discord' },
  { v: 25 * MB, testo: '25 MB', nota: 'Email' },
  { v: 100 * MB, testo: '100 MB', nota: 'WhatsApp' },
  { v: 500 * MB, testo: '500 MB', nota: 'Telegram' },
  { v: 2 * GB, testo: '2 GB', nota: 'WeTransfer' },
  { v: 4 * GB - 1, testo: '4 GB', nota: 'Chiavetta FAT32' },
  // il DVD da «4,7 GB» conta a 1000: sono 4,37 GB come li conta Windows
  { v: 4.7e9, testo: '4,37 GB', nota: 'DVD' },
  { v: 25e9, testo: '23,3 GB', nota: 'Blu-ray' },
];

function pesoObiettivo(id: string | null) {
  if (!id) return 0;
  const f = s.file.get(id)!;
  return s.pesoPerFile.get(id) ?? Math.round(f.peso / 2);
}

function regolazioniVideo(f: Formato) {
  const o = s.opzioni.video;
  const fa = s.fileAttivo ? s.file.get(s.fileAttivo) : undefined;
  const p = s.piano;
  const quanti = s.attiva ? fileDi(s.attiva).length : 1;
  const modo = o.modo === 'auto' ? 'peso' : o.modo;

  const modi = h('div.modi', null,
    ...([
      { v: 'peso', testo: 'PESO FINALE', nota: 'deve starci' },
      { v: 'qualita', testo: 'QUALITÀ', nota: 'il peso viene da sé' },
      { v: 'copia', testo: 'VELOCE', nota: 'cambia solo la scatola', spento: p ? !p.puoCopiare : false },
    ] as const).map((x) => h(`button.modo${modo === x.v ? '.su' : ''}`, {
      disabled: 'spento' in x && x.spento, title: 'spento' in x && x.spento ? 'Questo video non si può copiare così com\'è in questo formato' : x.nota,
      onclick: () => { clac(); o.modo = x.v; s.piano = null; disegnaScegli(); disegnaPiede(); },
    }, h('i.led'), x.testo, h('small', null, x.nota))));

  const quadro = h('div.quadro');
  if (modo === 'copia') {
    quadro.append(h('div.copia', null, h('span.fulmine', { html: icone.fulmine }), h('div', null, h('b', null, 'Niente ricodifica.'), ' Il video e l\'audio passano tali e quali in un contenitore nuovo: ci mette pochi secondi e la qualità resta identica.')));
  } else {
    // display a sette segmenti + strumento a lancetta
    const valore = modo === 'peso' ? pesoObiettivo(fa?.id ?? null) : p?.pesoStimato ?? 0;
    const dsp = pesoDisplay(valore || 0);
    const display = h('div.display', null,
      h('div.display-etichetta', null, modo === 'peso' ? (quanti > 1 ? 'PESO FINALE · questo video' : 'PESO FINALE') : 'PESO STIMATO'),
      h('div.sette', null, h('span.spenti', null, '88888'), h('span.accesi', { id: 'dsp-numero' }, modo === 'qualita' ? `~${dsp.numero}` : dsp.numero)),
      h('div.display-unita', { id: 'dsp-unita' }, dsp.unita),
      h('div.display-sotto', { id: 'dsp-sotto' }, fa ? confrontoPeso(valore, fa.peso) : ''),
    );
    const vu = h('canvas.vu-qualita') as HTMLCanvasElement;
    const strumento = h('div.strumento', null, vu, h('div.giudizio', { id: 'giudizio' }, p?.giudizio ?? '…'));
    quadro.append(display, strumento);
    requestAnimationFrame(() => {
      lancettaQualita = new Lancetta(vu, { etichetta: 'QUALITÀ', rossoDa: 0, rossoA: 0.25,
        tacche: [{ v: 0.02, testo: 'BUTTARE' }, { v: 0.3, testo: 'BASSA' }, { v: 0.55, testo: 'BUONA' }, { v: 0.8, testo: 'OTTIMA' }, { v: 0.98, testo: 'ORIG.' }] });
      if (p) lancettaQualita.valore(p.punteggio);
    });
  }

  const comandi = h('div.comandi');
  if (modo === 'peso' && fa) {
    const min = p?.pesoMin ?? Math.max(1e6, fa.peso / 200);
    const max = p?.pesoMax ?? fa.peso;
    const valore = clamp(pesoObiettivo(fa.id), min, max);
    // scala logaritmica: fra 10 MB e 10 GB lo slider deve servire tutto
    const aPos = (b: number) => Math.log(b / min) / Math.log(max / min);
    const daPos = (x: number) => min * Math.pow(max / min, x);
    const fader = h('input.fader', { type: 'range', min: 0, max: 1000, value: Math.round(aPos(valore) * 1000) }) as HTMLInputElement;
    const segna = () => fader.style.setProperty('--p', `${(+fader.value / 10).toFixed(1)}%`);
    segna();
    fader.addEventListener('input', () => {
      segna();
      const b = Math.round(daPos(+fader.value / 1000));
      s.pesoPerFile.set(fa.id, b);
      const d = pesoDisplay(b);
      document.getElementById('dsp-numero')!.textContent = d.numero;
      document.getElementById('dsp-unita')!.textContent = d.unita;
      document.getElementById('dsp-sotto')!.textContent = confrontoPeso(b, fa.peso);
      document.querySelectorAll('.preset .tasto.su').forEach((e) => e.classList.remove('su'));
      aggiornaPiano(true);
    });
    fader.addEventListener('change', () => clac());
    const scala = h('div.fader-scala', null, h('span', null, peso(min, 0)), h('span', null, 'originale ' + peso(max, 1)));
    const chips = presetPeso.filter((x) => x.v >= min && x.v < max * 0.98);
    comandi.append(
      h('div.fader-box', null, fader, scala),
      h('div.bottoni.preset', null,
        ...[{ v: Math.round(fa.peso / 2), testo: 'Metà', nota: '½' }, { v: Math.round(fa.peso / 4), testo: 'Un quarto', nota: '¼' }, ...chips]
          .filter((x) => x.v >= min)
          .map((x) => h(`button.tasto.piccolo${Math.abs(valore - x.v) < x.v * 0.01 ? '.su' : ''}`, { onclick: () => { clac(); s.pesoPerFile.set(fa.id, x.v); s.piano = null; disegnaScegli(); } }, x.testo, h('small', null, x.nota)))),
    );
  } else if (modo === 'qualita') {
    comandi.append(cursore(0, 100, o.qualita, (v) => { o.qualita = v; aggiornaPiano(true); }, ['più leggero', 'perfetto']));
  }

  const ammessi = p?.codecAmmessi ?? (f.estensione === '.webm' ? ['av1', 'vp9'] : f.estensione === '.mov' ? ['h264', 'hevc', 'prores'] : ['h264', 'hevc', 'av1']);
  const codecAttuale = ammessi.includes(o.codec) ? o.codec : ammessi[0];
  const nomiCodec: Record<string, [string, string]> = { h264: ['H.264', 'va ovunque'], hevc: ['H.265', 'metà peso'], av1: ['AV1', 'il più efficiente'], vp9: ['VP9', 'web'], prores: ['ProRes', 'montaggio'] };
  const hw = s.stato.hardware;
  const haNvidia = !!hw?.encoder.some((e) => e.endsWith('_nvenc'));
  const haAmd = !!hw?.encoder.some((e) => e.endsWith('_amf'));
  const haIntel = !!hw?.encoder.some((e) => e.endsWith('_qsv'));
  const latoOrig = fa?.dettagli?.larghezza ? Math.min(fa.dettagli.larghezza, fa.dettagli.altezza ?? 0) : 2160;

  const dettagliTecnici = modo !== 'copia'
    ? gruppo('REGOLAZIONI',
        h('div.fila-regolazioni', null,
          h('div.sotto-gruppo', null, h('label', null, 'CODEC'), bottoni(ammessi.map((c) => ({ v: c, testo: nomiCodec[c][0], nota: nomiCodec[c][1] })), codecAttuale, (v) => { o.codec = v; s.piano = null; })),
          h('div.sotto-gruppo', null, h('label', null, 'RISOLUZIONE'), bottoni([0, 2160, 1440, 1080, 720, 480].filter((v) => v === 0 || v <= latoOrig).map((v) => ({ v, testo: v ? (v === 2160 ? '4K' : `${v}p`) : 'Auto' })), o.lato, (v) => { o.lato = v; s.piano = null; })),
          h('div.sotto-gruppo', null, h('label', null, 'MOTORE'), bottoni([
            { v: 'auto', testo: 'Auto' },
            ...(haNvidia ? [{ v: 'nvidia', testo: 'NVIDIA' }] : []),
            ...(haAmd ? [{ v: 'amd', testo: 'AMD' }] : []),
            ...(haIntel ? [{ v: 'intel', testo: 'Intel' }] : []),
            { v: 'cpu', testo: 'CPU', nota: 'più lenta' },
          ], o.motore, (v) => { o.motore = v; s.piano = null; })),
          h('div.sotto-gruppo', null, h('label', null, 'AUDIO'), bottoni([{ v: 'si', testo: 'Tienilo' }, { v: 'no', testo: 'Toglilo' }], o.senzaAudio ? 'no' : 'si', (v) => { o.senzaAudio = v === 'no'; s.piano = null; })),
        ))
    : null;

  const note = h('div.note', { id: 'note-piano' });
  riempiNote(note);

  return h('div.regolazioni.video', null, modi, h('div.pannello-peso', null, quadro, comandi), dettagliTecnici, note);
}

function confrontoPeso(b: number, orig: number) {
  if (!orig) return '';
  const r = (b / orig - 1) * 100;
  return `originale ${peso(orig)} · ${r <= 0 ? '−' : '+'}${numero(Math.abs(r))}%`;
}

function riempiNote(el: HTMLElement) {
  const p = s.piano;
  if (!p) { el.replaceChildren(); return; }
  const encoder = p.encoder.endsWith('_nvenc') ? `NVENC ${p.codec.toUpperCase()}` : p.encoder.endsWith('_amf') ? 'AMD AMF' : p.encoder.endsWith('_qsv') ? 'Intel Quick Sync' : p.encoder === 'copy' ? 'copia' : `CPU · ${p.encoder}`;
  const righe: (string | Node)[] = [
    h('span.nota-tec', null, h('i', { html: p.hardware ? icone.fulmine : icone.chip }), `${encoder} · ${p.larghezza}×${p.altezza}${p.modo === 'peso' ? ` · ${numero(p.bitrateVideo / 1e6, 1)} Mbit/s` : ''}${p.audioKbps ? ` · audio ${p.audioKbps} kbit/s` : ''}`),
    ...p.note.map((n) => h('span.nota', null, n)),
  ];
  el.replaceChildren(...righe);
}

let pianoInVolo = false;
let pianoDiNuovo = false;
async function aggiornaPiano(soloNumeri = false) {
  const att = s.attiva;
  if (!att || !s.fileAttivo) return;
  const f = sceltaPer(att);
  if (!eVideoVero(f)) return;
  if (pianoInVolo) { pianoDiNuovo = true; return; }
  pianoInVolo = true;
  try {
    const o = structuredClone(s.opzioni);
    o.video.pesoByte = pesoObiettivo(s.fileAttivo);
    const p = await chiedi<Piano>('piano', { id: s.fileAttivo, formato: f!.id, opzioni: o });
    const primo = !s.piano;
    s.piano = p;
    if (primo && !soloNumeri) { disegnaScegli(); return; }
    lancettaQualita?.valore(p.punteggio);
    const g = document.getElementById('giudizio');
    if (g) { g.textContent = p.giudizio; g.dataset.livello = p.punteggio >= 0.72 ? 'alto' : p.punteggio >= 0.38 ? 'medio' : 'basso'; }
    if (s.opzioni.video.modo === 'qualita') {
      const d = pesoDisplay(p.pesoStimato);
      const n = document.getElementById('dsp-numero');
      if (n) n.textContent = `~${d.numero}`;
      const u = document.getElementById('dsp-unita');
      if (u) u.textContent = d.unita;
      const fa = s.file.get(s.fileAttivo!);
      const sotto = document.getElementById('dsp-sotto');
      if (sotto && fa) sotto.textContent = confrontoPeso(p.pesoStimato, fa.peso);
    }
    const note = document.getElementById('note-piano');
    if (note) riempiNote(note);
  } catch (e) {
    console.warn(e);
  } finally {
    pianoInVolo = false;
    if (pianoDiNuovo) { pianoDiNuovo = false; void aggiornaPiano(true); }
  }
}

// ————————————————————————————————— piede —————————————————————————————————

function nomeUscita(f: InfoFile, fmt: Formato) {
  const radice = f.eDirectory ? f.nome : f.nome.replace(/(\.tar)?\.[^.]+$/i, '');
  if (fmt.id === 'arch.estrai') return `${radice}\\`;
  const scritta = s.stato.impostazioni.scritta?.trim();
  return `${radice}${scritta ? ` (${scritta})` : ''}${fmt.estensione || '\\'}`;
}

function disegnaPiede() {
  if (s.vista === 'scegli') {
    const elementi = elementiDaConvertire();
    const n = elementi.reduce((a, e) => a + (formato(e.formato)?.unisce ? 1 : e.ids.length), 0);
    let dest = '';
    if (n === 1) {
      const e = elementi[0];
      dest = nomeUscita(s.file.get(e.ids[0])!, formato(e.formato)!);
    } else if (n > 1) dest = `${n} conversioni, ognuna accanto al suo originale`;
    piede.replaceChildren(
      h('div.destinazione', null,
        n ? h('span.freccia', { html: icone.freccia }) : null,
        h('span.dest-nome', { title: dest }, n ? dest : 'Aggiungi qualcosa da convertire'),
        n === 1 ? h('span.dest-dove', null, 'nella stessa cartella') : null),
      h('button.converti', { disabled: !n, onclick: () => void converti() }, h('i.led'), h('span', null, 'CONVERTI'), n > 1 ? h('small', null, String(n)) : null),
    );
  } else if (s.vista === 'lavoro') {
    piede.replaceChildren(
      h('div.destinazione', null, h('span.dest-nome#piede-stato', null, statoGiro())),
      h('button.annulla', { onclick: () => { clac(true); void chiedi('annulla', {}); } }, h('span', { html: icone.ferma }), 'ANNULLA'),
    );
  } else {
    piede.replaceChildren(
      h('div.destinazione', null, h('span.dest-nome', null, 'Fatto. I file sono accanto agli originali.')),
      h('button.secondario', { onclick: () => { clac(); nuovoGiro(); } }, h('span', { html: icone.indietro }), 'CONVERTI ALTRO'),
      h('button.secondario', { onclick: () => chiedi('finestra', { azione: 'chiudi' }) }, 'CHIUDI'),
    );
  }
}

function statoGiro() {
  const l = s.giro.map((i) => s.lavori.get(i)!).filter(Boolean);
  const fatti = l.filter((x) => x.stato === 'fatto').length;
  return `${fatti} di ${l.length} fatti`;
}

function elementiDaConvertire() {
  const el: { ids: string[]; formato: string; opzioni: Opzioni }[] = [];
  for (const c of categoriePresenti()) {
    const f = sceltaPer(c);
    if (!f || f.manca) continue;
    const ids = fileDi(c).filter((x) => x.formati.includes(f.id)).map((x) => x.id);
    if (!ids.length) continue;
    if (f.unisce) el.push({ ids, formato: f.id, opzioni: structuredClone(s.opzioni) });
    else for (const id of ids) {
      const o = structuredClone(s.opzioni);
      if (o.video.modo === 'auto') o.video.modo = 'peso';
      o.video.pesoByte = pesoObiettivo(id);
      el.push({ ids: [id], formato: f.id, opzioni: o });
    }
  }
  return el;
}

async function converti() {
  const elementi = elementiDaConvertire();
  if (!elementi.length) return;
  clac(true);
  for (const c of categoriePresenti()) { const f = sceltaPer(c); if (f) s.stato.impostazioni.formati[c] = f.id; }
  void chiedi('impostazioni', { formati: s.stato.impostazioni.formati, scelte: { video: { ...s.opzioni.video, pesoByte: 0 }, audio: s.opzioni.audio, immagine: s.opzioni.immagine, pdf: s.opzioni.pdf } });
  const ids = await chiedi<string[]>('converti', { elementi });
  s.giro = ids;
  s.vista = 'lavoro';
  disegnaTutto();
}

function nuovoGiro() {
  // si tolgono i file già convertiti, restano quelli andati storti
  const ok = new Set([...s.lavori.values()].filter((l) => s.giro.includes(l.id) && l.stato === 'fatto').map((l) => l.fileId));
  for (const id of ok) if (id) { s.file.delete(id); s.ordine = s.ordine.filter((x) => x !== id); void chiedi('togli', { id }); }
  s.giro = [];
  s.vista = 'scegli';
  s.piano = null;
  void chiedi('pulisci');
  disegnaTutto();
}

// ————————————————————————————————— banco: lavoro —————————————————————————————————

let bobine: Bobine | null = null;
const strumenti: { cpu?: Lancetta; enc?: Lancetta; dec?: Lancetta } = {};

function disegnaLavoro() {
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

function dsp(etichetta: string, id: string, spento: string, unita: string) {
  return h('div.dsp', null,
    h('div.dsp-etichetta', null, etichetta),
    h('div.sette.piccolo', null, h('span.spenti', null, spento.replace(/[0-9-]/g, '8').replace(/[^8:.]/g, '8')), h(`span.accesi#dsp-${id}`, null, spento)),
    unita ? h('span.dsp-unita', null, unita) : null);
}

function aggiornaLavoro() {
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
  disegnaCoda();
  if (giro.every((l) => l.stato === 'fatto' || l.stato === 'errore' || l.stato === 'annullato')) {
    bobine?.imposta(1, 0, false, true);
    setTimeout(() => { if (s.vista === 'lavoro') { s.vista = 'fatto'; disegnaTutto(); } }, 700);
    if (giro.some((l) => l.stato === 'fatto')) dinDon(); else bu();
  }
}

// ————————————————————————————————— banco: fatto —————————————————————————————————

function disegnaFatto() {
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

function schedaRisultato(l: Lavoro) {
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
  const confrontabile = f && (f.categoria === 'video' || f.categoria === 'immagine') && l.sorgenti.length === 1 && !['video.mp3', 'video.wav', 'video.srt', 'img.pdf', 'img.txt'].includes(l.formato);
  return h('div.risultato.fatto', null,
    h('div.ris-figura', null, f?.anteprima ? h('img', { src: f.anteprima }) : h('span', { html: iconaCategoria[f?.categoria ?? 'altro'] }), h('i.ok', { html: icone.ok })),
    h('div.ris-testo', null,
      h('div.ris-nome', { title: l.uscita ?? '' }, nomeUscitaFile),
      h('div.ris-pesi', null,
        h('span', null, peso(l.pesoPrima)), h('span.ris-freccia', { html: icone.freccia }), h('b', null, peso(l.pesoDopo)),
        l.pesoDopo != null && l.pesoPrima > 0 ? h(`span.ris-delta${rapporto <= 1 ? '.giu' : '.su'}`, null, `${rapporto <= 1 ? '−' : '+'}${numero(Math.abs(1 - rapporto) * 100)}%`) : null),
      h('div.ris-barre', null, h('i.prima'), h('i.dopo', { style: { width: `${Math.min(100, rapporto * 100)}%` } })),
    ),
    h('div.ris-azioni', null,
      h('button.tasto-icona', { title: 'Apri', onclick: () => chiedi('apri', { percorso: l.uscita }), html: icone.apri }),
      h('button.tasto-icona', { title: 'Mostra nella cartella', onclick: () => chiedi('mostra', { percorso: l.uscita }), html: icone.cartella }),
      confrontabile ? h('button.tasto-icona', { title: 'Confronta prima e dopo', onclick: () => void confronta(l), html: icone.confronta }) : null,
    ),
  );
}

// ——— confronto prima / dopo ———

async function confronta(l: Lavoro, posizione = 0.35) {
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

// ————————————————————————————————— impostazioni —————————————————————————————————

function apriImpostazioni() {
  clac();
  document.querySelector('.cassetto')?.remove();
  const imp = s.stato.impostazioni;
  const cassetto = h('div.velo', { onclick: (e: Event) => { if (e.target === cassetto) cassetto.remove(); } });
  const salva = (m: Partial<typeof imp>) => { Object.assign(imp, m); void chiedi('impostazioni', m); };
  const scritta = h('input.campo', { value: imp.scritta, maxlength: 40, placeholder: 'convertito' }) as HTMLInputElement;
  scritta.addEventListener('change', () => { salva({ scritta: scritta.value.trim() }); disegnaPiede(); });
  const riga = (titolo: string, spiega: string, controllo: Node) => h('div.imp-riga', null, h('div', null, h('b', null, titolo), h('p', null, spiega)), controllo);
  const leva = (acceso: boolean, cambia: (v: boolean) => void) => {
    const b = h(`button.interruttore.solo${acceso ? '.su' : ''}`, null, h('i.levetta'));
    b.addEventListener('click', () => { clac(); acceso = !acceso; b.classList.toggle('su', acceso); cambia(acceso); });
    return b;
  };
  cassetto.append(h('div.cassetto', null,
    h('div.confronto-testa', null, h('b', null, 'IMPOSTAZIONI'), h('span'), h('button.btn-fin', { html: icone.chiudi, onclick: () => cassetto.remove() })),
    riga('La scritta nel nome', 'Foto.jpg diventa «Foto (convertito).webp». Puoi cambiarla.', scritta),
    riga('Nel tasto destro', 'La voce DaP Convertitore in Esplora file, col sottomenu delle conversioni al volo. Su Windows 11 sta in «Mostra altre opzioni» (o Maiusc + tasto destro).', leva(imp.menu, (v) => { salva({ menu: v }); void chiedi('menu', { attivo: v }); })),
    riga('Spingi al massimo', 'FFmpeg a priorità normale: un po\' più veloce, ma mentre converte il PC si sente.', leva(imp.alMassimo, (v) => salva({ alMassimo: v }))),
    riga('Suoni', 'Il clac dei tasti e il din-don alla fine.', leva(imp.suoni, (v) => { salva({ suoni: v }); accendiSuoni(v); })),
    riga('Apri la cartella alla fine', 'Quando ha finito, apre Esplora file col file convertito già selezionato.', leva(imp.apriCartella, (v) => salva({ apriCartella: v }))),
    h('div.imp-piede', null,
      h('span', null, `DaP Convertitore ${s.stato.versione} · DaProd`),
      h('button.link', { onclick: () => chiedi('registro') }, 'Apri il registro'),
      h('button.link#cerca-agg', { onclick: async (e: Event) => {
        const b = e.currentTarget as HTMLButtonElement;
        b.textContent = 'Cerco…';
        const r = await chiedi<{ versione: string | null }>('aggiorna', { controlla: true }).catch(() => ({ versione: null }));
        b.textContent = r.versione ? `Installa la ${r.versione}` : 'Sei all\'ultima versione';
        if (r.versione) b.onclick = () => chiedi('aggiorna', { installa: true });
      } }, 'Cerca aggiornamenti')),
  ));
  document.body.append(cassetto);
}

// ————————————————————————————————— modo rapido —————————————————————————————————
// Dal sottomenu del tasto destro: niente scelte, una finestrella in basso a destra con le bobine.

let bobineRapide: Bobine | null = null;
let chiusuraRapida: number | undefined;

function disegnaRapido() {
  const giro = s.giro.map((i) => s.lavori.get(i)!).filter(Boolean);
  const l = giro.find((x) => x.stato === 'corre') ?? giro.find((x) => x.stato === 'attesa') ?? giro[giro.length - 1];
  if (!document.querySelector('.rapido-corpo')) {
    const canvas = h('canvas.bobine-mini') as HTMLCanvasElement;
    app.replaceChildren(testa, h('div.rapido-corpo', null, canvas, h('div.rapido-testo#rapido-testo')));
    requestAnimationFrame(() => { bobineRapide = new Bobine(canvas, '#ff3df2'); disegnaRapido(); });
    app.addEventListener('pointerenter', () => { if (chiusuraRapida) { clearTimeout(chiusuraRapida); chiusuraRapida = undefined; } });
    return;
  }
  const t = document.getElementById('rapido-testo')!;
  if (!l) { t.replaceChildren(h('div.r-nome', null, 'Preparo…')); return; }
  const f = l.fileId ? s.file.get(l.fileId) : undefined;
  const finiti = giro.every((x) => x.stato === 'fatto' || x.stato === 'errore' || x.stato === 'annullato');
  const nome = giro.length > 1 ? `${giro.filter((x) => x.stato === 'fatto').length} di ${giro.length} · ${f?.nome ?? ''}` : f?.nome ?? l.sorgenti[0].split('\\').pop() ?? '';
  if (!finiti) {
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
        ? h('div.r-fase.ok', null, h('span', { html: icone.ok }), ok.length > 1 ? `${ok.length} file fatti` : 'Fatto', ` · ${peso(ok.reduce((a, x) => a + x.pesoPrima, 0))} → ${peso(ok.reduce((a, x) => a + (x.pesoDopo ?? 0), 0))}`)
        : h('div.r-fase.ko', null, h('span', { html: icone.attenzione }), giro[0].errore ?? 'Annullato'),
      h('div.r-azioni', null,
        ultimo ? h('button.tasto.piccolo', { onclick: () => chiedi('apri', { percorso: ultimo.uscita }) }, 'Apri') : null,
        ultimo ? h('button.tasto.piccolo', { onclick: () => chiedi('mostra', { percorso: ultimo.uscita }) }, 'Mostra nella cartella') : null,
        h('button.tasto.piccolo', { onclick: () => chiedi('finestra', { azione: 'espandi' }) }, 'Altro…')),
    );
    if (!chiusuraRapida && ok.length === giro.length) chiusuraRapida = window.setTimeout(() => chiedi('finestra', { azione: 'chiudi' }), 9000);
  }
}

// ————————————————————————————————— tutto insieme —————————————————————————————————

function disegnaTutto() {
  if (s.stato.modo === 'rapido') { disegnaTesta(); disegnaRapido(); return; }
  app.classList.toggle('lavora', s.vista !== 'scegli');
  disegnaTesta();
  disegnaCoda();
  if (s.vista === 'scegli') disegnaScegli();
  else if (s.vista === 'lavoro') disegnaLavoro();
  else disegnaFatto();
  disegnaPiede();
}

function opzioniSalvate() {
  const sc = s.stato.impostazioni.scelte as Partial<Opzioni> | null | undefined;
  if (!sc) return;
  for (const k of ['video', 'audio', 'immagine', 'pdf'] as const) if (sc[k]) Object.assign(s.opzioni[k], sc[k]);
  if (s.opzioni.video.modo === 'auto') s.opzioni.video.modo = 'peso';
}

async function avvia() {
  await avviaFinto();
  ascolta('file', (f: InfoFile) => {
    if (!s.file.has(f.id)) s.ordine.push(f.id);
    s.file.set(f.id, f);
    if (s.vista !== 'scegli' && s.stato.modo === 'finestra') return;
    if (!s.attiva) s.attiva = f.categoria;
    disegnaTutto();
  });
  ascolta('dettagli', (d: { id: string; dettagli: any }) => {
    const f = s.file.get(d.id);
    if (!f) return;
    f.dettagli = d.dettagli;
    if (s.stato.modo === 'finestra') { disegnaCoda(); if (d.id === s.fileAttivo) { s.piano = null; if (s.vista === 'scegli') disegnaScegli(); } }
  });
  ascolta('anteprima', (d: { id: string; url: string }) => {
    const f = s.file.get(d.id);
    if (!f) return;
    f.anteprima = d.url;
    if (s.stato.modo === 'finestra') disegnaCoda();
  });
  ascolta('lavoro', (l: Lavoro) => {
    s.lavori.set(l.id, l);
    if (s.stato.modo === 'rapido') {
      if (!s.giro.includes(l.id)) s.giro.push(l.id);
      disegnaRapido();
      return;
    }
    if (!s.giro.includes(l.id) && s.vista !== 'scegli') s.giro.push(l.id);
    if (s.vista === 'lavoro') aggiornaLavoro();
  });
  ascolta('hardware', (hw) => {
    if (!hw) return;
    s.stato.hardware = hw;
    disegnaTesta();
    if (s.vista === 'scegli' && s.stato.modo === 'finestra') { s.piano = null; disegnaScegli(); }
  });
  ascolta('carico', (c: Carico) => {
    s.carico = c;
    strumenti.cpu?.valore(c.cpu / 100);
    strumenti.enc?.valore(c.encoder / 100);
    strumenti.dec?.valore(c.decoder / 100);
  });
  ascolta('modo', (m: { modo: 'finestra' | 'rapido' }) => {
    s.stato.modo = m.modo;
    app.classList.toggle('rapido', m.modo === 'rapido');
    if (m.modo === 'finestra') {
      app.replaceChildren(testa, corpo, piede);
      s.vista = s.giro.length && [...s.lavori.values()].some((l) => s.giro.includes(l.id) && (l.stato === 'corre' || l.stato === 'attesa')) ? 'lavoro' : s.giro.length ? 'fatto' : 'scegli';
    }
    disegnaTutto();
  });

  s.stato = await chiedi<Stato>('stato');
  accendiSuoni(s.stato.impostazioni.suoni);
  opzioniSalvate();
  for (const f of s.stato.file) { s.file.set(f.id, f); s.ordine.push(f.id); }
  for (const l of s.stato.lavori) { s.lavori.set(l.id, l); s.giro.push(l.id); }
  app.classList.toggle('rapido', s.stato.modo === 'rapido');
  if (s.stato.modo === 'finestra' && s.giro.length) s.vista = 'lavoro';
  document.body.classList.toggle('nel-browser', !dentroApp);
  disegnaTutto();
  // trascina i file dentro: WebView2 passa i percorsi veri a C#
  document.addEventListener('dragover', (e) => { e.preventDefault(); app.classList.add('sopra'); });
  document.addEventListener('dragleave', (e) => { if (!e.relatedTarget) app.classList.remove('sopra'); });
  document.addEventListener('drop', (e) => {
    e.preventDefault();
    app.classList.remove('sopra');
    const w = (window as any).chrome?.webview;
    if (w?.postMessageWithAdditionalObjects && e.dataTransfer?.files.length) w.postMessageWithAdditionalObjects({ cmd: 'lasciati' }, e.dataTransfer.files);
  });
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') document.querySelector('.velo')?.remove();
    if (e.key === 'Enter' && s.vista === 'scegli' && !document.querySelector('.velo') && !(e.target instanceof HTMLInputElement)) void converti();
  });
  void chiedi('pronto');
}

void avvia();
