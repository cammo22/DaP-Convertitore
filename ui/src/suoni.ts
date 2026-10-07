// I suoni, sintetizzati al volo (niente file): il "clac" dei tasti della piastra e il din-don della fine.

let ctx: AudioContext | null = null;
export let suoniAccesi = true;
export function accendiSuoni(si: boolean) { suoniAccesi = si; }

function ac() {
  ctx ??= new AudioContext();
  if (ctx.state === 'suspended') void ctx.resume();
  return ctx;
}

/** Il tasto meccanico: un colpetto di rumore filtrato, corto. */
export function clac(forte = false) {
  if (!suoniAccesi) return;
  const a = ac();
  const durata = 0.045;
  const buf = a.createBuffer(1, Math.ceil(a.sampleRate * durata), a.sampleRate);
  const d = buf.getChannelData(0);
  for (let i = 0; i < d.length; i++) d[i] = (Math.random() * 2 - 1) * Math.pow(1 - i / d.length, 4);
  const s = a.createBufferSource();
  s.buffer = buf;
  const f = a.createBiquadFilter();
  f.type = 'bandpass';
  f.frequency.value = forte ? 1800 : 2600;
  f.Q.value = 1.2;
  const g = a.createGain();
  g.gain.value = forte ? 0.32 : 0.16;
  s.connect(f).connect(g).connect(a.destination);
  s.start();
}

/** Fatto: due note che salgono, morbide. */
export function dinDon() {
  if (!suoniAccesi) return;
  const a = ac();
  const t = a.currentTime;
  [[784, 0], [1175, 0.13]].forEach(([fr, dt]) => {
    const o = a.createOscillator();
    o.type = 'sine';
    o.frequency.value = fr;
    const g = a.createGain();
    g.gain.setValueAtTime(0, t + dt);
    g.gain.linearRampToValueAtTime(0.18, t + dt + 0.015);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dt + 0.9);
    o.connect(g).connect(a.destination);
    o.start(t + dt);
    o.stop(t + dt + 1);
  });
}

/** Errore: una nota bassa che scende. */
export function bu() {
  if (!suoniAccesi) return;
  const a = ac();
  const t = a.currentTime;
  const o = a.createOscillator();
  o.type = 'triangle';
  o.frequency.setValueAtTime(330, t);
  o.frequency.exponentialRampToValueAtTime(160, t + 0.35);
  const g = a.createGain();
  g.gain.setValueAtTime(0.16, t);
  g.gain.exponentialRampToValueAtTime(0.0001, t + 0.45);
  o.connect(g).connect(a.destination);
  o.start(t);
  o.stop(t + 0.5);
}
