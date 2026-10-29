// Lo stato della piastra e le domande che si fanno sempre sullo stato. Una cosa sola, condivisa da tutti i pezzi.
import type { Carico, Categoria, Formato, InfoFile, Lavoro, Opzioni, Piano, StimaImmagine, Stato } from '../ponte';

export const opzioniDiPartenza = (): Opzioni => ({
  video: { modo: 'peso', pesoByte: 0, qualita: 70, codec: 'h264', lato: 0, motore: 'auto', senzaAudio: false, audioKbps: 0, fps: 0, gifLarghezza: 480, gifFps: 15 },
  audio: { kbps: 0, normalizza: false, mono: false, bit: 16 },
  immagine: { modo: 'peso', qualita: 88, lato: 0, pesoMaxByte: 0 },
  pdf: { dpi: 200 },
  archivio: { livello: 0 },
});

/** Lo stato: una cosa sola, la leggono e la cambiano tutti i pezzi. */
export const s = {
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
  /** La stima del peso della foto scelta (quanto pesa, a che qualità ci si arriva), con la chiave che dice per quale foto e formato. */
  stimaFoto: null as (StimaImmagine & { chiave: string }) | null,
  carico: { cpu: 0, encoder: 0, decoder: 0, grafica: 0 } as Carico,
  giro: [] as string[],
  impostazioniAperte: false,
  aggiornamento: null as string | null,
  /** La scelta fatta dall'avatar (formato e preset), da applicare appena i file sono arrivati. */
  sceltaRichiesta: null as { formato: string; preset?: string | null } | null,
};

export const nomeCat = (c: string) => s.stato.categorie.find((x) => x.id === c)?.nome ?? c;
export const formato = (id: string) => s.stato.formati.find((f) => f.id === id);
export const fileDi = (c: Categoria) => s.ordine.map((i) => s.file.get(i)!).filter((f) => f && f.categoria === c);
export const categoriePresenti = () => {
  const ordine = s.stato.categorie.map((c) => c.id as string);
  const cc = [...new Set(s.ordine.map((i) => s.file.get(i)?.categoria).filter(Boolean))] as Categoria[];
  return cc.sort((a, b) => ordine.indexOf(a) - ordine.indexOf(b));
};
export const sceltaPer = (c: Categoria): Formato | undefined => {
  const disponibili = s.stato.formati.filter((f) => f.categoria === c && fileDi(c).some((x) => x.formati.includes(f.id)));
  const id = s.scelta[c] ?? s.stato.impostazioni.formati[c];
  return disponibili.find((f) => f.id === id && !f.manca) ?? disponibili.find((f) => !f.manca) ?? disponibili[0];
};
export const eVideoVero = (f?: Formato) => !!f && f.categoria === 'video' && ['.mp4', '.mkv', '.webm', '.mov'].includes(f.estensione);

/**
 * I pezzi che disegnano si chiamano fra loro (il pannello delle regolazioni ridisegna la piastra, le impostazioni
 * ridisegnano il piede…). Per non far girare gli import in tondo parlano tramite questo: main.ts li collega all'avvio.
 */
export const ui = {
  tutto: () => {},
  scegli: () => {},
  piede: () => {},
  coda: () => {},
  fatto: () => {},
  rapido: () => {},
  impostazioni: () => {},
  nuovoGiro: () => {},
};
