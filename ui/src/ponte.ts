// Il ponte con l'app: dentro WebView2 i messaggi vanno a C# (Ponte.cs), nel browser a un finto motore
// (finto.ts) che serve per provare l'interfaccia senza Windows.

export type Categoria =
  | 'video' | 'audio' | 'immagine' | 'pdf' | 'documento' | 'foglio' | 'presentazione'
  | 'testo' | 'dati' | 'sottotitoli' | 'archivio' | 'cartella' | 'altro';

export interface Formato {
  id: string;
  categoria: Categoria;
  etichetta: string;
  descrizione: string;
  estensione: string;
  unisce: boolean;
  manca: string | null;
}

export interface InfoFile {
  id: string;
  percorso: string;
  nome: string;
  cartella: string;
  estensione: string;
  categoria: Categoria;
  nomeCategoria: string;
  peso: number;
  eDirectory: boolean;
  formati: string[];
  dettagli?: Dettagli;
  anteprima?: string;
}

export interface Dettagli {
  durata?: number;
  larghezza?: number;
  altezza?: number;
  fps?: number;
  codec?: string;
  hdr?: boolean;
  bitrate?: number;
  audio?: string;
  canali?: number;
  tracceAudio?: number;
  sottotitoli?: number;
  pagine?: number | null;
  file?: number;
  peso?: number;
  errore?: string;
}

export interface Gpu { nome: string; marca: string; vram: number; driver: string }

export interface Hardware {
  cpu: string;
  thread: number;
  gpu: Gpu[];
  encoder: string[];
  acceleratore: string | null;
}

export interface Impostazioni {
  scritta: string;
  menu: boolean;
  alMassimo: boolean;
  suoni: boolean;
  apriCartella: boolean;
  formati: Record<string, string>;
  scelte?: Record<string, unknown> | null;
}

export type StatoLavoro = 'attesa' | 'corre' | 'fatto' | 'errore' | 'annullato';

export interface Lavoro {
  id: string;
  fileId: string | null;
  sorgenti: string[];
  formato: string;
  etichetta: string;
  stato: StatoLavoro;
  frazione: number;
  fase: string | null;
  velocita: number | null;
  fps: number | null;
  eta: number | null;
  uscita: string | null;
  pesoPrima: number;
  pesoDopo: number | null;
  errore: string | null;
  dettaglio: string | null;
  secondi: number;
}

export interface Piano {
  modo: 'copia' | 'peso' | 'qualita';
  codec: string;
  encoder: string;
  hardware: boolean;
  larghezza: number;
  altezza: number;
  fps: number;
  bitrateVideo: number;
  audioKbps: number;
  punteggio: number;
  giudizio: string;
  pesoStimato: number;
  pesoMin: number;
  pesoMax: number;
  puoCopiare: boolean;
  note: string[];
  codecAmmessi: string[];
}

export interface Carico { cpu: number; encoder: number; decoder: number; grafica: number }

export interface Stato {
  versione: string;
  hardware: Hardware | null;
  formati: Formato[];
  /** In ordine: un oggetto non andrebbe bene, Chromium riordina le chiavi passando da WebView2. */
  categorie: { id: Categoria; nome: string }[];
  impostazioni: Impostazioni;
  modo: 'finestra' | 'rapido';
  file: InfoFile[];
  lavori: Lavoro[];
  office: { libreOffice: boolean; word: boolean };
}

export interface OpzioniVideo {
  modo: 'auto' | 'peso' | 'qualita' | 'copia';
  pesoByte: number;
  qualita: number;
  codec: string;
  lato: number;
  motore: string;
  senzaAudio: boolean;
  audioKbps: number;
  fps: number;
  gifLarghezza: number;
  gifFps: number;
}
export interface OpzioniAudio { kbps: number; normalizza: boolean; mono: boolean; bit: number }
export interface OpzioniImmagine { qualita: number; lato: number; pesoMaxByte: number }
export interface OpzioniPdf { dpi: number }
export interface Opzioni { video: OpzioniVideo; audio: OpzioniAudio; immagine: OpzioniImmagine; pdf: OpzioniPdf }

type Ascoltatore = (dati: any) => void;

interface WebView { postMessage(m: unknown): void; addEventListener(t: 'message', f: (e: { data: any }) => void): void }
const webview: WebView | undefined = (window as any).chrome?.webview;

export const dentroApp = !!webview;

let contatore = 0;
const attese = new Map<number, { ok: (v: any) => void; no: (e: Error) => void }>();
const ascoltatori = new Map<string, Set<Ascoltatore>>();

function arriva(m: any) {
  if (m && typeof m.id === 'number' && attese.has(m.id)) {
    const a = attese.get(m.id)!;
    attese.delete(m.id);
    if (m.ok) a.ok(m.dati);
    else a.no(new Error(m.errore ?? 'errore'));
    return;
  }
  if (m && typeof m.evento === 'string') ascoltatori.get(m.evento)?.forEach((f) => f(m.dati));
}

let finto: ((cmd: string, args: any, emetti: (e: string, d: any) => void) => Promise<any>) | null = null;

if (webview) webview.addEventListener('message', (e) => arriva(e.data));

export async function avviaFinto() {
  if (webview) return;
  const m = await import('./finto');
  finto = m.rispondi;
}

/** Una domanda all'app; la risposta arriva come promessa. */
export function chiedi<T = any>(cmd: string, args: Record<string, unknown> = {}): Promise<T> {
  if (finto) return finto(cmd, args, (evento, dati) => arriva({ evento, dati }));
  if (!webview) return Promise.reject(new Error('fuori dall\'app'));
  const id = ++contatore;
  return new Promise<T>((ok, no) => {
    attese.set(id, { ok, no });
    webview.postMessage({ id, cmd, args });
  });
}

/** Le notizie che l'app manda da sola: file nuovi, avanzamento, carico della macchina… */
export function ascolta(evento: string, f: Ascoltatore) {
  if (!ascoltatori.has(evento)) ascoltatori.set(evento, new Set());
  ascoltatori.get(evento)!.add(f);
}
