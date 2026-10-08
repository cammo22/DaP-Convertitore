// Video e musica: cosa WebView2 suona da sé, e la fila per quello che FFmpeg traduce al volo.
import { chiedi } from '../ponte';
import { evento } from './comune';

export interface InfoMedia {
  durata: number;
  contenitore: string;
  bitrate: number;
  video: { codec: string; larghezza: number; altezza: number; fps: number; hdr: boolean; bit: number; pixFmt: string } | null;
  audio: { indice: number; lingua: string | null; titolo: string | null; codec: string; canali: number; campionamento: number; bitrate: number }[];
  sottotitoli: { indice: number; lingua: string | null; titolo: string | null; codec: string; testo: boolean }[];
  esterni: { nome: string; percorso: string }[];
  tag: Record<string, string>;
  capitoli: { inizio: number; titolo: string }[];
  copertina: string | null;
}

const audioOk = ['aac', 'mp3', 'opus', 'vorbis', 'flac', 'pcm_s16le', 'pcm_s24le', 'pcm_s32le', 'pcm_f32le', 'pcm_u8', 'pcm_s16be'];

function hevcOk() {
  try { return MediaSource.isTypeSupported('video/mp4; codecs="hvc1.1.6.L120.90"') || MediaSource.isTypeSupported('video/mp4; codecs="hev1.1.6.L120.90"'); } catch { return false; }
}

/** WebView2 lo suona da sé? Se no, lo traduce FFmpeg (e se si sbaglia, ci pensa l'errore del tag a cambiare strada). */
export function nativo(i: InfoMedia, traccia = 0): boolean {
  const c = i.contenitore;
  const scatola = /mov|mp4|matroska|webm|ogg|^mp3$|^flac$|^wav$|^aac$/.test(c);
  if (!scatola) return false;
  if (traccia > 0) return false; // le altre tracce audio WebView2 non le sa scegliere
  const v = i.video;
  if (v) {
    const ok = ['h264', 'vp8', 'vp9', 'av1'].includes(v.codec) || (v.codec === 'hevc' && hevcOk());
    if (!ok) return false;
    if (v.codec === 'h264' && /10|12|422|444/.test(v.pixFmt)) return false; // H.264 a 10 bit: la scheda non lo decodifica
  }
  const a = i.audio[0];
  return !a || audioOk.includes(a.codec);
}

/** Il video si può copiare nel flusso così com'è (si rifà solo l'audio): H.264 a 8 bit. */
export function copiabile(i: InfoMedia) {
  const v = i.video;
  return !!v && v.codec === 'h264' && !/10|12|422|444/.test(v.pixFmt);
}

const dorme = (ms: number) => new Promise((ok) => setTimeout(ok, ms));

/**
 * La fila: FFmpeg traduce da un punto in poi, la pagina chiede i pezzi e li mette nel Media Source. Scorrere
 * dentro quello che c'è già è immediato; scorrere più in là fa ripartire FFmpeg da lì.
 */
export class Fila {
  private gen = 0;
  private url: string | null = null;
  attiva = false;

  constructor(private v: HTMLMediaElement, private info: InfoMedia, private opz: { traccia: number; copia: boolean }, private avvisa: (t: string | null) => void) {}

  async parti(da: number) {
    const g = ++this.gen;
    this.attiva = true;
    if (this.url) URL.revokeObjectURL(this.url);
    const ms = new MediaSource();
    this.url = URL.createObjectURL(ms);
    const giocava = !this.v.paused || da === 0;
    this.v.src = this.url;
    await evento(ms, 'sourceopen');
    if (g !== this.gen) return;
    this.avvisa('Traduco al volo con FFmpeg…');
    const r = await chiedi<{ url: string; mime: string; da: number }>('flusso', { da, traccia: this.opz.traccia, copia: this.opz.copia });
    if (g !== this.gen) return;
    if (this.info.durata > 0) ms.duration = this.info.durata;
    const sb = ms.addSourceBuffer(r.mime);
    // FFmpeg parte da zero: è il Media Source a spostare tutto al punto giusto del film
    sb.timestampOffset = r.da;
    let n = 0;
    let primo = true;
    try {
      while (g === this.gen) {
        while (g === this.gen && this.avanti() > 40) await dorme(350);
        if (g !== this.gen) break;
        const resp = await fetch(r.url + n++);
        if (g !== this.gen) break;
        if (resp.status === 204) {
          await this.libero(sb);
          if (ms.readyState === 'open') ms.endOfStream();
          break;
        }
        if (!resp.ok) throw new Error(`il flusso si è fermato (${resp.status})`);
        const buf = await resp.arrayBuffer();
        if (g !== this.gen) break;
        await this.appendi(sb, buf);
        if (primo && sb.buffered.length) {
          primo = false;
          this.avvisa(null);
          this.v.currentTime = Math.max(da, sb.buffered.start(0));
          if (giocava) void this.v.play().catch(() => {});
        }
        // dietro non serve tenere tutto: un film intero non sta in memoria
        if (n % 24 === 0 && this.v.currentTime > 90) await this.togli(sb, 0, this.v.currentTime - 60);
      }
    } catch (e) {
      if (g === this.gen) this.avvisa('Il video si è fermato: ' + (e as Error).message);
    }
  }

  /** Quanti secondi pronti ci sono davanti a dove sei. */
  private avanti() {
    const b = this.v.buffered;
    for (let i = 0; i < b.length; i++) if (b.start(i) <= this.v.currentTime + 0.5 && b.end(i) >= this.v.currentTime) return b.end(i) - this.v.currentTime;
    return 0;
  }

  private async libero(sb: SourceBuffer) { while (sb.updating) await evento(sb, 'updateend'); }

  private async appendi(sb: SourceBuffer, buf: ArrayBuffer) {
    await this.libero(sb);
    try {
      sb.appendBuffer(buf);
    } catch (e) {
      if ((e as DOMException).name !== 'QuotaExceededError') throw e;
      await this.togli(sb, 0, Math.max(0, this.v.currentTime - 10));
      await this.libero(sb);
      sb.appendBuffer(buf);
    }
    await this.libero(sb);
  }

  private async togli(sb: SourceBuffer, da: number, a: number) {
    if (a <= da) return;
    await this.libero(sb);
    try { sb.remove(da, a); } catch { return; }
    await this.libero(sb);
  }

  /** Scorre: dentro il già pronto subito, se no FFmpeg riparte da lì. */
  vai(t: number) {
    const b = this.v.buffered;
    for (let i = 0; i < b.length; i++) if (t >= b.start(i) && t < b.end(i) - 0.3) { this.v.currentTime = t; return; }
    void this.parti(t);
  }

  cambiaTraccia(traccia: number) {
    this.opz.traccia = traccia;
    void this.parti(this.v.currentTime);
  }

  ferma() {
    this.gen++;
    this.attiva = false;
    if (this.url) URL.revokeObjectURL(this.url);
    this.url = null;
    void chiedi('fermaFlusso').catch(() => {});
  }
}
