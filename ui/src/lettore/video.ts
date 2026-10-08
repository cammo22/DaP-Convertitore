// Il cinema: nero, il video al centro con la luce che gli esce intorno (come le TV con l'ambilight), i comandi
// di vetro in basso che spariscono quando guardi. Riprende da dove eri rimasto, passa all'episodio dopo.
import { chiedi } from '../ponte';
import { h, clamp, durata as tempo } from '../util';
import { attesa, ic, memoria, menuSu, schermoIntero, type Contesto, type Tasto, type Vista } from './comune';
import { Fila, copiabile, nativo, type InfoMedia } from './media';

export async function monta(c: Contesto): Promise<Vista> {
  const f = c.scheda;
  c.palco.append(attesa('Guardo dentro il video…'));
  const info = await chiedi<InfoMedia>('media');
  if (!c.viva()) return { tasti: [], smonta() {} };

  const video = h<HTMLVideoElement>('video.v-video', { playsinline: true, preload: 'auto' });
  video.crossOrigin = 'anonymous';
  video.volume = memoria.leggi<number>('volume') ?? 1;
  video.muted = memoria.leggi<boolean>('muto') ?? false;
  const luce = h<HTMLCanvasElement>('canvas.v-luce', { width: 48, height: 27 });
  const cinema = h('div.v-cinema', null, luce, video);
  const centro = h('div.v-centro');
  const capitolo = h('span.v-capitolo');

  // ——— la linea del tempo ———
  const fatto = h('i.v-fatto');
  const pronto = h('i.v-pronto');
  const pallino = h('i.v-pallino');
  const bolla = h('span.v-bolla');
  const tacche = h('div.v-tacche', null, ...info.capitoli.filter((x) => x.inizio > 0).map((x) => h('i', { style: { left: `${(x.inizio / info.durata) * 100}%` }, title: x.titolo })));
  const linea = h('div.v-linea', null, h('div.v-binario', null, pronto, fatto, tacche), pallino, bolla);
  const ora = h('span.v-ora', null, '0:00');
  const tot = h('span.v-tot', null, tempo(info.durata));
  const bPlay = h('button.v-b.grande', { title: 'Play / pausa (Spazio)', html: ic.play, onclick: () => giraPlay() });
  const bVol = h('button.v-b', { title: 'Muto (M)', html: video.muted ? ic.muto : ic.volume, onclick: () => muto() });
  const vol = h<HTMLInputElement>('input.v-vol', { type: 'range', min: 0, max: 1, step: 0.01, value: video.volume });
  const bSub = h('button.v-b', { title: 'Sottotitoli (C)', html: ic.sottotitoli, onclick: (e: Event) => menuSottotitoli(e.currentTarget as HTMLElement) });
  const bTracce = info.audio.length > 1 ? h('button.v-b', { title: 'Traccia audio (A)', html: ic.tracce, onclick: (e: Event) => menuTracce(e.currentTarget as HTMLElement) }) : null;
  const bVel = h('button.v-b.testo', { title: 'Velocità ([ e ])', onclick: (e: Event) => menuVelocita(e.currentTarget as HTMLElement) }, '1×');
  const comandi = h('div.v-comandi', null,
    linea,
    h('div.v-riga', null,
      bPlay,
      h('button.v-b', { title: '10 secondi indietro (J)', html: ic.indietro10, onclick: () => salta(-10) }),
      h('button.v-b', { title: '10 secondi avanti (L)', html: ic.avanti10, onclick: () => salta(10) }),
      h('div.v-volume', null, bVol, vol),
      h('span.v-tempi', null, ora, h('i', null, '/'), tot),
      capitolo,
      h('span.v-spazio'),
      bVel,
      info.sottotitoli.some((x) => x.testo) || info.esterni.length ? bSub : null,
      bTracce,
      h('button.v-b', { title: 'Salva il fotogramma (S)', html: ic.foto, onclick: () => void fotogramma() }),
      document.pictureInPictureEnabled ? h('button.v-b', { title: 'Finestrella sempre sopra (P)', html: ic.pip, onclick: () => void pip() }) : null,
      h('button.v-b', { title: 'Schermo intero (F)', html: ic.schermo, onclick: () => schermoIntero() })));
  c.palco.replaceChildren(h('div.v-sala', null, cinema, centro, comandi));
  const sala = c.palco.firstElementChild as HTMLElement;

  // ——— da dove arriva il video: com'è, o tradotto al volo ———
  let fila: Fila | null = null;
  let traccia = 0;
  const viaFila = (da: number) => {
    fila?.ferma();
    fila = new Fila(video, info, { traccia, copia: copiabile(info) }, c.stato);
    void fila.parti(da);
    c.info();
  };
  if (nativo(info)) {
    video.src = f.url;
    // se WebView2 non ce la fa (codec che non ha, o solo l'audio di un HEVC), si passa a FFmpeg
    video.addEventListener('error', () => { if (!fila) viaFila(video.currentTime || riprendiDa()); }, { once: true });
    video.addEventListener('loadedmetadata', () => { if (info.video && video.videoWidth === 0 && !fila) viaFila(0); }, { once: true });
  } else viaFila(0);

  // ——— riprendi da dove eri ———
  const chiave = `pos:${f.percorso}`;
  function riprendiDa() {
    const p = memoria.leggi<number>(chiave) ?? 0;
    return p > 20 && p < info.durata - 25 ? p : 0;
  }
  const ripresa = riprendiDa();
  let schedaRipresa: HTMLElement | null = null;
  if (ripresa) {
    schedaRipresa = h('button.v-riprendi', { onclick: () => { cerca(ripresa); schedaRipresa?.remove(); } }, h('span', { html: ic.play }), `Riprendi da ${tempo(ripresa)}`, h('kbd', null, 'R'));
    sala.append(schedaRipresa);
    setTimeout(() => schedaRipresa?.classList.add('via'), 9000);
  }
  let ultimoSalvato = 0;

  video.addEventListener('loadeddata', () => { void video.play().catch(() => {}); }, { once: true });

  // ——— comandi ———
  function giraPlay() {
    if (video.paused) void video.play(); else video.pause();
    pulsa(video.paused ? ic.pausa : ic.play);
  }
  function pulsa(icona: string) {
    centro.innerHTML = icona;
    centro.classList.remove('su');
    void centro.offsetWidth;
    centro.classList.add('su');
  }
  function cerca(t: number) {
    t = clamp(t, 0, Math.max(0, (info.durata || video.duration || 0) - 0.05));
    if (fila) fila.vai(t); else video.currentTime = t;
    aggiorna();
  }
  function salta(s: number) {
    cerca(video.currentTime + s);
    c.hud(`${s > 0 ? '+' : '−'}${Math.abs(s)} s`, s > 0 ? ic.avanti10 : ic.indietro10);
  }
  function muto() {
    video.muted = !video.muted;
    memoria.scrivi('muto', video.muted);
    c.hud(video.muted ? 'Muto' : `Volume ${Math.round(video.volume * 100)}%`, video.muted ? ic.muto : ic.volume);
  }
  function volume(d: number) {
    video.muted = false;
    video.volume = clamp(Math.round((video.volume + d) * 100) / 100, 0, 1);
    c.hud(`Volume ${Math.round(video.volume * 100)}%`, ic.volume);
  }
  video.addEventListener('volumechange', () => {
    vol.value = String(video.muted ? 0 : video.volume);
    vol.style.setProperty('--p', `${(video.muted ? 0 : video.volume) * 100}%`);
    bVol.innerHTML = video.muted || video.volume === 0 ? ic.muto : ic.volume;
    memoria.scrivi('volume', video.volume);
  });
  vol.style.setProperty('--p', `${(video.muted ? 0 : video.volume) * 100}%`);
  vol.addEventListener('input', () => { video.muted = false; video.volume = +vol.value; });

  const velocita = [0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2, 3];
  function cambiaVelocita(v: number) {
    video.playbackRate = v;
    bVel.textContent = `${v.toLocaleString('it-IT')}×`;
    c.hud(`Velocità ${v.toLocaleString('it-IT')}×`, ic.velocita);
  }
  function menuVelocita(b: HTMLElement) {
    menuSu(b, velocita.map((v) => ({ testo: v === 1 ? 'Normale' : `${v.toLocaleString('it-IT')}×`, attiva: video.playbackRate === v, fai: () => cambiaVelocita(v) })), true);
  }

  // ——— sottotitoli: dentro il video (testo) o accanto (.srt, .vtt, .ass) ———
  type Sub = { nome: string; carica: () => Promise<string> };
  const sub: Sub[] = [
    ...info.sottotitoli.filter((x) => x.testo).map((x, i) => ({
      nome: [x.titolo, lingua(x.lingua)].filter(Boolean).join(' · ') || `Traccia ${i + 1}`,
      carica: () => chiedi<string>('vtt', { traccia: info.sottotitoli.indexOf(x) }),
    })),
    ...info.esterni.map((x) => ({ nome: x.nome, carica: () => chiedi<string>('vtt', { file: x.percorso }) })),
  ];
  let subAttivo = -1;
  async function mettiSottotitoli(i: number) {
    subAttivo = i;
    video.querySelectorAll('track').forEach((t) => { URL.revokeObjectURL(t.src); t.remove(); });
    bSub.classList.toggle('su', i >= 0);
    if (i < 0) { c.hud('Sottotitoli spenti', ic.sottotitoli); return; }
    try {
      const vtt = await sub[i].carica();
      const t = h<HTMLTrackElement>('track', { kind: 'subtitles', srclang: 'it', label: sub[i].nome, src: URL.createObjectURL(new Blob([vtt], { type: 'text/vtt' })) });
      video.append(t);
      t.track.mode = 'showing';
      c.hud(sub[i].nome, ic.sottotitoli);
    } catch (e) { c.hud((e as Error).message, ic.sottotitoli); }
  }
  function menuSottotitoli(b: HTMLElement) {
    menuSu(b, [{ testo: 'Spenti', attiva: subAttivo < 0, fai: () => void mettiSottotitoli(-1) }, ...sub.map((x, i) => ({ testo: x.nome, attiva: subAttivo === i, fai: () => void mettiSottotitoli(i) }))], true);
  }

  // ——— tracce audio: WebView2 suona solo la prima, le altre passano da FFmpeg ———
  function mettiTraccia(i: number) {
    if (i === traccia) return;
    traccia = i;
    const t = video.currentTime;
    if (fila) fila.cambiaTraccia(i);
    else viaFila(t);
    const a = info.audio[i];
    c.hud([a.titolo, lingua(a.lingua)].filter(Boolean).join(' · ') || `Traccia ${i + 1}`, ic.tracce);
  }
  function menuTracce(b: HTMLElement) {
    menuSu(b, info.audio.map((a, i) => ({ testo: [a.titolo, lingua(a.lingua), a.codec.toUpperCase()].filter(Boolean).join(' · '), attiva: traccia === i, fai: () => mettiTraccia(i) })), true);
  }

  async function fotogramma() {
    if (!video.videoWidth) return;
    const cv = h<HTMLCanvasElement>('canvas', { width: video.videoWidth, height: video.videoHeight });
    cv.getContext('2d')!.drawImage(video, 0, 0);
    try {
      const dove = await chiedi<string>('fotogramma', { png: cv.toDataURL('image/png'), tempo: video.currentTime });
      c.hud(`Salvato: ${dove.split('\\').pop()}`, ic.foto);
    } catch (e) { c.hud((e as Error).message, ic.foto); }
  }

  async function pip() {
    try {
      if (document.pictureInPictureElement) await document.exitPictureInPicture();
      else await video.requestPictureInPicture();
    } catch { /* non si può */ }
  }

  // ——— la luce intorno: il video in piccolissimo, sfocato dietro ———
  let luceAccesa = memoria.leggi<boolean>('luce') ?? true;
  const lc = luce.getContext('2d', { willReadFrequently: false })!;
  let luceTimer = 0;
  function disegnaLuce() {
    if (luceAccesa && video.videoWidth && !video.paused) {
      try { lc.drawImage(video, 0, 0, luce.width, luce.height); } catch { /* non ancora */ }
    }
    luceTimer = window.setTimeout(disegnaLuce, 180);
  }
  disegnaLuce();
  luce.classList.toggle('spenta', !luceAccesa);

  // ——— la linea del tempo: clic, trascina, bolla col tempo ———
  let tieni = false;
  const daX = (x: number) => { const r = linea.getBoundingClientRect(); return clamp((x - r.left) / r.width, 0, 1) * (info.durata || video.duration || 0); };
  linea.addEventListener('pointerdown', (e) => { tieni = true; linea.setPointerCapture(e.pointerId); mostraPosizione(daX(e.clientX)); });
  linea.addEventListener('pointermove', (e) => {
    const t = daX(e.clientX);
    const r = linea.getBoundingClientRect();
    const cap = [...info.capitoli].reverse().find((x) => x.inizio <= t);
    bolla.textContent = cap?.titolo ? `${tempo(t)} · ${cap.titolo}` : tempo(t);
    bolla.style.left = `${clamp(e.clientX - r.left, 30, r.width - 30)}px`;
    if (tieni) mostraPosizione(t);
  });
  linea.addEventListener('pointerup', (e) => { if (tieni) { tieni = false; cerca(daX(e.clientX)); } });
  function mostraPosizione(t: number) {
    const p = (t / (info.durata || 1)) * 100;
    fatto.style.width = `${p}%`;
    pallino.style.left = `${p}%`;
    ora.textContent = tempo(t);
  }

  function aggiorna() {
    const d = info.durata || video.duration || 0;
    if (!tieni) mostraPosizione(video.currentTime);
    const b = video.buffered;
    let fine = 0;
    for (let i = 0; i < b.length; i++) if (b.start(i) <= video.currentTime + 1) fine = Math.max(fine, b.end(i));
    pronto.style.width = `${d ? (fine / d) * 100 : 0}%`;
    const cap = [...info.capitoli].reverse().find((x) => x.inizio <= video.currentTime);
    capitolo.textContent = cap?.titolo ?? '';
    if (Math.abs(video.currentTime - ultimoSalvato) > 5) {
      ultimoSalvato = video.currentTime;
      memoria.scrivi(chiave, video.currentTime > 20 && video.currentTime < d - 25 ? Math.round(video.currentTime) : null);
    }
  }
  video.addEventListener('timeupdate', aggiorna);
  video.addEventListener('progress', aggiorna);
  video.addEventListener('play', () => { bPlay.innerHTML = ic.pausa; sala.classList.add('va'); schedaRipresa?.classList.add('via'); });
  video.addEventListener('pause', () => { bPlay.innerHTML = ic.play; sala.classList.remove('va'); });
  video.addEventListener('waiting', () => sala.classList.add('aspetta'));
  video.addEventListener('playing', () => sala.classList.remove('aspetta'));

  // ——— fine: il prossimo della cartella (le serie), con la possibilità di fermarlo ———
  let prossimo: number | undefined;
  video.addEventListener('ended', () => {
    memoria.scrivi(chiave, null);
    if (c.fratelli() < 2) return;
    let n = 6;
    const carta = h('div.v-prossimo', null, h('b', null, 'Il prossimo'), h('span'), h('div', null,
      h('button.l-tasto.oro', { onclick: () => { clearInterval(prossimo); c.vai(1); } }, 'Guarda ora'),
      h('button.l-tasto', { onclick: () => { clearInterval(prossimo); carta.remove(); } }, 'Resta qui')));
    const conta = () => { (carta.children[1] as HTMLElement).textContent = `tra ${n} secondi`; if (n-- <= 0) { clearInterval(prossimo); c.vai(1); } };
    conta();
    prossimo = window.setInterval(conta, 1000);
    sala.append(carta);
  });

  // clic: play/pausa; doppio clic: schermo intero (il primo clic del doppio non deve fermare)
  let clic: number | undefined;
  cinema.addEventListener('click', () => { clearTimeout(clic); clic = window.setTimeout(giraPlay, 220); });
  cinema.addEventListener('dblclick', () => { clearTimeout(clic); schermoIntero(); });
  cinema.addEventListener('wheel', (e) => { volume(e.deltaY < 0 ? 0.05 : -0.05); }, { passive: true });

  const passoFotogramma = () => 1 / (info.video?.fps || 25);
  const tasti: Tasto[] = [
    { k: [' ', 'k'], etichetta: 'Spazio K', cosa: 'Play / pausa', fai: giraPlay },
    { k: ['arrowleft'], etichetta: '←', cosa: '5 secondi indietro', fai: () => salta(-5) },
    { k: ['arrowright'], etichetta: '→', cosa: '5 secondi avanti', fai: () => salta(5) },
    { k: ['j'], etichetta: 'J', cosa: '10 secondi indietro', fai: () => salta(-10) },
    { k: ['l'], etichetta: 'L', cosa: '10 secondi avanti', fai: () => salta(10) },
    { k: ['shift+arrowleft'], etichetta: 'Maiusc ←', cosa: '1 minuto indietro', fai: () => salta(-60) },
    { k: ['shift+arrowright'], etichetta: 'Maiusc →', cosa: '1 minuto avanti', fai: () => salta(60) },
    { k: [','], etichetta: ',', cosa: 'Un fotogramma indietro', fai: () => { video.pause(); cerca(video.currentTime - passoFotogramma()); } },
    { k: ['.'], etichetta: '.', cosa: 'Un fotogramma avanti', fai: () => { video.pause(); cerca(video.currentTime + passoFotogramma()); } },
    { k: ['arrowup'], etichetta: '↑', cosa: 'Più volume', fai: () => volume(0.05) },
    { k: ['arrowdown'], etichetta: '↓', cosa: 'Meno volume', fai: () => volume(-0.05) },
    { k: ['m'], etichetta: 'M', cosa: 'Muto', fai: muto },
    { k: ['f'], etichetta: 'F', cosa: 'Schermo intero (anche doppio clic)', fai: schermoIntero },
    { k: ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'], etichetta: '0 … 9', cosa: 'Vai al 0%, 10%… 90%', fai: (e) => cerca((+e.key / 10) * (info.durata || video.duration)) },
    { k: ['home'], etichetta: 'Inizio', cosa: 'Dall\'inizio', fai: () => cerca(0) },
    { k: ['[', '-'], etichetta: '[', cosa: 'Più lento', fai: () => cambiaVelocita(velocita[Math.max(0, velocita.indexOf(video.playbackRate) - 1)] ?? 1) },
    { k: [']', '+'], etichetta: ']', cosa: 'Più veloce', fai: () => cambiaVelocita(velocita[Math.min(velocita.length - 1, velocita.indexOf(video.playbackRate) + 1)] ?? 1) },
    { k: ['c'], etichetta: 'C', cosa: 'Sottotitoli', fai: () => { if (sub.length) void mettiSottotitoli(subAttivo + 1 >= sub.length ? -1 : subAttivo + 1); else c.hud('Niente sottotitoli', ic.sottotitoli); } },
    { k: ['a'], etichetta: 'A', cosa: 'Traccia audio', fai: () => { if (info.audio.length > 1) mettiTraccia((traccia + 1) % info.audio.length); } },
    { k: ['s'], etichetta: 'S', cosa: 'Salva il fotogramma in PNG', fai: () => void fotogramma() },
    { k: ['p'], etichetta: 'P', cosa: 'Finestrella sempre sopra', fai: () => void pip() },
    { k: ['b'], etichetta: 'B', cosa: 'La luce intorno al video', fai: () => { luceAccesa = !luceAccesa; memoria.scrivi('luce', luceAccesa); luce.classList.toggle('spenta', !luceAccesa); c.hud(luceAccesa ? 'Luce accesa' : 'Luce spenta', ic.sole); } },
    { k: ['r'], etichetta: 'R', cosa: 'Riprendi da dove eri', fai: () => { if (ripresa) { cerca(ripresa); schedaRipresa?.remove(); } } },
    { k: ['n'], etichetta: 'N', cosa: 'Il video dopo', fai: () => c.vai(1) },
  ];

  return {
    tasti,
    barraSopra: true,
    info: () => {
      const v = info.video;
      const parti = [v ? `${v.larghezza}×${v.altezza}` : null, v ? `${v.codec.toUpperCase()}${v.hdr ? ' HDR' : ''}` : null, tempo(info.durata)];
      if (fila) parti.push('tradotto al volo');
      return parti.filter(Boolean).join(' · ');
    },
    scheda: async () => {
      const v = info.video;
      const righe: [string, string][] = [
        ['Durata', tempo(info.durata)],
        ...(v ? [['Video', `${v.codec.toUpperCase()} · ${v.larghezza}×${v.altezza} · ${Math.round(v.fps * 100) / 100} fps${v.hdr ? ' · HDR' : ''} · ${v.bit} bit`] as [string, string]] : []),
        ...info.audio.map((a, i) => [`Audio ${info.audio.length > 1 ? i + 1 : ''}`.trim(), [a.codec.toUpperCase(), lingua(a.lingua), a.titolo].filter(Boolean).join(' · ')] as [string, string]),
        ['Bitrate', info.bitrate ? `${(info.bitrate / 1e6).toLocaleString('it-IT', { maximumFractionDigits: 1 })} Mbit/s` : '—'],
        ['Contenitore', info.contenitore.split(',')[0].toUpperCase()],
      ];
      return [h('dl', null, ...righe.map(([k, v2]) => h('div', null, h('dt', null, k), h('dd', null, v2))))];
    },
    smonta() {
      clearTimeout(luceTimer);
      clearInterval(prossimo);
      if (video.currentTime > 20 && video.currentTime < (info.durata || 0) - 25) memoria.scrivi(chiave, Math.round(video.currentTime));
      fila?.ferma();
      video.pause();
      video.removeAttribute('src');
      video.load();
      if (document.pictureInPictureElement) void document.exitPictureInPicture().catch(() => {});
    },
  } as Vista;
}

const lingue: Record<string, string> = { ita: 'Italiano', it: 'Italiano', eng: 'Inglese', en: 'Inglese', fre: 'Francese', fra: 'Francese', ger: 'Tedesco', deu: 'Tedesco', spa: 'Spagnolo', jpn: 'Giapponese', por: 'Portoghese', rus: 'Russo', chi: 'Cinese', zho: 'Cinese', kor: 'Coreano' };
export const lingua = (l: string | null) => (l && l !== 'und' ? lingue[l.toLowerCase()] ?? l.toUpperCase() : null);
