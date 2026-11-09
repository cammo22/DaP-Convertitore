// La colonna a destra per i file che non sono testo: a sinistra la figura grande, a destra l'indice cliccabile come la
// minimappa di VS Code ma fatto di figurine: le pagine di un PDF, i fotogrammi di un video, le onde di un brano.
// Per tutto il resto (foto, documenti, cartelle…) c'è una figurina sola: nessun file resta senza anteprima.
// Video e brani si scorrono col cursore come in Finder (scrub): il fotogramma, o il suono, è quello del punto dove stai; il clic riproduce.
import { chiedi } from '../ponte';
import type { InfoFile } from '../ponte';
import { h, durata } from '../util';
import { iconaCategoria, coloreCategoria, icone } from '../icone';

interface Indice { tipo: 'pagine' | 'tempo' | 'singola'; voci: number; totale: number; punti: number[] }

export interface Colonna {
  el: HTMLElement;
  mostra(f: InfoFile): void;
  /** Il tipo di indice, quando è arrivato (per il piede del pannello). */
  descrizione(): string;
  /** Si ferma tutto (il file è cambiato o il pannello si è chiuso). */
  ferma(): void;
}

const LATO_FIGURINA = 160;
const LATO_GRANDE = 720;
/** Quanto dura il «tic» di suono quando il cursore scorre su un brano senza che tu abbia cliccato. */
const SALTO = 5;

export function creaColonna(apri: () => void, pronto: () => void = () => {}): Colonna {
  const img = h<HTMLImageElement>('img.cl-img', { alt: '', draggable: 'false' });
  const segnaposto = h('div.cl-segnaposto');
  const etichetta = h('div.cl-etichetta');
  const video = h<HTMLVideoElement>('video.cl-video', { muted: true, playsinline: true, preload: 'auto', hidden: true, draggable: 'false' });
  video.muted = true;
  const testina = h('div.cl-testina', { hidden: true });
  const figura = h('div.cl-figura', { title: 'Passa il cursore: scorre. Clic: riproduce. Doppio clic: apre nel lettore' }, segnaposto, img, video, testina);
  const riempi = h('i.cl-riempi');
  const barra = h('div.cl-barra', { tabindex: '0', role: 'slider', 'aria-label': 'Posizione', hidden: true }, riempi);
  const grande = h('div.cl-grande', null, figura, barra, etichetta);
  const strisce = h('div.cl-strisce', { tabindex: '0', role: 'listbox', 'aria-label': 'Indice dell\'anteprima' });
  const bPlay = h('button.cl-play', { type: 'button', title: 'Play / pausa', html: icone.play, onclick: () => alterna() });
  const tempoSuono = h('div.cl-tempo', null, '—');
  const vol = h<HTMLInputElement>('input.cl-vol', { type: 'range', min: '0', max: '1', step: '0.01', value: '1', 'aria-label': 'Volume' });
  const audioEl = h<HTMLAudioElement>('audio.cl-audio', { preload: 'auto' });
  audioEl.hidden = true;
  vol.addEventListener('input', () => { audioEl.volume = +vol.value; video.volume = +vol.value; });
  video.volume = +vol.value;
  const bQuadro = h('button.cl-quadro', { type: 'button', title: 'Salva questo fotogramma in Immagini\\DaPConvertitore', onclick: () => void salvaQuadro() }, 'Fotogramma');
  const tasti = h('div.cl-tasti', null, '← → fotogramma · J K L');
  const suono = h('div.cl-suono', null,
    bPlay,
    tempoSuono,
    bQuadro,
    h('div.cl-vol-etichetta', null, 'VOLUME'),
    vol,
    tasti,
    audioEl);
  const el = h('div.ap-colonna', { tabindex: '-1' }, grande, suono, strisce);

  let gettone = 0;                              // cambia a ogni file: le risposte in ritardo si scartano
  let file: InfoFile | null = null;
  let indice: Indice | null = null;
  let scelta = -1;
  let tipoDescritto = '';
  const url = new Map<string, string>();        // le figure già arrivate (id:n:lato)
  const voci: HTMLElement[] = [];
  const attesa: number[] = [];                  // le figurine da chiedere, una alla volta
  let inCorso = false;
  let osserva: IntersectionObserver | null = null;

  // ——— lo scrub: video e brani ———
  /** 'video' = il tag video suona il file (fotogramma al volo); 'foto' = lo fa FFmpeg un fotogramma alla volta; 'audio' = si sente; 'muto' = niente da fare. */
  let scrub: 'video' | 'foto' | 'audio' | 'muto' | null = null;
  let audio: HTMLAudioElement | null = null;
  let suona = false;                            // il clic ha fatto partire la riproduzione
  let vuoleSuonare = false;                     // play premuto prima che il file sia pronto
  let posizione = 0;                            // secondi
  let ultimaRichiesta = -1;
  let inVolo = false;
  let pendenteFoto: number | null = null;
  let pendenteSeek: number | null = null;
  let rafSeek = 0;
  let ondaPiena = false;                        // l'onda intera del brano è arrivata
  const durataTot = () => (indice?.tipo === 'tempo' ? indice.totale : 0);
  const media = (): HTMLMediaElement | null => (scrub === 'video' ? video : scrub === 'audio' ? audio : null);

  const chiave = (n: number, lato: number) => `${file!.id}:${n}:${lato}`;

  async function chiediFigura(n: number, lato: number): Promise<string | null> {
    const k = chiave(n, lato), g = gettone;
    const c = url.get(k);
    if (c) return c;
    try {
      const r = await chiedi<{ url: string | null }>('voceAnteprima', { id: file!.id, n, lato });
      if (g !== gettone || !r.url) return null;
      url.set(k, r.url);
      // se ne tengono poche: sono data-url
      while (url.size > 600) url.delete(url.keys().next().value!);
      return r.url;
    } catch { return null; }
  }

  function mostraPosizione(t: number) {
    const tot = durataTot();
    if (!tot) { posizione = Math.max(0, t); tempoSuono.textContent = durata(posizione); return; }
    posizione = Math.max(0, Math.min(tot, t));
    const p = `${((posizione / tot) * 100).toFixed(2)}%`;
    riempi.style.width = p;
    testina.style.left = p;
    barra.setAttribute('aria-valuenow', String(Math.round(posizione)));
    barra.setAttribute('aria-valuemax', String(Math.round(tot)));
    etichetta.textContent = `${durata(posizione)} di ${durata(tot)}${suona ? ' · in riproduzione' : ''}`;
    tempoSuono.textContent = `${durata(posizione)}  /  ${durata(tot)}`;
  }

  /** Dove cade il cursore sulla figura, in secondi. */
  function tempoDa(e: { clientX: number }, el: HTMLElement): number {
    const r = el.getBoundingClientRect();
    return Math.max(0, Math.min(1, (e.clientX - r.left) / Math.max(1, r.width))) * durataTot();
  }

  /** Il fotogramma (o il punto del brano) di quel secondo: se ne chiede uno solo per volta, conta l'ultimo. */
  function vaiA(t: number) {
    mostraPosizione(t);
    if (scrub === 'video' || scrub === 'audio') {
      pendenteSeek = posizione;
      if (!rafSeek) rafSeek = requestAnimationFrame(() => {
        rafSeek = 0;
        const m = media();
        if (!m || pendenteSeek == null) return;
        const dove = pendenteSeek;
        // se sta già suonando e il punto è lì vicino non si smuove (si sentirebbe un singhiozzo)
        if (!(suona && Math.abs(m.currentTime - dove) < 0.08)) { try { m.currentTime = dove; } catch { /* non ancora pronto */ } }
        pendenteSeek = null;
      });
      if (scrub === 'video') { video.hidden = false; img.hidden = true; segnaposto.hidden = true; }
      // l'audio non si sente passando il cursore: ci si sposta dalla timeline, e si sente solo col play
    } else if (scrub === 'foto') {
      pendenteFoto = posizione;
      void pompaFoto();
    }
  }

  async function pompaFoto() {
    if (inVolo) return;
    inVolo = true;
    const g = gettone;
    while (pendenteFoto != null && g === gettone && file) {
      const t = pendenteFoto;
      pendenteFoto = null;
      if (Math.abs(t - ultimaRichiesta) < 0.2) continue;
      ultimaRichiesta = t;
      try {
        const r = await chiedi<{ url: string | null }>('scrubAnteprima', { id: file.id, t, lato: LATO_GRANDE });
        if (g !== gettone) break;
        if (r.url) { img.src = r.url; img.hidden = false; segnaposto.hidden = true; }
      } catch { /* il prossimo movimento riprova */ }
    }
    inVolo = false;
  }

  function parti(t: number) {
    const m = media();
    if (!m) { vuoleSuonare = true; return; }
    vuoleSuonare = false;
    suona = true;
    bPlay.innerHTML = icone.pausa;
    m.muted = false;
    try { m.currentTime = t; } catch { /* sotto */ }
    mostraPosizione(t);
    void m.play().catch(() => { suona = false; mostraPosizione(posizione); });
    figura.classList.add('suona');
    barra.classList.add('suona');
  }

  function pausa() {
    suona = false;
    bPlay.innerHTML = icone.play;
    media()?.pause();
    figura.classList.remove('suona');
    barra.classList.remove('suona');
    mostraPosizione(posizione);
  }

  const alterna = (t = posizione) => { if (suona) { vuoleSuonare = false; pausa(); } else parti(t); };

  function spegniMedia() {
    suona = false;
    bPlay.innerHTML = icone.play;
    cancelAnimationFrame(rafSeek);
    rafSeek = 0;
    pendenteSeek = null;
    pendenteFoto = null;
    ultimaRichiesta = -1;
    vuoleSuonare = false;
    for (const m of [video, audioEl]) { m.pause(); m.removeAttribute('src'); m.load(); }
    audio = null;
    scrub = null;
    ondaPiena = false;
    video.hidden = true;
    testina.hidden = true;
    barra.hidden = true;
    figura.classList.remove('suona', 'onda', 'scorre');
    barra.classList.remove('suona');
  }

  /** Il file vero per il tag video/audio: se la pagina lo suona lo scrub è al volo, se no resta quello di FFmpeg. */
  async function preparaScrub(f: InfoFile, g: number) {
    const eAudio = f.categoria === 'audio';
    scrub = eAudio ? 'audio' : 'foto';
    barra.hidden = false;
    testina.hidden = true;
    figura.classList.toggle('scorre', !eAudio);
    figura.classList.toggle('onda', eAudio);
    mostraPosizione(eAudio ? 0 : (indice!.punti[0] ?? 0));
    let fonte: string | null = null;
    try { fonte = (await chiedi<{ url: string | null }>('fonteAnteprima', { id: f.id })).url; } catch { /* resta com'è */ }
    if (g !== gettone || !fonte) {
      if (eAudio && g === gettone) tempoSuono.textContent = 'non si sente';
      return;
    }
    if (f.categoria === 'video') {
      video.onloadeddata = () => {
        if (g !== gettone || scrub !== 'foto') return;
        scrub = 'video';
        video.hidden = false;
        img.hidden = true;
        segnaposto.hidden = true;
        if (vuoleSuonare) parti(posizione);
      };
      video.onerror = () => { if (g === gettone && scrub === 'video') { scrub = 'foto'; video.hidden = true; img.hidden = false; } };
      video.ontimeupdate = () => { if (g === gettone && suona) mostraPosizione(video.currentTime); };
      video.onended = () => { if (g === gettone) pausa(); };
      video.src = fonte;
    } else {
      const a = audioEl;
      a.preload = 'auto';
      a.volume = +vol.value;
      audio = a;
      scrub = 'audio';
      const pronto = () => {
        if (g !== gettone || audio !== a) return;
        scrub = 'audio';
        if (isFinite(a.duration) && a.duration > 0 && indice) {
          indice.totale = a.duration;
          mostraPosizione(Math.min(posizione, a.duration));
        }
        if (vuoleSuonare) parti(posizione);
      };
      a.onloadedmetadata = pronto;
      a.oncanplay = pronto;
      a.onerror = () => { if (g === gettone && audio === a) { scrub = 'muto'; suona = false; vuoleSuonare = false; tempoSuono.textContent = 'non si sente'; } };
      a.ontimeupdate = () => { if (g === gettone && suona && audio === a) mostraPosizione(a.currentTime); };
      a.onended = () => { if (g === gettone && audio === a) pausa(); };
      a.src = fonte;
      if (a.readyState >= 1) pronto();
      // l'onda di tutto il brano: il cursore ci scorre sopra
      try {
        const r = await chiedi<{ url: string | null }>('scrubAnteprima', { id: f.id, lato: LATO_GRANDE });
        if (g !== gettone || !r.url) return;
        ondaPiena = true;
        img.src = r.url; img.hidden = false; segnaposto.hidden = true;
        testina.hidden = false;
        mostraPosizione(posizione);
      } catch { /* restano le onde a pezzi */ }
    }
  }

  async function pompa() {
    if (inCorso) return;
    inCorso = true;
    const g = gettone;
    while (attesa.length && g === gettone) {
      const n = attesa.shift()!;
      const u = await chiediFigura(n, LATO_FIGURINA);
      if (g !== gettone) break;
      const v = voci[n];
      if (!v) continue;
      if (u) { (v.querySelector('img') as HTMLImageElement).src = u; v.classList.add('pronta'); } else v.classList.add('manca');
    }
    inCorso = false;
  }

  function etichettaDi(n: number): string {
    if (!indice) return '';
    const p = indice.punti[n];
    if (indice.tipo === 'pagine') return String(Math.round(p));
    if (indice.tipo === 'tempo') return durata(p);
    return '';
  }

  function didascalia(n: number): string {
    if (!indice) return '';
    const p = indice.punti[n];
    if (indice.tipo === 'pagine') return `Pagina ${Math.round(p)} di ${Math.round(indice.totale)}`;
    if (indice.tipo === 'tempo') return `${durata(p)} di ${durata(indice.totale)}`;
    return '';
  }

  async function scegli(n: number) {
    if (!indice || !file) return;
    n = Math.max(0, Math.min(indice.voci - 1, n));
    scelta = n;
    voci.forEach((v, k) => { v.classList.toggle('su', k === n); v.setAttribute('aria-selected', String(k === n)); });
    voci[n]?.scrollIntoView({ block: 'nearest' });
    etichetta.textContent = didascalia(n);
    if (indice.tipo === 'tempo') {
      // video e brani: la figurina porta il cursore in quel punto (se sta suonando, la riproduzione prosegue da lì)
      if (scrub === 'video' || (scrub === 'audio' && ondaPiena)) { if (suona) { parti(indice.punti[n]); } else vaiA(indice.punti[n]); return; }
      mostraPosizione(indice.punti[n]);
      if (scrub === 'audio' || (scrub === 'muto' && ondaPiena)) return;
    }
    // intanto la figurina (se c'è), poi la figura grande
    const piccola = url.get(chiave(n, LATO_FIGURINA)) ?? (indice.tipo === 'singola' ? file.anteprima : undefined);
    if (piccola) { img.src = piccola; img.hidden = false; segnaposto.hidden = true; }
    const g = gettone;
    const u = await chiediFigura(n, LATO_GRANDE);
    if (g !== gettone || scelta !== n) return;
    if (u) { img.src = u; img.hidden = false; segnaposto.hidden = true; }
    else if (!piccola) { img.hidden = true; segnaposto.hidden = false; segnaposto.classList.add('manca'); }
  }

  strisce.addEventListener('keydown', (e) => {
    if (!indice) return;
    const passo: Record<string, number> = { ArrowDown: 1, ArrowRight: 1, ArrowUp: -1, ArrowLeft: -1, PageDown: 5, PageUp: -5 };
    if (e.key in passo) { e.preventDefault(); void scegli(scelta + passo[e.key]); }
    else if (e.key === 'Home') { e.preventDefault(); void scegli(0); }
    else if (e.key === 'End') { e.preventDefault(); void scegli(indice.voci - 1); }
    else if ((e.key === ' ' || e.key === 'Enter') && indice.tipo === 'tempo' && media()) { e.preventDefault(); alterna(); }
  });

  // ——— il cursore: l'onda e la timeline spostano il punto, senza farlo sentire. Il video si cerca dalla barra. ———
  const puoScorrere = () => !!indice && indice.tipo === 'tempo' && !!scrub && scrub !== 'muto';
  const eTempo = () => file?.categoria === 'audio' || file?.categoria === 'video';
  let tira = false;
  function sposta(t: number) {
    if (suona && media()) {
      mostraPosizione(t);
      try { media()!.currentTime = t; } catch { /* non ancora pronto */ }
    } else vaiA(t);
  }
  function focusVideo() { if (file?.categoria === 'video') el.focus({ preventScroll: true }); }
  function tastoVideo(e: KeyboardEvent) {
    if (file?.categoria !== 'video') return;
    const fps = file.dettagli?.fps && file.dettagli.fps > 1 ? file.dettagli.fps : 25;
    const k = e.key.toLowerCase();
    const passo = (d: number, continua: boolean) => {
      e.preventDefault();
      e.stopPropagation();
      const t = posizione + d;
      if (continua && suona) parti(t);
      else { if (!continua) pausa(); vaiA(t); }
    };
    if (e.key === 'ArrowLeft') passo(-1 / fps, false);
    else if (e.key === 'ArrowRight') passo(1 / fps, false);
    else if (k === 'j') passo(-10, true);
    else if (k === 'l') passo(10, true);
    else if (k === 'k' || e.key === ' ') { e.preventDefault(); e.stopPropagation(); alterna(); }
  }
  async function salvaQuadro() {
    if (!file || file.categoria !== 'video') return;
    try {
      if (video.videoWidth > 0) {
        const cv = document.createElement('canvas');
        cv.width = video.videoWidth;
        cv.height = video.videoHeight;
        cv.getContext('2d')!.drawImage(video, 0, 0);
        const r = await chiedi<{ nome: string }>('salvaPng', { png: cv.toDataURL('image/png') });
        tasti.textContent = `Salvato: ${r.nome}`;
        return;
      }
    } catch { /* il fotogramma lo prende FFmpeg */ }
    try {
      const r = await chiedi<{ nome: string }>('salvaQuadro', { id: file.id, t: posizione });
      tasti.textContent = `Salvato: ${r.nome}`;
    } catch (e) { tasti.textContent = (e as Error).message; }
  }
  figura.addEventListener('pointermove', (e) => { if (scrub === 'audio' || file?.categoria === 'video' || !puoScorrere() || suona) return; vaiA(tempoDa(e, figura)); });
  figura.addEventListener('pointerup', (e) => {
    if (file?.categoria !== 'audio' || e.button !== 0) return;
    sposta(tempoDa(e, figura));
  });
  barra.addEventListener('pointerdown', (e) => {
    if (!eTempo() || e.button !== 0) return;
    tira = true;
    barra.setPointerCapture(e.pointerId);
    focusVideo();
    sposta(tempoDa(e, barra));
  });
  barra.addEventListener('pointermove', (e) => {
    if (eTempo()) { if (tira) sposta(tempoDa(e, barra)); return; }
    if (puoScorrere() && !suona) vaiA(tempoDa(e, barra));
  });
  barra.addEventListener('pointerup', () => { tira = false; });
  figura.addEventListener('pointerleave', () => { if (!suona && scrub !== 'audio') audio?.pause(); });
  barra.addEventListener('pointerleave', () => { if (!suona && scrub !== 'audio') audio?.pause(); });
  figura.addEventListener('click', (e) => {
    // l'onda del brano ha già messo il punto (pointerup): il play sta sotto. Il video: clic = play/pausa, la barra cerca.
    if (file?.categoria === 'audio') return;
    if (file?.categoria === 'video') { focusVideo(); alterna(); return; }
    if (indice?.tipo === 'tempo' && media()) { if (suona) pausa(); else parti(tempoDa(e, figura)); }
    else apri();
  });
  figura.addEventListener('dblclick', () => { if (indice?.tipo === 'tempo' && (media() || file?.categoria === 'audio')) { pausa(); apri(); } });
  barra.addEventListener('click', (e) => {
    if (eTempo()) return;
    if (puoScorrere() && media()) parti(tempoDa(e, barra));
  });
  barra.addEventListener('keydown', (e) => {
    if (file?.categoria === 'video') { tastoVideo(e); return; }
    if (!puoScorrere()) return;
    const passo = e.shiftKey ? 30 : SALTO;
    const vai = (t: number) => { e.preventDefault(); if (suona) parti(t); else vaiA(t); };
    if (e.key === 'ArrowRight') vai(posizione + passo);
    else if (e.key === 'ArrowLeft') vai(posizione - passo);
    else if (e.key === 'Home') vai(0);
    else if (e.key === 'End') vai(durataTot());
    else if ((e.key === ' ' || e.key === 'Enter') && media()) { e.preventDefault(); alterna(); }
  });
  el.addEventListener('keydown', (e) => {
    if (e.target === barra || e.target instanceof HTMLInputElement || e.target instanceof HTMLButtonElement) return;
    tastoVideo(e);
  });

  function costruisci(i: Indice) {
    indice = i;
    tipoDescritto = i.tipo === 'pagine' ? `${Math.round(i.totale)} ${i.totale === 1 ? 'pagina' : 'pagine'}` : i.tipo === 'tempo' ? `${i.voci} punti nel tempo` : '';
    strisce.replaceChildren();
    voci.length = 0;
    attesa.length = 0;
    osserva?.disconnect();
    osserva = new IntersectionObserver((es) => {
      for (const e of es) {
        if (!e.isIntersecting) continue;
        const n = Number((e.target as HTMLElement).dataset.n);
        osserva!.unobserve(e.target);
        if (!url.has(chiave(n, LATO_FIGURINA))) attesa.push(n);
        else { (e.target.querySelector('img') as HTMLImageElement).src = url.get(chiave(n, LATO_FIGURINA))!; e.target.classList.add('pronta'); }
      }
      // quelle in vista per prime: si chiede dalla più vicina alla scelta
      attesa.sort((a, b) => Math.abs(a - scelta) - Math.abs(b - scelta));
      void pompa();
    }, { root: strisce, rootMargin: '240px 0px' });
    for (let n = 0; n < i.voci; n++) {
      const v = h('button.cl-voce', { role: 'option', 'aria-label': didascalia(n) || 'Anteprima', dataset: { n: String(n) }, tabindex: '-1', onclick: () => { void scegli(n); strisce.focus({ preventScroll: true }); } },
        h('div.cl-mini', null, h('img', { alt: '', draggable: 'false' })),
        etichettaDi(n) ? h('span.cl-n', null, etichettaDi(n)) : null);
      voci.push(v);
      strisce.append(v);
      osserva.observe(v);
    }
    strisce.classList.toggle('sola', i.voci <= 1);
  }

  return {
    el,
    descrizione: () => tipoDescritto,
    ferma() { gettone++; attesa.length = 0; osserva?.disconnect(); spegniMedia(); file = null; indice = null; },
    mostra(f) {
      gettone++;
      spegniMedia();
      attesa.length = 0;
      inCorso = false;
      file = f;
      el.classList.toggle('audio', f.categoria === 'audio');
      el.classList.toggle('video', f.categoria === 'video');
      figura.title = f.categoria === 'audio'
        ? 'Clic sull\'onda: sposta il punto, senza farlo sentire. Poi Play.'
        : f.categoria === 'video'
          ? 'Clic: play o pausa. La barra sotto cerca il punto. ← → un fotogramma, J K L.'
          : 'Passa il cursore: scorre. Clic: riproduce. Doppio clic: apre nel lettore';
      bPlay.innerHTML = icone.play;
      tempoSuono.textContent = '—';
      tasti.textContent = f.categoria === 'video' ? '← → fotogramma · J K L' : '';
      indice = null;
      scelta = -1;
      tipoDescritto = '';
      strisce.replaceChildren();
      voci.length = 0;
      etichetta.textContent = '';
      // finché non arriva niente: la figurina della coda o l'icona della categoria
      segnaposto.classList.remove('manca');
      segnaposto.style.setProperty('--tinta', coloreCategoria[f.categoria] ?? 'var(--oro)');
      segnaposto.innerHTML = iconaCategoria[f.categoria] ?? '';
      if (f.anteprima) { img.src = f.anteprima; img.hidden = false; segnaposto.hidden = true; } else { img.hidden = true; segnaposto.hidden = false; }
      const g = gettone;
      chiedi<Indice>('indiceAnteprima', { id: f.id })
        .then((i) => {
          if (g !== gettone) return;
          // un brano non ha i pezzi nel tempo: si ascolta da qui, con play, posizione e volume
          if (f.categoria === 'audio' || f.categoria === 'video') {
            // anche se la durata letta è zero: il tag, quando è pronto, dice quanto dura davvero
            indice = i.tipo === 'tempo' ? i : { tipo: 'tempo', voci: 1, totale: f.dettagli?.durata || 0, punti: [0] };
            tipoDescritto = f.categoria === 'audio' ? 'si ascolta da qui' : 'si guarda da qui';
            strisce.replaceChildren();
            void preparaScrub(f, g);
            pronto();
            return;
          }
          costruisci(i);
          if (i.tipo === 'tempo') void preparaScrub(f, g);
          void scegli(0);
          pronto();
        })
        .catch(() => {
          if (g !== gettone) return;
          if (f.categoria === 'audio' || f.categoria === 'video') {
            indice = { tipo: 'tempo', voci: 1, totale: f.dettagli?.durata || 0, punti: [0] };
            void preparaScrub(f, g);
            pronto();
            return;
          }
          costruisci({ tipo: 'singola', voci: 1, totale: 1, punti: [0] });
          void scegli(0);
          pronto();
        });
    },
  };
}
