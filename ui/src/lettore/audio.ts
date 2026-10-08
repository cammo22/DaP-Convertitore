// Il giradischi: il disco gira (33 giri, la copertina fa da etichetta), il braccio scende e avanza col brano,
// la forma d'onda si scorre col dito, sotto l'analizzatore di spettro balla. Finito un brano parte il dopo.
import { chiedi } from '../ponte';
import { h, clamp, durata as tempo } from '../util';
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

  const audio = h<HTMLAudioElement>('audio', { preload: 'auto' });
  audio.crossOrigin = 'anonymous';
  audio.volume = memoria.leggi<number>('volume') ?? 1;
  // il muto sta dopo l'analizzatore: lo spettro balla anche a volume zero
  let zitto = memoria.leggi<boolean>('muto') ?? false;

  // ——— il disco e il braccio ———
  const etichetta = info.copertina
    ? h('div.a-etichetta', { style: { backgroundImage: `url(${info.copertina})` } })
    : h('div.a-etichetta.vuota', null, h('b', null, 'DaP'), h('span', null, (f.estensione || '').replace('.', '').toUpperCase()));
  const disco = h('div.a-disco', null, h('div.a-solchi'), etichetta, h('i.a-perno'));
  const braccio = h('div.a-braccio', { html: braccioSvg });
  const piatto = h('div.a-piatto', null, h('div.a-base'), disco, h('div.a-riflesso'), braccio);

  // ——— la parte di destra ———
  const onda = h<HTMLCanvasElement>('canvas.a-onda');
  const bollaOnda = h('span.a-bolla');
  const ora = h('span', null, '0:00');
  const bPlay = h('button.a-play', { title: 'Play / pausa (Spazio)', html: ic.play, onclick: () => giraPlay() });
  let ripeti = memoria.leggi<boolean>('ripeti') ?? false;
  const bRipeti = h(`button.a-b${ripeti ? '.su' : ''}`, { title: 'Ripeti il brano (R)', html: ic.ripeti, onclick: () => cambiaRipeti() });
  const vol = h<HTMLInputElement>('input.v-vol', { type: 'range', min: 0, max: 1, step: 0.01, value: audio.volume });
  vol.style.setProperty('--p', `${audio.volume * 100}%`);
  const bVol = h('button.a-b', { title: 'Muto (M)', html: zitto ? ic.muto : ic.volume, onclick: () => muto() });
  const spettro = h<HTMLCanvasElement>('canvas.a-spettro');
  const tecnica = [
    (f.estensione || '').replace('.', '').toUpperCase(),
    a0 && a0.codec !== f.estensione.replace('.', '') ? a0.codec.toUpperCase().replace('PCM_', 'PCM ') : null,
    a0?.campionamento ? `${(a0.campionamento / 1000).toLocaleString('it-IT')} kHz` : null,
    a0?.canali ? (a0.canali === 1 ? 'mono' : a0.canali === 2 ? 'stereo' : `${a0.canali} canali`) : null,
    a0?.bitrate || info.bitrate ? `${Math.round((a0?.bitrate || info.bitrate) / 1000)} kbit/s` : null,
  ].filter(Boolean) as string[];
  const scaletta = h('aside.a-scaletta');
  const stanza = h('div.a-stanza', null,
    info.copertina ? h('div.a-fondo', { style: { backgroundImage: `url(${info.copertina})` } }) : h('div.a-fondo.vuoto'),
    piatto,
    h('div.a-destra', null,
      h('div.a-titoli', null,
        h('h1', { title: titolo }, titolo),
        artista ? h('div.a-artista', null, artista) : null,
        album || anno ? h('div.a-album', null, [album, anno].filter(Boolean).join(' · ')) : null),
      h('div.a-tecnica', null, ...tecnica.map((x) => h('span', null, x))),
      h('div.a-onda-box', null, onda, bollaOnda),
      h('div.a-tempi', null, ora, h('span', null, tempo(info.durata))),
      h('div.a-comandi', null,
        h('button.a-b.solo-fratelli', { title: 'Brano prima (P)', html: ic.prima, onclick: () => prima() }),
        bPlay,
        h('button.a-b.solo-fratelli', { title: 'Brano dopo (N)', html: ic.dopo, onclick: () => c.vai(1) }),
        h('span.a-spazio'),
        bRipeti,
        h('button.a-b.solo-fratelli', { title: 'Scaletta (L)', html: ic.elenco, onclick: () => giraScaletta() }),
        h('div.v-volume', null, bVol, vol)),
      spettro),
    scaletta);
  c.palco.replaceChildren(stanza);

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

  // ——— lo spettro, 60 volte al secondo ———
  let giro = 0;
  const cime: number[] = [];
  function anima() {
    giro = requestAnimationFrame(anima);
    ora.textContent = tempo(audio.currentTime);
    disegnaOnda();
    // il braccio entra col brano: dall'orlo verso l'etichetta
    const p = audio.duration ? audio.currentTime / audio.duration : 0;
    braccio.style.setProperty('--avanza', String(p));
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
    const base = hh * 0.72;
    for (let i = 0; i < n; i++) {
      // bande in scala logaritmica, da 40 Hz a 16 kHz
      let v = 0;
      if (dati.length && actx) {
        const f0 = 40 * Math.pow(16000 / 40, i / n), f1 = 40 * Math.pow(16000 / 40, (i + 1) / n);
        const b0 = Math.floor(f0 / (actx.sampleRate / 2) * dati.length), b1 = Math.max(b0 + 1, Math.floor(f1 / (actx.sampleRate / 2) * dati.length));
        for (let b = b0; b < b1; b++) v = Math.max(v, dati[b]);
        v = Math.pow(v / 255, 1.6);
      }
      const alto = Math.max(2, v * base);
      cime[i] = Math.max((cime[i] ?? 0) - 0.6, alto);
      const x = i * largo + 1;
      const gr = g.createLinearGradient(0, base, 0, base - base);
      gr.addColorStop(0, '#5dffb4');
      gr.addColorStop(0.6, '#ffd54a');
      gr.addColorStop(1, '#ff3df2');
      g.fillStyle = gr;
      g.fillRect(x, base - alto, largo - 3, alto);
      g.fillStyle = 'rgba(255,255,255,0.85)';
      g.fillRect(x, base - cime[i] - 3, largo - 3, 2);
      // il riflesso sul piano lucido
      g.globalAlpha = 0.16;
      g.fillStyle = gr;
      g.fillRect(x, base + 3, largo - 3, alto * 0.35);
      g.globalAlpha = 1;
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
    if (c.fratelli() > 1) c.vai(1);
  });
  // si parte da soli, come mettere giù la puntina
  collegaAnalisi();
  void audio.play().catch(() => { /* aspetta un clic */ });

  // ——— la scaletta: i brani della cartella ———
  function giraScaletta() {
    const aperta = stanza.classList.toggle('con-scaletta');
    if (!aperta) return;
    const { file, indice } = c.elenco();
    scaletta.replaceChildren(h('b', null, 'SCALETTA'), h('div.a-brani', null, ...file.map((p, i) =>
      h(`button.a-brano${i === indice ? '.su' : ''}`, { onclick: () => c.vaiA(i) }, h('span', null, String(i + 1).padStart(2, '0')), p.split('\\').pop()!.replace(/\.[^.]+$/, '')))));
    scaletta.querySelector('.su')?.scrollIntoView({ block: 'center' });
  }
  // la scaletta aperta l'ultima volta: si riapre appena si sa quali brani ci sono accanto
  if (memoria.leggi<boolean>('scaletta')) {
    if (c.fratelli() > 1) giraScaletta();
    else document.addEventListener('dap-fratelli', () => { if (c.viva() && c.fratelli() > 1 && !stanza.classList.contains('con-scaletta')) giraScaletta(); }, { once: true });
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
    { k: ['l'], etichetta: 'L', cosa: 'Scaletta', fai: () => { giraScaletta(); memoria.scrivi('scaletta', stanza.classList.contains('con-scaletta')); } },
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

const braccioSvg = `<svg viewBox="0 0 120 300">
<defs><linearGradient id="br" x1="0" x2="1"><stop offset="0" stop-color="#8d8a96"/><stop offset=".5" stop-color="#f1eef7"/><stop offset="1" stop-color="#77737f"/></linearGradient></defs>
<circle cx="88" cy="34" r="26" fill="#1c1b22" stroke="#3a3842" stroke-width="3"/>
<circle cx="88" cy="34" r="12" fill="url(#br)"/>
<rect x="76" y="2" width="24" height="18" rx="4" fill="#2b2a31"/>
<path d="M88 40 L88 200 Q88 236 60 262" fill="none" stroke="url(#br)" stroke-width="7" stroke-linecap="round"/>
<rect x="40" y="252" width="34" height="22" rx="4" transform="rotate(38 57 263)" fill="#26252c" stroke="#ffd54a" stroke-width="1.5"/>
<circle cx="50" cy="276" r="2.5" fill="#ff3df2"/>
</svg>`;
