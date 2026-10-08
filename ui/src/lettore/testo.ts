// I testi. Il codice col numero di riga e i colori (senza librerie: un tokenizzatore piccolo per famiglia di
// linguaggi), anche da centomila righe (si disegnano solo quelle che vedi). Il Markdown impaginato con
// l'indice, l'HTML com'è (senza script), il JSON ad albero, i sottotitoli battuta per battuta.
import { chiedi } from '../ponte';
import { h, clamp } from '../util';
import { attesa, errore, ic, memoria, type Contesto, type Tasto, type Vista } from './comune';

export async function monta(c: Contesto): Promise<Vista> {
  c.palco.append(attesa('Leggo…'));
  const t = c.scheda.tipo;
  try {
    if (t === 'markdown') {
      const r = await chiedi<{ html: string; testo: string; troncato: boolean }>('markdown');
      if (!c.viva()) return vuota;
      return doppia(c, () => markdown(c, r.html), () => codice(c, r.testo, 'md', r.troncato));
    }
    const r = await chiedi<{ testo: string; troncato: boolean }>('testo');
    if (!c.viva()) return vuota;
    if (t === 'web') return doppia(c, () => pagina(c, r.testo), () => codice(c, r.testo, 'markup', r.troncato));
    if (t === 'json' && !r.troncato && r.testo.length < 4e6) {
      let dati: unknown;
      try { dati = JSON.parse(r.testo); } catch { return codice(c, r.testo, 'json', r.troncato); }
      return doppia(c, () => albero(c, dati), () => codice(c, r.testo, 'json', r.troncato));
    }
    if (t === 'sottotitoli') return battute(c, r.testo);
    return codice(c, r.testo, linguaggio(c.scheda.estensione, c.scheda.nome), r.troncato);
  } catch (e) {
    c.palco.replaceChildren(errore((e as Error).message));
    return vuota;
  }
}

const vuota: Vista = { tasti: [], smonta() {} };

/** Due facce dello stesso file (impaginato / sorgente): M passa dall'una all'altra. */
function doppia(c: Contesto, bella: () => Vista, sorgente: () => Vista): Vista {
  let su = memoria.leggi<boolean>(`sorgente:${c.scheda.tipo}`) ? 1 : 0;
  let attuale = su ? sorgente() : bella();
  const giro: Tasto = {
    k: ['m'], etichetta: 'M', cosa: 'Impaginato / sorgente', fai: () => {
      attuale.smonta();
      su = 1 - su;
      memoria.scrivi(`sorgente:${c.scheda.tipo}`, su === 1);
      attuale = su ? sorgente() : bella();
      v.tasti = [giro, ...attuale.tasti];
      v.info = attuale.info;
      c.info();
      c.hud(su ? 'Sorgente' : 'Impaginato', su ? ic.codice : ic.libro);
    },
  };
  const v: Vista = { tasti: [giro, ...attuale.tasti], info: attuale.info, smonta: () => attuale.smonta() };
  return v;
}

// ————————————————————————— il codice —————————————————————————

type Fam = 'c' | 'py' | 'sh' | 'ps' | 'sql' | 'lua' | 'markup' | 'css' | 'yaml' | 'ini' | 'json' | 'log' | 'diff' | 'md' | 'testo';

function linguaggio(est: string, nome: string): Fam {
  const e = est.replace('.', '');
  if (/^(js|mjs|cjs|ts|tsx|jsx|cs|c|h|cpp|hpp|cc|java|kt|go|rs|swift|dart|php|scala|fs|gradle|json5|jsonc)$/.test(e)) return 'c';
  if (e === 'py') return 'py';
  if (/^(sh|bash|rb|pl|r|dockerfile|makefile)$/.test(e) || /^(makefile|dockerfile)$/i.test(nome)) return 'sh';
  if (/^(ps1|psm1|bat|cmd)$/.test(e)) return 'ps';
  if (e === 'sql') return 'sql';
  if (e === 'lua') return 'lua';
  if (/^(xml|xaml|html|htm|csproj|props|targets|svg|vue|svelte|config|manifest)$/.test(e)) return 'markup';
  if (/^(css|scss|less)$/.test(e)) return 'css';
  if (/^(yml|yaml)$/.test(e)) return 'yaml';
  if (/^(ini|cfg|conf|env|toml|editorconfig|gitignore|reg|inf|properties)$/.test(e)) return 'ini';
  if (/^(json|geojson|webmanifest)$/.test(e)) return 'json';
  if (e === 'log') return 'log';
  if (/^(diff|patch)$/.test(e)) return 'diff';
  if (/^(md|markdown)$/.test(e)) return 'md';
  return 'testo';
}

const parole: Partial<Record<Fam, string>> = {
  c: 'abstract async await bool break byte case catch char class const continue default delegate do double else enum event explicit export extends false final finally fixed float for foreach fn from func function get go if impl implements import in int interface internal is let long match mod module mut namespace new nil null object operator out override package private protected pub public readonly record ref return sealed select self set short static string struct super switch this throw throws true try type typeof uint ulong unsafe use using var virtual void volatile where while with yield',
  py: 'and as assert async await break class continue def del elif else except False finally for from global if import in is lambda None nonlocal not or pass raise return self True try while with yield print',
  sh: 'if then else elif fi for while do done case esac in function return local export echo exit def end class module require unless until begin rescue ensure',
  ps: 'begin break catch class continue data do dynamicparam else elseif end exit filter finally for foreach from function if in param process return switch throw trap try until using var while echo set goto call rem not exist errorlevel',
  sql: 'select from where and or not insert into values update set delete create table alter drop index view join left right inner outer on group by order having limit offset as distinct union all null is in like between primary key foreign references default case when then else end begin commit rollback',
  lua: 'and break do else elseif end false for function goto if in local nil not or repeat return then true until while',
  css: 'important media import from to keyframes font-face supports',
};

interface Regole { commento?: string; blocco?: [string, string]; stringhe: string; parole?: Set<string> }

function regole(f: Fam): Regole {
  const p = parole[f] ? new Set(parole[f]!.split(' ').map((x) => (f === 'sql' || f === 'ps' ? x.toLowerCase() : x))) : undefined;
  switch (f) {
    case 'c': return { commento: '//', blocco: ['/*', '*/'], stringhe: '"\'`', parole: p };
    case 'css': return { blocco: ['/*', '*/'], stringhe: '"\'', parole: p };
    case 'py': case 'sh': case 'yaml': return { commento: '#', stringhe: '"\'', parole: p };
    case 'ps': return { commento: '#', blocco: ['<#', '#>'], stringhe: '"\'', parole: p };
    case 'sql': case 'lua': return { commento: '--', blocco: f === 'lua' ? ['--[[', ']]'] : ['/*', '*/'], stringhe: '"\'', parole: p };
    case 'ini': return { commento: ';', stringhe: '"', parole: p };
    case 'json': return { stringhe: '"', parole: new Set(['true', 'false', 'null']) };
    default: return { stringhe: '' };
  }
}

type Pezzo = [classe: string, testo: string];

/** Il testo in pezzi colorati. Una passata sola, carattere per carattere: veloce anche su file grossi. */
function colora(testo: string, f: Fam): Pezzo[] {
  if (f === 'markup') return coloraMarkup(testo);
  if (f === 'testo' || f === 'md' || f === 'log' || f === 'diff') return [['', testo]];
  const r = regole(f);
  const out: Pezzo[] = [];
  let i = 0, piano = 0;
  const n = testo.length;
  const spingi = (cl: string, a: number) => { if (piano < i) out.push(['', testo.slice(piano, i)]); out.push([cl, testo.slice(i, a)]); i = piano = a; };
  const ci = f === 'sql' || f === 'ps';
  while (i < n) {
    const ch = testo[i];
    if (r.blocco && testo.startsWith(r.blocco[0], i)) { const e = testo.indexOf(r.blocco[1], i + r.blocco[0].length); spingi('co', e < 0 ? n : e + r.blocco[1].length); continue; }
    if (r.commento && testo.startsWith(r.commento, i) && !(f === 'c' && testo[i - 1] === ':')) { const e = testo.indexOf('\n', i); spingi('co', e < 0 ? n : e); continue; }
    if (r.stringhe.includes(ch)) {
      let e = i + 1;
      while (e < n && testo[e] !== ch && !(testo[e] === '\n' && ch !== '`')) e += testo[e] === '\\' ? 2 : 1;
      // in JSON le chiavi hanno un colore loro
      const cl = f === 'json' && /^\s*:/.test(testo.slice(e + 1, e + 4)) ? 'ch' : 'st';
      spingi(cl, Math.min(n, e + 1));
      continue;
    }
    if (/[0-9]/.test(ch) && !/[\w$]/.test(testo[i - 1] ?? '')) { let e = i + 1; while (e < n && /[0-9a-fA-FxX._]/.test(testo[e])) e++; spingi('nu', e); continue; }
    if (/[A-Za-z_$@]/.test(ch)) {
      let e = i + 1;
      while (e < n && /[\w$-]/.test(testo[e]) && !(testo[e] === '-' && f !== 'css' && f !== 'ps')) e++;
      const w = testo.slice(i, e);
      if (r.parole?.has(ci ? w.toLowerCase() : w)) { spingi('pa', e); continue; }
      if (testo[e] === '(') { spingi('fu', e); continue; }
      if (f === 'yaml' || f === 'ini') { if (/^\s*[:=]/.test(testo.slice(e, e + 3)) && /(^|\n)\s*$/.test(testo.slice(Math.max(0, i - 40), i))) { spingi('ch', e); continue; } }
      if (/^[A-Z][a-z]/.test(w) && f === 'c') { spingi('ti', e); continue; }
      i = e;
      continue;
    }
    if (f === 'ini' && ch === '[' && (i === 0 || testo[i - 1] === '\n')) { const e = testo.indexOf('\n', i); spingi('pa', e < 0 ? n : e); continue; }
    i++;
  }
  if (piano < n) out.push(['', testo.slice(piano)]);
  return out;
}

function coloraMarkup(t: string): Pezzo[] {
  const out: Pezzo[] = [];
  const re = /(<!--[\s\S]*?-->)|(<\/?)([\w:.-]+)((?:\s+[\w:.@-]+(?:\s*=\s*(?:"[^"]*"|'[^']*'|[^\s>]+))?)*)\s*(\/?>)/g;
  let ultimo = 0;
  for (let m; (m = re.exec(t));) {
    if (m.index > ultimo) out.push(['', t.slice(ultimo, m.index)]);
    if (m[1]) out.push(['co', m[1]]);
    else {
      out.push(['pu', m[2]], ['pa', m[3]]);
      const attr = m[4];
      const ra = /(\s+)([\w:.@-]+)(?:(\s*=\s*)("[^"]*"|'[^']*'|[^\s>]+))?/g;
      for (let a; (a = ra.exec(attr));) { out.push(['', a[1]], ['ch', a[2]]); if (a[3]) out.push(['', a[3]], ['st', a[4]]); }
      out.push(['pu', m[0].slice(m[2].length + m[3].length + attr.length)]);
    }
    ultimo = re.lastIndex;
  }
  if (ultimo < t.length) out.push(['', t.slice(ultimo)]);
  return out;
}

const esc = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

/** I pezzi colorati diventano righe di HTML (un pezzo che va a capo si spezza in due righe). */
function inRighe(pezzi: Pezzo[]): string[] {
  const righe: string[] = [];
  let r = '';
  for (const [cl, t] of pezzi) {
    const parti = t.split('\n');
    parti.forEach((p, k) => {
      if (k > 0) { righe.push(r); r = ''; }
      if (p) r += cl ? `<span class="${cl}">${esc(p)}</span>` : esc(p);
    });
  }
  righe.push(r);
  return righe;
}

function codice(c: Contesto, testo: string, fam: Fam, troncato: boolean): Vista {
  testo = testo.replace(/\r\n?/g, '\n');
  const grezze = testo.split('\n');
  const n = grezze.length;
  const conColori = testo.length < 2_000_000 && fam !== 'testo';
  let righe: string[] = conColori ? inRighe(colora(testo, fam)) : grezze.map(esc);
  if (fam === 'log') righe = grezze.map((r) => coloraLog(esc(r)));
  if (fam === 'diff') righe = grezze.map((r) => `<span class="${r.startsWith('+') ? 'di-piu' : r.startsWith('-') ? 'di-meno' : r.startsWith('@@') ? 'pa' : ''}">${esc(r)}</span>`);

  let a_capo = memoria.leggi<boolean>('a-capo') ?? (fam === 'testo' || fam === 'md');
  let corpo = memoria.leggi<number>('codice-corpo') ?? 13;
  const ALTEZZA = () => Math.round(corpo * 1.6);
  const fogli = h('div.t-righe');
  const spazio = h('div.t-spazio', null, fogli);
  const scorre = h('div.t-scorre', null, spazio);
  const cerca = h<HTMLInputElement>('input.t-cerca', { placeholder: 'Cerca…  (Invio: dopo)', spellcheck: false });
  const trovati = h('span.t-trovati');
  const sopra = h('div.t-sopra', null,
    h('span.c-nota', null, `${n.toLocaleString('it-IT')} righe${troncato ? ' · è lungo: ne vedi l\'inizio' : ''}`),
    h('span.l-spazio'),
    h('div.t-cerca-box', null, h('span', { html: ic.cerca }), cerca, trovati),
    h('button.l-icona', { title: 'A capo (W)', html: ic.avvolgi, onclick: () => giraACapo() }));
  const box = h(`div.t-codice.fam-${fam}`, null, sopra, scorre);
  c.palco.replaceChildren(box);
  let cercato = '';
  let colpi: number[] = [];
  let colpo = -1;

  function evidenzia(html: string) {
    if (!cercato) return html;
    // si evidenzia solo fuori dai tag
    const q = cercato.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    return html.replace(new RegExp(`(>|^)([^<]*)`, 'g'), (_, a: string, b: string) => a + b.replace(new RegExp(esc(q), 'gi'), (m) => `<mark>${m}</mark>`));
  }
  const riga = (i: number) => `<div class="t-r${i === colpi[colpo] ? ' su' : ''}"><i>${i + 1}</i><code>${evidenzia(righe[i]) || ' '}</code></div>`;

  // ——— senza a capo: si disegnano solo le righe che si vedono ———
  let da = -1, a = -1;
  function disegna(forza = false) {
    box.style.setProperty('--corpo', `${corpo}px`);
    box.style.setProperty('--riga', `${ALTEZZA()}px`);
    box.style.setProperty('--cifre', String(String(n).length));
    box.classList.toggle('a-capo', a_capo);
    if (a_capo) {
      spazio.style.height = '';
      fogli.style.transform = '';
      if (forza || da !== 0) { fogli.innerHTML = righe.map((_, i) => riga(i)).join(''); da = 0; a = n; }
      return;
    }
    const lh = ALTEZZA();
    spazio.style.height = `${n * lh + 24}px`;
    const primo = Math.max(0, Math.floor(scorre.scrollTop / lh) - 40);
    const ultimo = Math.min(n, Math.ceil((scorre.scrollTop + scorre.clientHeight) / lh) + 40);
    if (!forza && primo >= da && ultimo <= a) return;
    da = Math.max(0, primo - 80); a = Math.min(n, ultimo + 80);
    let html = '';
    for (let i = da; i < a; i++) html += riga(i);
    fogli.innerHTML = html;
    fogli.style.transform = `translateY(${da * lh}px)`;
  }
  scorre.addEventListener('scroll', () => { if (!a_capo) disegna(); }, { passive: true });
  if (a_capo && n > 20000) a_capo = false;
  disegna(true);

  function giraACapo() {
    if (!a_capo && n > 20000) { c.hud('Troppo lungo per andare a capo'); return; }
    a_capo = !a_capo;
    memoria.scrivi('a-capo', a_capo);
    disegna(true);
    c.hud(a_capo ? 'A capo' : 'Righe intere', ic.avvolgi);
  }
  function dimensione(d: number) {
    corpo = clamp(corpo + d, 9, 26);
    memoria.scrivi('codice-corpo', corpo);
    disegna(true);
    c.hud(`Testo ${corpo} px`);
  }
  function vaiAllaRiga(i: number) {
    if (a_capo) { (fogli.children[i] as HTMLElement)?.scrollIntoView({ block: 'center' }); return; }
    scorre.scrollTop = i * ALTEZZA() - scorre.clientHeight / 2;
    disegna(true);
  }
  function trova(avanti = true) {
    const q = cerca.value;
    if (q !== cercato) {
      cercato = q;
      const ql = q.toLowerCase();
      colpi = q ? grezze.map((r, i) => (r.toLowerCase().includes(ql) ? i : -1)).filter((i) => i >= 0) : [];
      colpo = -1;
    }
    if (colpi.length) {
      colpo = (colpo + (avanti ? 1 : -1) + colpi.length) % colpi.length;
      vaiAllaRiga(colpi[colpo]);
    }
    trovati.textContent = q ? (colpi.length ? `${colpo + 1}/${colpi.length}` : 'niente') : '';
    disegna(true);
  }
  cerca.addEventListener('keydown', (e) => { if (e.key === 'Enter') { e.preventDefault(); trova(!e.shiftKey); } });
  cerca.addEventListener('input', () => { colpo = -1; cercato = ''; trova(); });

  return {
    tasti: [
      { k: ['ctrl+f', '/'], etichetta: 'Ctrl F', cosa: 'Cerca', fai: () => { cerca.focus(); cerca.select(); } },
      { k: ['f3'], etichetta: 'F3', cosa: 'Il prossimo trovato', fai: () => trova(true) },
      { k: ['w'], etichetta: 'W', cosa: 'A capo / righe intere', fai: giraACapo },
      { k: ['+', '=', 'ctrl+='], etichetta: '+', cosa: 'Testo più grande', fai: () => dimensione(1) },
      { k: ['-', 'ctrl+-'], etichetta: '−', cosa: 'Testo più piccolo', fai: () => dimensione(-1) },
      { k: ['home', 'ctrl+home'], etichetta: 'Inizio', cosa: 'All\'inizio', fai: () => { scorre.scrollTop = 0; } },
      { k: ['end', 'ctrl+end'], etichetta: 'Fine', cosa: 'Alla fine', fai: () => { scorre.scrollTop = scorre.scrollHeight; } },
      { k: ['ctrl+a'], etichetta: 'Ctrl A', cosa: 'Copia tutto il testo', fai: () => void navigator.clipboard.writeText(testo).then(() => c.hud('Testo copiato', ic.copia)) },
    ],
    info: () => ({ c: 'Codice', py: 'Python', sh: 'Script', ps: 'PowerShell / batch', sql: 'SQL', lua: 'Lua', markup: 'XML / HTML', css: 'CSS', yaml: 'YAML', ini: 'Configurazione', json: 'JSON', log: 'Registro', diff: 'Differenze', md: 'Markdown', testo: 'Testo' } as Record<Fam, string>)[fam] + ` · ${n.toLocaleString('it-IT')} righe`,
    smonta() {},
  };
}

function coloraLog(r: string) {
  return r
    .replace(/^(\[?\d{4}-\d\d-\d\d[ T]\d\d:\d\d:\d\d[^\]\s]*\]?|\d\d:\d\d:\d\d(?:[.,]\d+)?)/, '<span class="nu">$1</span>')
    .replace(/\b(ERROR|ERRORE|FATAL|FAIL(?:ED)?|EXCEPTION|Exception)\b/g, '<span class="lg-e">$1</span>')
    .replace(/\b(WARN(?:ING)?|ATTENZIONE)\b/g, '<span class="lg-w">$1</span>')
    .replace(/\b(INFO|DEBUG|TRACE)\b/g, '<span class="co">$1</span>');
}

// ————————————————————————— Markdown —————————————————————————

function markdown(c: Contesto, html: string): Vista {
  const carta = h('article.c-carta.md', { html });
  // le foto con percorso relativo stanno accanto al file
  carta.querySelectorAll('img').forEach((i) => { const s = i.getAttribute('src') ?? ''; if (!/^[a-z]+:/i.test(s)) i.src = new URL(s, c.scheda.url).href; });
  carta.querySelectorAll('a').forEach((a) => a.addEventListener('click', (e) => {
    const href = a.getAttribute('href') ?? '';
    e.preventDefault();
    if (href.startsWith('#')) carta.querySelector(`[id="${CSS.escape(decodeURIComponent(href.slice(1)))}"]`)?.scrollIntoView({ behavior: 'smooth' });
  }));
  const titoli = [...carta.querySelectorAll('h1, h2, h3')] as HTMLElement[];
  const indice = titoli.length > 2 ? h('nav.c-indice', null, h('b', null, 'INDICE'), ...titoli.map((t) =>
    h(`button.l${t.tagName[1]}`, { onclick: () => t.scrollIntoView({ behavior: 'smooth' }) }, t.textContent ?? ''))) : null;
  const scorre = h('div.c-scorre', null, carta);
  c.palco.replaceChildren(h(`div.c-tavolo.con-indice${indice ? '' : '.senza'}`, null, indice, scorre));
  const parole = (carta.textContent ?? '').trim().split(/\s+/).filter(Boolean).length;
  return {
    tasti: [
      { k: ['home'], etichetta: 'Inizio', cosa: 'All\'inizio', fai: () => scorre.scrollTo({ top: 0, behavior: 'smooth' }) },
      { k: ['end'], etichetta: 'Fine', cosa: 'Alla fine', fai: () => scorre.scrollTo({ top: scorre.scrollHeight, behavior: 'smooth' }) },
    ],
    info: () => `${parole.toLocaleString('it-IT')} parole · circa ${Math.max(1, Math.round(parole / 230))} min`,
    smonta() {},
  };
}

// ————————————————————————— pagina web —————————————————————————

function pagina(c: Contesto, testo: string): Vista {
  // gli script non girano (sandbox): la pagina si vede, ma non fa niente da sola
  const base = `<base href="${c.scheda.url}">`;
  const doc = /<head[^>]*>/i.test(testo) ? testo.replace(/<head[^>]*>/i, (m) => m + base) : base + testo;
  const f = h<HTMLIFrameElement>('iframe.w-pagina', { sandbox: '', referrerpolicy: 'no-referrer' });
  f.srcdoc = doc;
  c.palco.replaceChildren(h('div.w-cornice', null, h('div.p-comandi', null, h('span.c-nota', null, 'La pagina com\'è, senza far girare i suoi script (M per il sorgente)')), f));
  return { tasti: [], info: () => 'Pagina web', smonta() {} };
}

// ————————————————————————— JSON ad albero —————————————————————————

function albero(c: Contesto, dati: unknown): Vista {
  const radice = h('div.j-albero');
  const nodo = (chiave: string | null, v: unknown, livello: number): HTMLElement => {
    const k = chiave !== null ? h('span.j-k', null, chiave) : null;
    if (v !== null && typeof v === 'object') {
      const arr = Array.isArray(v);
      const voci = arr ? (v as unknown[]).map((x, i) => [String(i), x] as const) : Object.entries(v as object);
      const figli = h('div.j-figli');
      const aperto = livello < 2;
      const testa = h('button.j-testa', null, h('i.j-freccia'), k, h('span.j-tipo', null, arr ? `[ ${voci.length} ]` : `{ ${voci.length} }`));
      const el = h(`div.j-nodo${aperto ? '.aperto' : ''}`, null, testa, figli);
      let fatto = false;
      const riempi = () => { if (fatto) return; fatto = true; for (const [kk, vv] of voci.slice(0, 5000)) figli.append(nodo(arr ? null : kk, vv, livello + 1)); if (voci.length > 5000) figli.append(h('div.j-v', null, `… e altri ${voci.length - 5000}`)); };
      if (aperto) riempi();
      testa.addEventListener('click', () => { riempi(); el.classList.toggle('aperto'); });
      return el;
    }
    const tipo = v === null ? 'nl' : typeof v === 'string' ? 'st' : typeof v === 'number' ? 'nu' : 'pa';
    return h('div.j-foglia', null, k, k ? h('span.j-due', null, ':') : null, h(`span.${tipo}`, null, typeof v === 'string' ? `"${v}"` : String(v)));
  };
  radice.append(nodo(null, dati, 0));
  c.palco.replaceChildren(h('div.j-box', null, h('div.p-comandi', null, h('span.c-nota', null, 'Clic per aprire e chiudere · M per il testo'),
    h('button.l-tasto', { onclick: () => radice.querySelectorAll('.j-testa').forEach((t) => { if (!t.parentElement!.classList.contains('aperto')) (t as HTMLElement).click(); }) }, 'Apri tutto')), radice));
  return { tasti: [], info: () => (Array.isArray(dati) ? `Elenco di ${dati.length}` : 'Oggetto JSON'), smonta() {} };
}

// ————————————————————————— sottotitoli —————————————————————————

function battute(c: Contesto, testo: string): Vista {
  const righe = testo.replace(/\r/g, '').replace(/^\uFEFF/, '');
  const b: { da: string; a: string; t: string }[] = [];
  if (/^\[Script Info\]/m.test(righe)) {
    for (const r of righe.split('\n')) {
      const m = r.match(/^Dialogue:\s*[^,]*,([^,]*),([^,]*),(?:[^,]*,){6}(.*)$/);
      if (m) b.push({ da: m[1], a: m[2], t: m[3].replace(/\{[^}]*\}/g, '').replace(/\\N/gi, '\n') });
    }
  } else {
    for (const blocco of righe.split(/\n\s*\n/)) {
      const m = blocco.match(/(\d[\d:.,]+)\s*-->\s*(\d[\d:.,]+)[^\n]*\n([\s\S]+)/);
      if (m) b.push({ da: m[1].replace(',', '.').replace(/^00:/, ''), a: m[2].replace(',', '.').replace(/^00:/, ''), t: m[3].replace(/<[^>]+>/g, '').trim() });
    }
  }
  const lista = h('div.s-lista', null, ...b.map((x, i) => h('div.s-battuta', null, h('span.s-n', null, String(i + 1)), h('span.s-tempo', null, x.da.replace(/\.\d+$/, '')), h('p', null, x.t))));
  const cerca = h<HTMLInputElement>('input.t-cerca', { placeholder: 'Cerca una battuta…' });
  cerca.addEventListener('input', () => {
    const q = cerca.value.toLowerCase();
    [...lista.children].forEach((el, i) => (el as HTMLElement).classList.toggle('via', !!q && !b[i].t.toLowerCase().includes(q)));
  });
  c.palco.replaceChildren(h('div.s-box', null, h('div.p-comandi', null, h('span.c-nota', null, `${b.length} battute${b.length ? ` · fino a ${b[b.length - 1].a.replace(/\.\d+$/, '')}` : ''}`), h('span.l-spazio'), h('div.t-cerca-box', null, h('span', { html: ic.cerca }), cerca)), lista));
  return {
    tasti: [{ k: ['ctrl+f', '/'], etichetta: 'Ctrl F', cosa: 'Cerca', fai: () => cerca.focus() }],
    info: () => `${b.length} battute`,
    smonta() {},
  };
}
