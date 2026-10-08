// I fogli: Excel, CSV, ODS… come in un foglio di calcolo, con le lettere sulle colonne, i numeri sulle righe e
// le linguette dei fogli in basso. Si scorre liscio anche con ventimila righe (si disegnano solo quelle che vedi).
import { chiedi } from '../ponte';
import { h } from '../util';
import { attesa, errore, ic, type Contesto, type Vista } from './comune';

interface Foglio { fogli: string[]; attivo: string; colonne: string[]; righe: unknown[][]; totale: number }

export async function monta(c: Contesto): Promise<Vista> {
  c.palco.append(attesa('Apro il foglio…'));
  const lento = setTimeout(() => c.stato('Lo apre LibreOffice: la prima volta ci vuole qualche secondo…'), 1500);
  let t: Foglio;
  try { t = await chiedi<Foglio>('tabella'); }
  catch (e) { clearTimeout(lento); c.stato(null); c.palco.replaceChildren(errore((e as Error).message)); return { tasti: [], smonta() {} }; }
  clearTimeout(lento);
  c.stato(null);
  if (!c.viva()) return { tasti: [], smonta() {} };

  const RIGA = 28;
  const corpo = h('div.f-corpo');
  const spazio = h('div.f-spazio', null, corpo);
  const scorre = h('div.f-scorre', null, spazio);
  const filtro = h<HTMLInputElement>('input.t-cerca', { placeholder: 'Filtra le righe…' });
  const conta = h('span.c-nota');
  const linguette = h('div.f-linguette');
  const box = h('div.f-box', null, h('div.p-comandi', null, conta, h('span.l-spazio'), h('div.t-cerca-box', null, h('span', { html: ic.cerca }), filtro)), scorre, linguette);
  c.palco.replaceChildren(box);

  let vis: number[] = [];
  let larghezze: number[] = [];
  const csv = t.fogli.length === 0;
  const lettera = (i: number) => { let s = ''; i++; while (i > 0) { const m = (i - 1) % 26; s = String.fromCharCode(65 + m) + s; i = Math.floor((i - 1) / 26); } return s; };
  const cella = (v: unknown) => (v == null ? '' : typeof v === 'number' ? v.toLocaleString('it-IT', { maximumFractionDigits: 10 }) : typeof v === 'boolean' ? (v ? 'VERO' : 'FALSO') : String(v));
  const esc = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;');

  function prepara() {
    const n = t.colonne.length;
    // la larghezza di ogni colonna dalle prime 300 righe
    larghezze = Array.from({ length: n }, (_, j) => {
      let m = csv ? t.colonne[j].length : 2;
      for (let i = 0; i < Math.min(300, t.righe.length); i++) m = Math.max(m, cella(t.righe[i][j]).length);
      return Math.min(420, Math.max(64, m * 7.6 + 24));
    });
    box.style.setProperty('--num', `${Math.max(44, String(t.righe.length).length * 9 + 22)}px`);
    filtra();
  }
  function filtra() {
    const q = filtro.value.toLowerCase();
    vis = [];
    for (let i = 0; i < t.righe.length; i++) if (!q || t.righe[i].some((v) => cella(v).toLowerCase().includes(q))) vis.push(i);
    conta.textContent = `${vis.length.toLocaleString('it-IT')} righe${q ? ` su ${t.righe.length.toLocaleString('it-IT')}` : ''} · ${t.colonne.length} colonne${t.totale > t.righe.length ? ` · ne vedi ${t.righe.length.toLocaleString('it-IT')} di ${t.totale.toLocaleString('it-IT')}` : ''}`;
    da = -1;
    disegna();
  }
  let da = -1, a = -1;
  function disegna() {
    const tot = larghezze.reduce((x, y) => x + y, 0);
    spazio.style.height = `${(vis.length + 1) * RIGA + 8}px`;
    spazio.style.width = `calc(var(--num) + ${tot}px)`;
    const primo = Math.max(0, Math.floor(scorre.scrollTop / RIGA) - 20);
    const ultimo = Math.min(vis.length, Math.ceil((scorre.scrollTop + scorre.clientHeight) / RIGA) + 20);
    if (primo >= da && ultimo <= a && da >= 0) return;
    da = Math.max(0, primo - 40); a = Math.min(vis.length, ultimo + 40);
    const cols = larghezze.map((w) => `${w}px`).join(' ');
    let html = `<div class="f-testa" style="grid-template-columns: var(--num) ${cols}"><span class="f-angolo"></span>${t.colonne.map((x, j) => `<span title="${esc(csv ? x : lettera(j))}">${esc(csv ? x : lettera(j))}</span>`).join('')}</div>`;
    html += `<div class="f-righe" style="transform: translateY(${da * RIGA}px)">`;
    for (let k = da; k < a; k++) {
      const i = vis[k];
      const r = t.righe[i];
      html += `<div class="f-riga${k % 2 ? ' pari' : ''}" style="grid-template-columns: var(--num) ${cols}"><span class="f-n">${i + 1}</span>`;
      for (let j = 0; j < t.colonne.length; j++) {
        const v = r[j];
        const s = esc(cella(v));
        html += `<span class="${typeof v === 'number' ? 'num' : ''}" title="${s.length > 30 ? s : ''}">${s}</span>`;
      }
      html += '</div>';
    }
    corpo.innerHTML = html + '</div>';
  }
  scorre.addEventListener('scroll', disegna, { passive: true });
  filtro.addEventListener('input', filtra);

  function linguetteFogli() {
    linguette.replaceChildren(...t.fogli.map((nome) => h(`button${nome === t.attivo ? '.su' : ''}`, {
      onclick: async () => {
        if (nome === t.attivo) return;
        try { t = await chiedi<Foglio>('tabella', { foglio: nome }); prepara(); scorre.scrollTo(0, 0); linguetteFogli(); c.info(); } catch (e) { c.hud((e as Error).message); }
      },
    }, nome)));
    linguette.hidden = t.fogli.length < 2;
  }
  linguetteFogli();
  prepara();

  return {
    tasti: [
      { k: ['ctrl+f', '/'], etichetta: 'Ctrl F', cosa: 'Filtra le righe', fai: () => filtro.focus() },
      { k: ['home', 'ctrl+home'], etichetta: 'Inizio', cosa: 'Prima riga', fai: () => scorre.scrollTo({ top: 0, left: 0 }) },
      { k: ['end', 'ctrl+end'], etichetta: 'Fine', cosa: 'Ultima riga', fai: () => scorre.scrollTo({ top: scorre.scrollHeight }) },
      { k: ['ctrl+pagedown'], etichetta: 'Ctrl Pag ↓', cosa: 'Foglio dopo', fai: () => (linguette.querySelector('.su')?.nextElementSibling as HTMLElement)?.click() },
      { k: ['ctrl+pageup'], etichetta: 'Ctrl Pag ↑', cosa: 'Foglio prima', fai: () => (linguette.querySelector('.su')?.previousElementSibling as HTMLElement)?.click() },
    ],
    info: () => `${t.totale.toLocaleString('it-IT')} righe${t.fogli.length > 1 ? ` · ${t.fogli.length} fogli` : ''}`,
    smonta() {},
  };
}
