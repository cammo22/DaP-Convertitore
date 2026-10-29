// Il lettore di musica: una scheda pulita, come un lettore vero. A sinistra la copertina (o una mattonella con
// l'estensione se non c'è), a destra titolo e artista, la forma d'onda che è anche la barra di avanzamento, i comandi,
// il volume, e sotto lo spettro che balla. La rotella cambia il volume, ovunque sulla pagina; la scaletta si apre a destra.
import { chiedi } from '../ponte';
import { h, clamp, durata as tempo } from '../util';
import { scaletta } from './scaletta';
import { attesa, ic, memoria, type Contesto, type Tasto, type Vista } from './comune';
import { nativo, type InfoMedia } from './media';

export async function monta(c: Contesto): Promise<Vista> {
  const f = c.scheda;
  c.palco.append(attesa('Metto il disco…'));
  const info = await chiedi<InfoMedia>('media');
  if (!c.viva()) return { tasti: [], smonta() {} };
  const tag = info.tag;
  const t = (k: string) => tag[k] ?? tag[k.toUpperCase()] ?? Object.entries(tag).find(([x]) => x.toLowerCase() === k)?.[1];
  const titolo = t('title') ?? f.nome.replace(/\.[^.]+$/, '');
  const artista = t('artist') ?? t('album_artist') ?? t('composer') ?? '';
  const album = t('album') ?? '';
  const anno = (t('date') ?? t('year') ?? '').slice(0, 4);
  const a0 = info.audio[0];
  const estensione = (f.estensione || '').replace('.', '').toUpperCase();

  const audio = h<HTMLAudioElement>('audio', { preload: 'auto' });
  audio.crossOrigin = 'anonymous';
  audio.volume = memoria.leggi<number>('volume') ?? 1;
  // il muto sta dopo l'analizzatore: lo spettro balla anche a volume zero
  let zitto = memoria.leggi<boolean>('muto') ?? false;

  // ——— la copertina: se non c'è, una mattonella col formato ———
  const copertina = info.copertina
    ? h('div.a-cover', { style: { backgroundImage: `url(${info.copertina})` } })
    : h('div.a-cover.vuota', null, h('b', null, estensione || 'AUDIO'), h('small', null, a0 ? a0.codec.toUpperCase().replace('PCM_', 'PCM ') : ''));

  // ——— la parte di destra ———
  const onda = h<HTMLCanvasElement>('canvas.a-onda');
  const bollaOnda = h('span.a-bolla');
  const ora = h('span', null, '0:00');
  const bPlay = h('button.a-play', { title: 'Play / pausa (Spazio)', html: ic.play, onclick: () => giraPlay() });
  let ripeti = memoria.leggi<boolean>('ripeti') ?? false;
  /** Di partenza suona solo il brano scelto; «Di seguito» fa suonare anche gli altri della cartella. */
  let seguito = memoria.leggi<boolean>('seguito') ?? false;
  const bSeguito = h(`button.a-seguito.solo-fratelli${seguito ? '.su' : ''}`, { title: 'Suona anche gli altri brani della cartella, uno dopo l\'altro (T)', onclick: () => cambiaSeguito() }, h('i.led'), 'Di seguito');
  const bRipeti = h(`button.a-b${ripeti ? '.su' : ''}`, { title: 'Ripeti il brano (R)', html: ic.ripeti, onclick: () => cambiaRipeti() });
  const vol = h<HTMLInputElement>('input.v-vol', { type: 'range', min: 0, max: 1, step: 0.01, value: audio.volume });
  vol.style.setProperty('--p', `${audio.volume * 100}%`);
  const bVol = h('button.a-b', { title: 'Muto (M)', html: zitto ? ic.muto : ic.volume, onclick: () => muto() });
  const spettro = h<HTMLCanvasElement>('canvas.a-spettro');
  const tecnica = [
    estensione,
    a0 && a0.codec !== f.estensione.replace('.', '') ? a0.codec.toUpperCase().replace('PCM_', 'PCM ') : null,
    a0?.campionamento ? `${(a0.campionamento / 1000).toLocaleString('it-IT')} kHz` : null,
    a0?.canali ? (a0.canali === 1 ? 'mono' : a0.canali === 2 ? 'stereo' : `${a0.canali} canali`) : null,
    a0?.bitrate || info.bitrate ? `${Math.round((a0?.bitrate || info.bitrate) / 1000)} kbit/s` : null,
  ].filter(Boolean) as string[];
  const sc = scaletta(c, { extra: () => riga() });
  const stanza = h('div.a-stanza', null,
    info.copertina ? h('div.a-fondo', { style: { backgroundImage: `url(${info.copertina})` } }) : h('div.a-fondo.vuoto'),
    h('div.a-scheda', null,
      h('div.a-sinistra', null,
        copertina,
        h('div.a-tecnica', null, ...tecnica.map((x) => h('span', null, x)))),
      h('div.a-destra', null,
        h('div.a-titoli', null,
          h('h1', { title: titolo }, titolo),
          artista ? h('div.a-artista', null, artista) : null,
          album || anno ? h('div.a-album', null, [album, anno].filter(Boolean).join(' · ')) : null),
        h('div.a-onda-box', null, onda, bollaOnda),
        h('div.a-tempi', null, ora, h('span', null, tempo(info.durata))),
        h('div.a-comandi', null,
          h('button.a-b.solo-fratelli', { title: 'Brano prima (P)', html: ic.prima, onclick: () => prima() }),
          bPlay,
          h('button.a-b.solo-fratelli', { title: 'Brano dopo (N)', html: ic.dopo, onclick: () => c.vai(1) }),
          h('span.a-spazio'),
          bSeguito,
          bRipeti,
          h('button.a-b.solo-fratelli', { title: 'Scaletta (L)', html: ic.elenco, onclick: () => sc.gira() }),
          h('div.a-volume', null, bVol, vol)),
        spettro)),
    sc.el, sc.linguetta);
  c.palco.replaceChildren(stanza);

  // la rotella cambia il volume, ovunque sulla pagina (sulla scaletta scorre la lista)
  stanza.addEventListener('wheel', (e) => {
    if (sc.el.contains(e.target as Node) || e.deltaY === 0) return;
    e.preventDefault();
    volume(e.deltaY < 0 ? 0.05 : -0.05);
  }, { passive: false });

  // il colore della copertina tinge la stanza
  if (info.copertina) tintaDa(info.copertina).then((col) => { if (col) stanza.style.setProperty('--cop', col); });

  // ——— la sorgente: com'è, o FLAC in cache se WebView2 non lo suona ———
  async function sorgente() {
    if (nativo(info)) return f.url;
    c.stato('Preparo l\'audio con FFmpeg…');
    try { return await chiedi<string>('audioCache'); } finally { c.stato(null); }
  }
  audio.src = await sorgente();
  if (!c.viva()) return { tasti: [], smonta() {} };
  audio.addEventListener('error', async () => {
    if (audio.src.includes('/f/') && !audio.dataset.cache) {
      audio.dataset.cache = '1';
      c.stato('Preparo l\'audio con FFmpeg…');
      try { audio.src = await chiedi<string>('audioCache'); void audio.play(); } catch (e) { c.stato((e as Error).message); return; }
      c.stato(null);
    }
  });

  // ——— l'analizzatore: WebAudio legge quello che suona ———
  let actx: AudioContext | null = null;
  let analisi: AnalyserNode | null = null;
  let guadagno: GainNode | null = null;
  function collegaAnalisi() {
    if (actx) { void actx.resume(); return; }
    try {
      actx = new AudioContext();
      const src = actx.createMediaElementSource(audio);
      analisi = actx.createAnalyser();
      analisi.fftSize = 4096;
      analisi.smoothingTimeConstant = 0.78;
      guadagno = actx.createGain();
      guadagno.gain.value = zitto ? 0 : 1;
      src.connect(analisi).connect(guadagno).connect(actx.destination);
    } catch { audio.muted = zitto; /* senza spettro si suona lo stesso */ }
  }

  // ——— la forma d'onda: arriva da FFmpeg, intanto c'è una riga ———
  let picchi: number[] = [];
  void chiedi<number[]>('onda', { fette: 900 }).then((p) => { picchi = p; disegnaOnda(); }).catch(() => {});
  function disegnaOnda() {
    const dpr = devicePixelRatio;
    const w = onda.clientWidth, hh = onda.clientHeight;
    if (!w) return;
    if (onda.width !== Math.round(w * dpr)) { onda.width = Math.round(w * dpr); onda.height = Math.round(hh * dpr); }
    const g = onda.getContext('2d')!;
    g.setTransform(dpr, 0, 0, dpr, 0, 0);
    g.clearRect(0, 0, w, hh);
    const p = audio.duration ? audio.currentTime / audio.duration : 0;
    const barre = Math.floor(w / 3);
    const grad = g.createLinearGradient(0, 0, w, 0);
    grad.addColorStop(0, '#5dffb4');
    grad.addColorStop(0.55, '#ffd54a');
    grad.addColorStop(1, '#ff3df2');
    for (let i = 0; i < barre; i++) {
      const v = picchi.length ? picchi[Math.floor((i / barre) * picchi.length)] : 0.06;
      const alto = Math.max(2, v * (hh - 6));
      g.fillStyle = i / barre < p ? grad : 'rgba(255,255,255,0.16)';
      g.fillRect(i * 3, (hh - alto) / 2, 2, alto);
    }
  }
  let tieni = false;
  const daX = (x: number) => { const r = onda.getBoundingClientRect(); return clamp((x - r.left) / r.width, 0, 1) * (audio.duration || info.durata); };
  onda.addEventListener('pointerdown', (e) => { tieni = true; onda.setPointerCapture(e.pointerId); audio.currentTime = daX(e.clientX); });
  onda.addEventListener('pointermove', (e) => {
    const r = onda.getBoundingClientRect();
    bollaOnda.textContent = tempo(daX(e.clientX));
    bollaOnda.style.left = `${clamp(e.clientX - r.left, 20, r.width - 20)}px`;
    if (tieni) audio.currentTime = daX(e.clientX);
  });
  onda.addEventListener('pointerup', () => (tieni = false));

  // ——— lo spettro, 60 volte al secondo: barre che salgono dal basso, con la cima che scende piano ———
  let giro = 0;
  const cime: number[] = [];
  function anima() {
    giro = requestAnimationFrame(anima);
    ora.textContent = tempo(audio.currentTime);
    disegnaOnda();
    const dpr = devicePixelRatio;
    const w = spettro.clientWidth, hh = spettro.clientHeight;
    if (!w) return;
    if (spettro.width !== Math.round(w * dpr)) { spettro.width = Math.round(w * dpr); spettro.height = Math.round(hh * dpr); }
    const g = spettro.getContext('2d')!;
    g.setTransform(dpr, 0, 0, dpr, 0, 0);
    g.clearRect(0, 0, w, hh);
    const n = Math.max(24, Math.min(72, Math.floor(w / 9)));
    const dati = new Uint8Array(analisi?.frequencyBinCount ?? 0);
    analisi?.getByteFrequencyData(dati);
    const largo = w / n;
    const base = hh - 2;
    const gr = g.createLinearGradient(0, base, 0, 0);
    gr.addColorStop(0, '#5dffb4');
    gr.addColorStop(0.6, '#ffd54a');
    gr.addColorStop(1, '#ff3df2');
    for (let i = 0; i < n; i++) {
      // bande in scala logaritmica, da 40 Hz a 16 kHz
      let v = 0;
      if (dati.length && actx) {
        const f0 = 40 * Math.pow(16000 / 40, i / n), f1 = 40 * Math.pow(16000 / 40, (i + 1) / n);
        const b0 = Math.floor(f0 / (actx.sampleRate / 2) * dati.length), b1 = Math.max(b0 + 1, Math.floor(f1 / (actx.sampleRate / 2) * dati.length));
        for (let b = b0; b < b1; b++) v = Math.max(v, dati[b]);
        v = Math.pow(v / 255, 1.6);
      }
      const alto = Math.max(2, v * (hh - 8));
      cime[i] = Math.max((cime[i] ?? 0) - 0.7, alto);
      const x = i * largo + 1;
      g.fillStyle = gr;
      g.fillRect(x, base - alto, largo - 3, alto);
      g.fillStyle = 'rgba(255,255,255,0.85)';
      g.fillRect(x, base - cime[i] - 2, largo - 3, 2);
    }
  }
  anima();

  // ——— comandi ———
  function giraPlay() {
    collegaAnalisi();
    if (audio.paused) void audio.play(); else audio.pause();
  }
  function prima() {
    if (audio.currentTime > 4) { audio.currentTime = 0; return; }
    c.vai(-1);
  }
  function muto() {
    zitto = !zitto;
    memoria.scrivi('muto', zitto);
    if (guadagno && actx) guadagno.gain.setTargetAtTime(zitto ? 0 : 1, actx.currentTime, 0.04); else audio.muted = zitto;
    sistemaVolume();
    c.hud(zitto ? 'Muto' : `Volume ${Math.round(audio.volume * 100)}%`, zitto ? ic.muto : ic.volume);
  }
  function volume(d: number) {
    if (zitto) muto();
    audio.volume = clamp(Math.round((audio.volume + d) * 100) / 100, 0, 1);
    c.hud(`Volume ${Math.round(audio.volume * 100)}%`, ic.volume);
  }
  function cambiaSeguito(valore = !seguito) {
    seguito = valore;
    memoria.scrivi('seguito', seguito);
    bSeguito.classList.toggle('su', seguito);
    sc.el.querySelector('.a-seguito-s')?.classList.toggle('su', seguito);
    c.hud(seguito ? 'Di seguito: suona anche gli altri brani' : 'Solo questo brano', ic.elenco);
  }
  function cambiaRipeti() {
    ripeti = !ripeti;
    memoria.scrivi('ripeti', ripeti);
    bRipeti.classList.toggle('su', ripeti);
    c.hud(ripeti ? 'Ripeti il brano' : 'Avanti col prossimo', ic.ripeti);
  }
  function sistemaVolume() {
    vol.value = String(zitto ? 0 : audio.volume);
    vol.style.setProperty('--p', `${(zitto ? 0 : audio.volume) * 100}%`);
    bVol.innerHTML = zitto || audio.volume === 0 ? ic.muto : ic.volume;
  }
  audio.addEventListener('volumechange', () => { sistemaVolume(); memoria.scrivi('volume', audio.volume); });
  vol.addEventListener('input', () => { if (zitto) muto(); audio.volume = +vol.value; });
  sistemaVolume();
  audio.addEventListener('play', () => { bPlay.innerHTML = ic.pausa; stanza.classList.add('suona'); });
  audio.addEventListener('pause', () => { bPlay.innerHTML = ic.play; stanza.classList.remove('suona'); });
  audio.addEventListener('ended', () => {
    if (ripeti) { audio.currentTime = 0; void audio.play(); return; }
    if (seguito && c.fratelli() > 1) c.vai(1);
  });
  // si parte da soli, come mettere giù la puntina
  collegaAnalisi();
  void audio.play().catch(() => { /* aspetta un clic */ });

  // ——— la scaletta: i brani della cartella (il componente è in scaletta.ts) ———
  /** La riga «Di seguito» sotto il titolo della scaletta. */
  function riga() {
    return h(`button.a-seguito-s${seguito ? '.su' : ''}`, { title: 'Suona anche gli altri brani, uno dopo l\'altro (T)', onclick: () => cambiaSeguito() },
      h('i.led'), 'Di seguito', h('small', null, seguito ? 'suona anche gli altri' : 'suona solo quello scelto'));
  }

  const tasti: Tasto[] = [
    { k: [' ', 'k'], etichetta: 'Spazio', cosa: 'Play / pausa', fai: giraPlay },
    { k: ['arrowleft'], etichetta: '←', cosa: '5 secondi indietro', fai: () => { audio.currentTime = Math.max(0, audio.currentTime - 5); } },
    { k: ['arrowright'], etichetta: '→', cosa: '5 secondi avanti', fai: () => { audio.currentTime = Math.min(audio.duration || 0, audio.currentTime + 5); } },
    { k: ['arrowup'], etichetta: '↑', cosa: 'Più volume', fai: () => volume(0.05) },
    { k: ['arrowdown'], etichetta: '↓', cosa: 'Meno volume', fai: () => volume(-0.05) },
    { k: ['m'], etichetta: 'M', cosa: 'Muto', fai: muto },
    { k: ['n'], etichetta: 'N', cosa: 'Brano dopo', fai: () => c.vai(1) },
    { k: ['p'], etichetta: 'P', cosa: 'Brano prima (o da capo)', fai: prima },
    { k: ['r'], etichetta: 'R', cosa: 'Ripeti il brano', fai: cambiaRipeti },
    { k: ['l'], etichetta: 'L', cosa: 'Scaletta', fai: () => sc.gira() },
    { k: ['t'], etichetta: 'T', cosa: 'Di seguito: suona anche gli altri brani', fai: () => cambiaSeguito() },
    { k: ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'], etichetta: '0 … 9', cosa: 'Vai al 0%, 10%… 90%', fai: (e) => { audio.currentTime = (+e.key / 10) * (audio.duration || 0); } },
  ];

  return {
    tasti,
    info: () => [artista, tempo(info.durata)].filter(Boolean).join(' · '),
    scheda: async () => {
      const righe = Object.entries(tag).filter(([k]) => !/^(encoder|major_brand|minor_version|compatible_brands|itunsmpb|itunnorm)$/i.test(k)).slice(0, 14);
      return [h('dl', null, ...righe.map(([k, v]) => h('div', null, h('dt', null, k.toLowerCase()), h('dd', { title: v }, v))))];
    },
    smonta() {
      cancelAnimationFrame(giro);
      audio.pause();
      audio.removeAttribute('src');
      audio.load();
      void actx?.close();
    },
  };
}

/** Il colore medio della copertina, un po' più vivo: tinge la stanza. */
async function tintaDa(url: string): Promise<string | null> {
  const img = new Image();
  img.src = url;
  try { await img.decode(); } catch { return null; }
  const cv = document.createElement('canvas');
  cv.width = cv.height = 16;
  const g = cv.getContext('2d')!;
  g.drawImage(img, 0, 0, 16, 16);
  const d = g.getImageData(0, 0, 16, 16).data;
  let r = 0, gg = 0, b = 0, n = 0;
  for (let i = 0; i < d.length; i += 4) {
    const mx = Math.max(d[i], d[i + 1], d[i + 2]), mn = Math.min(d[i], d[i + 1], d[i + 2]);
    const peso = 1 + (mx - mn) / 32; // i colori saturi contano di più
    r += d[i] * peso; gg += d[i + 1] * peso; b += d[i + 2] * peso; n += peso;
  }
  return `rgb(${Math.round(r / n)}, ${Math.round(gg / n)}, ${Math.round(b / n)})`;
}
