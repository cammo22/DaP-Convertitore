// Il finto motore: risponde come l'app vera, con file d'esempio e conversioni simulate.
// Serve a provare l'interfaccia nel browser (npm run dev). Dentro l'app non si carica mai.
import type { Formato, InfoFile, Lavoro, Piano, Stato } from './ponte';

const f = (id: string, etichetta: string, descrizione: string, estensione: string, unisce = false, manca: string | null = null): Formato =>
  ({ id, categoria: ({ video: 'video', audio: 'audio', img: 'immagine', pdf: 'pdf', doc: 'documento', arch: 'archivio' } as any)[id.split('.')[0]], etichetta, descrizione, estensione, unisce, sostituisce: !unisce && !['video.mp3', 'video.wav', 'video.srt', 'video.gif', 'img.txt', 'pdf.jpg', 'pdf.png', 'pdf.txt', 'arch.estrai'].includes(id), manca });

const formati: Formato[] = [
  f('video.mp4', 'MP4', 'va ovunque', '.mp4'), f('video.mkv', 'MKV', 'tiene tutte le tracce', '.mkv'), f('video.webm', 'WEBM', 'per il web', '.webm'),
  f('video.mov', 'MOV', 'Apple e montaggio', '.mov'), f('video.gif', 'GIF', 'animata, per le chat', '.gif'), f('video.mp3', 'MP3', "solo l'audio", '.mp3'),
  f('video.wav', 'WAV', "l'audio, senza perdite", '.wav'), f('video.srt', 'SRT', 'i sottotitoli che ha dentro', '.srt'),
  f('audio.mp3', 'MP3', 'va ovunque', '.mp3'), f('audio.m4a', 'M4A', 'AAC, iPhone e Mac', '.m4a'), f('audio.opus', 'OPUS', 'leggerissimo', '.opus'),
  f('audio.flac', 'FLAC', 'senza perdite, compresso', '.flac'), f('audio.wav', 'WAV', 'senza perdite', '.wav'),
  f('img.jpg', 'JPG', 'va ovunque', '.jpg'), f('img.png', 'PNG', 'senza perdite, con trasparenza', '.png'), f('img.webp', 'WEBP', 'leggera, per il web', '.webp'),
  f('img.avif', 'AVIF', 'la più leggera', '.avif'), f('img.jxl', 'JXL', 'JPEG XL, il futuro', '.jxl'), f('img.heic', 'HEIC', "come l'iPhone", '.heic'),
  f('img.tiff', 'TIFF', 'per la stampa', '.tif'), f('img.ico', 'ICO', 'icona di Windows', '.ico'), f('img.pdf', 'PDF', 'un PDF solo con tutte', '.pdf', true),
  f('img.txt', 'TXT', "il testo che c'è scritto (OCR)", '.txt'),
  f('pdf.jpg', 'JPG', 'una foto per pagina', ''), f('pdf.png', 'PNG', 'una immagine per pagina', ''), f('pdf.txt', 'TXT', 'il testo (anche delle scansioni)', '.txt'),
  f('pdf.docx', 'DOCX', 'da modificare in Word', '.docx'), f('pdf.unisci', 'UNISCI', 'tutti in un PDF solo', '.pdf', true),
  f('doc.pdf', 'PDF', 'da mandare e stampare', '.pdf'), f('doc.docx', 'DOCX', 'Word', '.docx'), f('doc.odt', 'ODT', 'LibreOffice', '.odt'),
  f('arch.estrai', 'ESTRAI', 'in una cartella qui', ''), f('arch.zip', 'ZIP', 'lo apre chiunque', '.zip'), f('arch.7z', '7Z', 'il più piccolo', '.7z'),
];

const quadro = (a: string, b: string, testo: string) => {
  const c = document.createElement('canvas');
  c.width = 320; c.height = 180;
  const g = c.getContext('2d')!;
  const gr = g.createLinearGradient(0, 0, 320, 180);
  gr.addColorStop(0, a); gr.addColorStop(1, b);
  g.fillStyle = gr; g.fillRect(0, 0, 320, 180);
  g.fillStyle = 'rgba(255,255,255,.85)'; g.font = '700 26px Orbitron, sans-serif'; g.textAlign = 'center';
  g.fillText(testo, 160, 100);
  return c.toDataURL('image/jpeg', 0.8);
};

const esempi: InfoFile[] = [
  { id: 'F1', percorso: 'C:\\Users\\cammo\\Videos\\Matrimonio Napoli 4K.mov', nome: 'Matrimonio Napoli 4K.mov', cartella: 'C:\\Users\\cammo\\Videos', estensione: '.mov', categoria: 'video', nomeCategoria: 'Video', peso: 7_840_000_000, eDirectory: false, formati: formati.filter((x) => x.categoria === 'video').map((x) => x.id) },
  { id: 'F2', percorso: 'C:\\Users\\cammo\\Pictures\\Tramonto Posillipo.heic', nome: 'Tramonto Posillipo.heic', cartella: 'C:\\Users\\cammo\\Pictures', estensione: '.heic', categoria: 'immagine', nomeCategoria: 'Immagini', peso: 3_120_000, eDirectory: false, formati: formati.filter((x) => x.categoria === 'immagine').map((x) => x.id) },
  { id: 'F3', percorso: 'C:\\Users\\cammo\\Documents\\Preventivo DaProd.pdf', nome: 'Preventivo DaProd.pdf', cartella: 'C:\\Users\\cammo\\Documents', estensione: '.pdf', categoria: 'pdf', nomeCategoria: 'PDF', peso: 482_000, eDirectory: false, formati: formati.filter((x) => x.categoria === 'pdf').map((x) => x.id) },
];
const dettagli: Record<string, any> = {
  F1: { durata: 1834, larghezza: 3840, altezza: 2160, fps: 25, codec: 'hevc', hdr: true, bitrate: 33_800_000, audio: 'aac', canali: 2, tracceAudio: 1, sottotitoli: 0 },
  F2: { larghezza: 4032, altezza: 3024 },
  F3: { pagine: 4 },
};
const anteprime: Record<string, [string, string, string]> = { F1: ['#3b1d6e', '#ff3df2', 'NAPOLI'], F2: ['#ff9f43', '#3b1d6e', 'TRAMONTO'], F3: ['#f4f1fa', '#c9c2da', 'PDF'] };

let lavori: Lavoro[] = [];
let nl = 0;

function piano(file: InfoFile, o: any): Piano {
  const d = dettagli[file.id] ?? {};
  const dur = d.durata ?? 60;
  const ammessi = file.formati.includes('video.webm') && o.__est === '.webm' ? ['av1', 'vp9'] : ['h264', 'hevc', 'av1'];
  const codec = ammessi.includes(o.codec) ? o.codec : ammessi[0];
  const soglia = ({ h264: 0.085, hevc: 0.055, av1: 0.042, vp9: 0.05 } as any)[codec] * 1.15;
  const pesoMin = Math.round((dur * (90 + 48) * 1000) / 8 / 0.97);
  const pesoMax = file.peso;
  const note: string[] = [];
  let modo: Piano['modo'] = o.modo === 'peso' ? 'peso' : 'qualita';
  let lato = Math.min(d.larghezza, d.altezza);
  let vbps = 0, punteggio = 0.8, pesoStimato = 0, audio = 192;
  if (modo === 'peso') {
    const obiettivo = Math.min(pesoMax, Math.max(pesoMin, o.pesoByte || pesoMax / 2));
    const tot = (obiettivo * 0.97 * 8) / dur;
    audio = tot > 2_500_000 ? 160 : tot > 900_000 ? 128 : tot > 350_000 ? 96 : 64;
    vbps = tot - audio * 1000;
    const rapporto = (l: number) => vbps / ((l * 16) / 9 * l * (d.fps ?? 25)) / soglia;
    if (o.lato > 0) lato = Math.min(lato, o.lato);
    else {
      for (const c of [lato, 1440, 1080, 720, 540, 480, 360].filter((x) => x <= lato)) { lato = c; if (rapporto(c) >= 0.6) break; }
      if (lato < Math.min(d.larghezza, d.altezza)) note.push(`Per starci scendo a ${lato}p.`);
    }
    punteggio = Math.max(0, Math.min(1, Math.log2(rapporto(lato)) * 0.25 + 0.75)) * Math.pow(lato / Math.min(d.larghezza, d.altezza), 0.25);
    pesoStimato = obiettivo * 0.97;
  } else {
    punteggio = 0.3 + (o.qualita / 100) * 0.72;
    vbps = soglia / 1.15 * Math.pow(2, (o.qualita - 70) / 22) * (lato * 16 / 9) * lato * (d.fps ?? 25);
    pesoStimato = ((vbps + 192000) * dur) / 8 / 0.97;
  }
  if (d.hdr) note.push(codec === 'h264' ? 'HDR → SDR: i colori restano giusti sugli schermi normali.' : 'HDR tenuto (10 bit).');
  const giudizio = punteggio >= 0.95 ? "Come l'originale" : punteggio >= 0.72 ? 'Ottima' : punteggio >= 0.55 ? 'Buona' : punteggio >= 0.38 ? 'Si vede, ma va' : punteggio >= 0.2 ? 'Bassa' : 'Da buttare';
  const enc = o.motore === 'cpu' ? ({ h264: 'libx264', hevc: 'libx265', av1: 'libsvtav1' } as any)[codec] : `${codec}_nvenc`;
  return { modo, codec, encoder: enc, hardware: enc.endsWith('nvenc'), larghezza: Math.round(lato * 16 / 9 / 2) * 2, altezza: lato, fps: d.fps ?? 25,
    bitrateVideo: vbps, audioKbps: audio, punteggio, giudizio, pesoStimato, pesoMin, pesoMax, puoCopiare: false, note, codecAmmessi: ammessi };
}

export async function rispondi(cmd: string, args: any, emetti: (e: string, d: any) => void): Promise<any> {
  await new Promise((r) => setTimeout(r, 10));
  switch (cmd) {
    case 'stato': {
      setTimeout(() => {
        for (const e of esempi) {
          emetti('dettagli', { id: e.id, dettagli: dettagli[e.id] });
          emetti('anteprima', { id: e.id, url: quadro(...anteprime[e.id]) });
        }
      }, 300);
      setInterval(() => {
        const occupato = lavori.some((l) => l.stato === 'corre');
        emetti('carico', { cpu: occupato ? 22 + Math.random() * 18 : 4 + Math.random() * 4, encoder: occupato ? 78 + Math.random() * 15 : 0, decoder: occupato ? 40 + Math.random() * 20 : 0, grafica: 3 });
      }, 700);
      const modo = new URLSearchParams(location.search).get('modo') === 'rapido' ? 'rapido' : 'finestra';
      if (modo === 'rapido') setTimeout(() => void rispondi('converti', { elementi: [{ ids: ['F1'], formato: 'video.mp4', opzioni: {} }] }, emetti), 500);
      return {
        versione: '1.0.0',
        hardware: { cpu: 'AMD Ryzen 7 5700G with Radeon Graphics', thread: 16, gpu: [{ nome: 'NVIDIA GeForce RTX 4060', marca: 'nvidia', vram: 8 << 30, driver: '32.0.16.1742' }],
          encoder: ['h264_nvenc', 'hevc_nvenc', 'av1_nvenc', 'libx264', 'libx265', 'libsvtav1'], acceleratore: 'RTX 4060 · NVENC' },
        formati,
        categorie: [{ id: 'video', nome: 'Video' }, { id: 'audio', nome: 'Audio' }, { id: 'immagine', nome: 'Immagini' }, { id: 'pdf', nome: 'PDF' }, { id: 'documento', nome: 'Documenti' }, { id: 'archivio', nome: 'Archivi' }],
        impostazioni: { scritta: 'convertito', menu: true, alMassimo: false, suoni: true, apriCartella: false, cestino: true, menu11Chiesto: false, formati: {} },
        modo,
        file: modo === 'rapido' ? [esempi[0]] : esempi,
        lavori: [],
        office: { libreOffice: true, word: false },
        menu11: { supportato: true, registrato: false, fidato: false, chiesto: false, windows11: true, classico: false },
      } satisfies Stato;
    }
    case 'piano': {
      const file = esempi.find((e) => e.id === args.id)!;
      return piano(file, { ...args.opzioni.video, __est: formati.find((x) => x.id === args.formato)?.estensione });
    }
    case 'converti': {
      const ids: string[] = [];
      for (const el of args.elementi as { ids: string[]; formato: string }[]) {
        const fmt = formati.find((x) => x.id === el.formato)!;
        const gruppi = fmt.unisce ? [el.ids] : el.ids.map((i) => [i]);
        for (const g of gruppi) {
          const file = esempi.find((e) => e.id === g[0])!;
          const l: Lavoro = { id: `L${++nl}`, fileId: file.id, sorgenti: g.map((i) => esempi.find((e) => e.id === i)!.percorso), formato: fmt.id, etichetta: fmt.etichetta,
            stato: 'attesa', frazione: 0, fase: null, velocita: null, fps: null, eta: null, uscita: null, pesoPrima: file.peso, pesoDopo: null, errore: null, dettaglio: null, secondi: 0, nelCestino: false, notaCestino: null };
          lavori.push(l);
          ids.push(l.id);
          emetti('lavoro', l);
          simula(l, emetti);
        }
      }
      return ids;
    }
    case 'annulla':
      for (const l of lavori) if ((!args.id || l.id === args.id) && (l.stato === 'corre' || l.stato === 'attesa')) { l.stato = 'annullato'; emetti('lavoro', { ...l }); }
      return true;
    case 'confronta':
      return { prima: quadro('#3b1d6e', '#ff3df2', 'ORIGINALE'), dopo: quadro('#36195f', '#e83ad9', 'CONVERTITO'), tempo: 917 };
    case 'scegli':
      return [];
    default:
      return true;
  }
}

function simula(l: Lavoro, emetti: (e: string, d: any) => void) {
  const durata = l.formato.startsWith('video') ? 14000 : 2500;
  const inizio = performance.now() + (lavori.filter((x) => x.stato === 'corre').length ? 800 : 200);
  const t = setInterval(() => {
    if (l.stato === 'annullato') { clearInterval(t); return; }
    const ora = performance.now();
    if (ora < inizio) return;
    l.stato = 'corre';
    l.frazione = Math.min(1, (ora - inizio) / durata);
    l.fase = l.formato.startsWith('video') ? 'Converto con la scheda video (NVENC)' : 'Converto';
    l.velocita = l.formato.startsWith('video') ? 4.2 + Math.sin(ora / 900) * 0.6 : null;
    l.fps = l.formato.startsWith('video') ? Math.round(105 + Math.sin(ora / 700) * 12) : null;
    l.eta = ((1 - l.frazione) * durata) / 1000 * 30;
    if (l.frazione >= 1) {
      clearInterval(t);
      l.stato = 'fatto';
      l.uscita = l.sorgenti[0].replace(/\.[^.]+$/, ` (convertito)${formati.find((x) => x.id === l.formato)?.estensione || ''}`);
      l.pesoDopo = Math.round(l.pesoPrima * 0.21);
      l.secondi = durata / 1000;
      l.nelCestino = !!formati.find((x) => x.id === l.formato)?.sostituisce;
      l.eta = null;
    }
    emetti('lavoro', { ...l });
  }, 120);
}
