// La grafica che si muove: lo strumento a lancetta (qualità, CPU, encoder) e la piastra con le bobine.
// Tutto su canvas, ridisegnato solo quando qualcosa si muove.

const DPR = () => Math.max(1, window.devicePixelRatio || 1);

function prepara(c: HTMLCanvasElement) {
  const r = c.getBoundingClientRect();
  const d = DPR();
  const w = Math.max(1, Math.round(r.width * d));
  const hh = Math.max(1, Math.round(r.height * d));
  if (c.width !== w || c.height !== hh) { c.width = w; c.height = hh; }
  const g = c.getContext('2d')!;
  g.setTransform(d, 0, 0, d, 0, 0);
  return { g, w: r.width, h: r.height };
}

export interface Tacca { v: number; testo?: string }

/**
 * Lo strumento a lancetta, quadrante crema come i VU di DaProd Video. La lancetta ha una molla:
 * arriva, supera un pelo e torna, come quelle vere.
 */
export class Lancetta {
  private pos = 0;
  private vel = 0;
  private obiettivo = 0;
  private corre = false;
  private picco = 0;
  private piccoT = 0;

  constructor(
    private c: HTMLCanvasElement,
    private o: { etichetta: string; tacche: Tacca[]; rossoDa?: number; rossoA?: number; sotto?: string; picco?: boolean },
  ) {
    new ResizeObserver(() => this.disegna()).observe(c);
    this.disegna();
  }

  valore(v: number) {
    this.obiettivo = Math.min(1.04, Math.max(-0.02, v));
    if (this.o.picco && v >= this.picco) { this.picco = v; this.piccoT = performance.now(); }
    if (!this.corre) { this.corre = true; requestAnimationFrame((t) => this.passo(t)); }
  }

  testoSotto(s: string) { this.o.sotto = s; this.disegna(); }

  private ultimo = 0;
  private passo(t: number) {
    const dt = Math.min(0.05, this.ultimo ? (t - this.ultimo) / 1000 : 0.016);
    this.ultimo = t;
    // molla smorzata: rigida quanto basta per sembrare un VU, non un tergicristallo
    const k = 90, smorza = 13;
    this.vel += ((this.obiettivo - this.pos) * k - this.vel * smorza) * dt;
    this.pos += this.vel * dt;
    if (this.o.picco && t - this.piccoT > 1200) this.picco = Math.max(this.obiettivo, this.picco - dt * 0.5);
    this.disegna();
    if (Math.abs(this.vel) > 0.0005 || Math.abs(this.obiettivo - this.pos) > 0.0005) requestAnimationFrame((t2) => this.passo(t2));
    else { this.corre = false; this.ultimo = 0; }
  }

  disegna() {
    const { g, w, h } = prepara(this.c);
    g.clearRect(0, 0, w, h);
    // quadrante
    const r0 = 6;
    const fondo = g.createLinearGradient(0, 0, 0, h);
    fondo.addColorStop(0, '#fbf3dc');
    fondo.addColorStop(1, '#e4d3a6');
    g.fillStyle = fondo;
    g.beginPath(); g.roundRect(0, 0, w, h, r0); g.fill();

    const cx = w / 2, cy = h * 1.08, R = h * 0.86;
    const a0 = -Math.PI / 2 - 0.82, a1 = -Math.PI / 2 + 0.82;
    const ang = (v: number) => a0 + (a1 - a0) * v;

    // zona rossa
    if (this.o.rossoDa != null && this.o.rossoA != null) {
      g.strokeStyle = '#d8323f';
      g.lineWidth = h * 0.06;
      g.beginPath(); g.arc(cx, cy, R * 0.93, ang(this.o.rossoDa), ang(this.o.rossoA)); g.stroke();
    }
    // arco e tacche
    g.strokeStyle = '#2b2118';
    g.lineWidth = 1.2;
    g.beginPath(); g.arc(cx, cy, R * 0.86, a0, a1); g.stroke();
    g.fillStyle = '#2b2118';
    g.textAlign = 'center';
    g.textBaseline = 'middle';
    g.font = `700 ${Math.max(7, h * 0.085)}px Rajdhani, sans-serif`;
    for (let i = 0; i <= 20; i++) {
      const v = i / 20;
      const a = ang(v);
      const lungo = i % 5 === 0;
      const r1 = R * 0.86, r2 = R * (lungo ? 0.97 : 0.92);
      g.lineWidth = lungo ? 1.6 : 1;
      g.beginPath(); g.moveTo(cx + Math.cos(a) * r1, cy + Math.sin(a) * r1); g.lineTo(cx + Math.cos(a) * r2, cy + Math.sin(a) * r2); g.stroke();
    }
    for (const t of this.o.tacche) {
      if (!t.testo) continue;
      const a = ang(t.v);
      g.save();
      g.translate(cx + Math.cos(a) * R * 1.07, cy + Math.sin(a) * R * 1.07);
      g.rotate(a + Math.PI / 2);
      g.fillText(t.testo, 0, 0);
      g.restore();
    }
    // etichetta e testo sotto
    g.font = `700 ${Math.max(8, h * 0.11)}px Orbitron, sans-serif`;
    g.fillStyle = '#3a2d20';
    g.fillText(this.o.etichetta, cx, h * 0.6);
    if (this.o.sotto) {
      g.font = `700 ${Math.max(8, h * 0.1)}px Rajdhani, sans-serif`;
      g.fillStyle = '#6b5a44';
      g.fillText(this.o.sotto, cx, h * 0.76);
    }
    // picco
    if (this.o.picco) {
      const a = ang(Math.min(1, this.picco));
      g.fillStyle = 'rgba(216,50,63,.75)';
      g.beginPath(); g.arc(cx + Math.cos(a) * R * 0.8, cy + Math.sin(a) * R * 0.8, 2, 0, Math.PI * 2); g.fill();
    }
    // lancetta
    const a = ang(this.pos);
    g.strokeStyle = '#1b130d';
    g.lineWidth = 1.8;
    g.beginPath(); g.moveTo(cx, cy); g.lineTo(cx + Math.cos(a) * R * 0.98, cy + Math.sin(a) * R * 0.98); g.stroke();
    g.strokeStyle = '#d8323f';
    g.lineWidth = 1.4;
    g.beginPath(); g.moveTo(cx + Math.cos(a) * R * 0.8, cy + Math.sin(a) * R * 0.8); g.lineTo(cx + Math.cos(a) * R * 0.98, cy + Math.sin(a) * R * 0.98); g.stroke();
    // vetro
    const vetro = g.createLinearGradient(0, 0, w, h);
    vetro.addColorStop(0, 'rgba(255,255,255,.38)');
    vetro.addColorStop(0.45, 'rgba(255,255,255,0)');
    vetro.addColorStop(1, 'rgba(0,0,0,.08)');
    g.fillStyle = vetro;
    g.beginPath(); g.roundRect(0, 0, w, h, r0); g.fill();
    g.strokeStyle = 'rgba(0,0,0,.55)';
    g.lineWidth = 1;
    g.beginPath(); g.roundRect(0.5, 0.5, w - 1, h - 1, r0); g.stroke();
  }
}

/**
 * La piastra: a sinistra la bobina dell'originale che si svuota, a destra quella del convertito che si riempie,
 * il nastro passa sotto la testina. Girano veloci quanto la conversione (velocità ×).
 */
export class Bobine {
  private angolo = 0;
  private frazione = 0;
  private mostrata = 0;
  private velocita = 0;
  private attiva = false;
  private corre = false;
  private ultimo = 0;
  private finito = false;

  constructor(private c: HTMLCanvasElement, private colore = '#ff3df2') {
    new ResizeObserver(() => this.disegna()).observe(c);
    this.disegna();
  }

  imposta(frazione: number, velocita: number | null, attiva: boolean, finito = false) {
    this.frazione = Math.max(0, Math.min(1, frazione));
    this.velocita = velocita ?? (attiva ? 1 : 0);
    this.attiva = attiva;
    this.finito = finito;
    if (!this.corre) { this.corre = true; requestAnimationFrame((t) => this.passo(t)); }
  }

  ferma() { this.attiva = false; }

  private passo(t: number) {
    const dt = Math.min(0.05, this.ultimo ? (t - this.ultimo) / 1000 : 0.016);
    this.ultimo = t;
    const giri = this.attiva ? 0.6 + Math.min(6, Math.log2(1 + this.velocita)) * 0.9 : 0;
    this.angolo += giri * dt * Math.PI * 2;
    this.mostrata += (this.frazione - this.mostrata) * Math.min(1, dt * 6);
    this.disegna();
    if (this.attiva || Math.abs(this.frazione - this.mostrata) > 0.001) requestAnimationFrame((t2) => this.passo(t2));
    else { this.corre = false; this.ultimo = 0; }
  }

  disegna() {
    const { g, w, h } = prepara(this.c);
    g.clearRect(0, 0, w, h);
    const p = this.mostrata;
    const R = Math.min(h * 0.42, w * 0.2);
    const rMin = R * 0.36;
    const sx = w * 0.25, dx = w * 0.75, cy = h * 0.46;
    const rSin = rMin + (R - rMin) * Math.sqrt(1 - p);
    const rDes = rMin + (R - rMin) * Math.sqrt(p);

    // nastro: dalle bobine ai rulli, sotto la testina
    const yN = h * 0.9;
    g.strokeStyle = '#3b2a20';
    g.lineWidth = 2;
    g.beginPath();
    g.moveTo(sx - rSin * 0.2, cy + rSin);
    g.lineTo(w * 0.36, yN);
    g.lineTo(w * 0.64, yN);
    g.lineTo(dx + rDes * 0.2, cy + rDes);
    g.stroke();
    for (const x of [w * 0.36, w * 0.64]) {
      g.fillStyle = '#9b97a8';
      g.beginPath(); g.arc(x, yN, 3.5, 0, Math.PI * 2); g.fill();
    }
    // testina
    const tg = g.createLinearGradient(0, yN - 12, 0, yN + 2);
    tg.addColorStop(0, '#d9d6e2');
    tg.addColorStop(1, '#6f6b7d');
    g.fillStyle = tg;
    g.beginPath(); g.roundRect(w / 2 - 14, yN - 13, 28, 12, 3); g.fill();
    if (this.attiva || this.finito) {
      g.fillStyle = this.finito ? '#5dffb4' : this.colore;
      g.shadowColor = g.fillStyle as string;
      g.shadowBlur = 10;
      g.beginPath(); g.arc(w / 2, yN - 7, 2.6, 0, Math.PI * 2); g.fill();
      g.shadowBlur = 0;
    }

    const bobina = (x: number, raggio: number, verso: number, tinta: string) => {
      // flangia
      g.fillStyle = 'rgba(255,255,255,.04)';
      g.strokeStyle = 'rgba(255,255,255,.12)';
      g.lineWidth = 1;
      g.beginPath(); g.arc(x, cy, R * 1.04, 0, Math.PI * 2); g.fill(); g.stroke();
      // nastro avvolto
      const ng = g.createRadialGradient(x, cy, rMin * 0.9, x, cy, raggio);
      ng.addColorStop(0, '#120c09');
      ng.addColorStop(0.7, '#2c1d14');
      ng.addColorStop(1, '#4a3123');
      g.fillStyle = ng;
      g.beginPath(); g.arc(x, cy, raggio, 0, Math.PI * 2); g.fill();
      // riflesso sul nastro, che gira
      g.strokeStyle = tinta;
      g.globalAlpha = 0.18;
      g.lineWidth = Math.max(1, raggio - rMin);
      g.beginPath(); g.arc(x, cy, (raggio + rMin) / 2, this.angolo * verso, this.angolo * verso + 0.9); g.stroke();
      g.globalAlpha = 1;
      // mozzo con tre razze
      g.fillStyle = '#d9d6e2';
      g.beginPath(); g.arc(x, cy, rMin * 0.95, 0, Math.PI * 2); g.fill();
      g.fillStyle = '#1c1b23';
      for (let i = 0; i < 3; i++) {
        const a = this.angolo * verso + (i * Math.PI * 2) / 3;
        g.beginPath();
        g.moveTo(x + Math.cos(a - 0.38) * rMin * 0.32, cy + Math.sin(a - 0.38) * rMin * 0.32);
        g.arc(x, cy, rMin * 0.8, a - 0.38, a + 0.38);
        g.closePath();
        g.fill();
      }
      g.fillStyle = '#2a2932';
      g.beginPath(); g.arc(x, cy, rMin * 0.24, 0, Math.PI * 2); g.fill();
    };
    bobina(sx, rSin, 1, '#ffd54a');
    bobina(dx, rDes, 1, this.colore);

    if (w > 240) {
      g.font = '700 9px Orbitron, sans-serif';
      g.textAlign = 'center';
      g.fillStyle = 'rgba(235,231,244,.45)';
      g.fillText('ORIGINALE', sx, h - 3);
      g.fillText('CONVERTITO', dx, h - 3);
    }
  }
}
