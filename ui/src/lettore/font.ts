// Il campionario: il carattere in grande, la frase che scrivi tu a tutte le misure, tutte le lettere. «Installa»
// lo mette in Windows solo per te, senza chiedere permessi.
import { chiedi } from '../ponte';
import { h } from '../util';
import { attesa, errore, ic, memoria, type Contesto, type Vista } from './comune';

export async function monta(c: Contesto): Promise<Vista> {
  c.palco.append(attesa('Apro il carattere…'));
  const r = await chiedi<{ url: string; installato: boolean }>('font');
  const famiglia = `dap-font-${Math.random().toString(36).slice(2, 8)}`;
  const ff = new FontFace(famiglia, `url("${r.url}")`);
  try { await ff.load(); document.fonts.add(ff); }
  catch { c.palco.replaceChildren(errore('Questo carattere non si apre.')); return { tasti: [], smonta() {} }; }
  if (!c.viva()) { document.fonts.delete(ff); return { tasti: [], smonta() {} }; }

  const nome = c.scheda.nome.replace(/\.[^.]+$/, '').replace(/[-_]/g, ' ');
  let frase = memoria.leggi<string>('frase-font') ?? 'Ma la volpe, col suo balzo, ha raggiunto il quieto Fido.';
  const prova = h('div.n-prova', { contenteditable: 'plaintext-only', spellcheck: false }, frase);
  prova.addEventListener('input', () => { frase = prova.textContent ?? ''; memoria.scrivi('frase-font', frase); misure.querySelectorAll('p').forEach((p) => (p.textContent = frase)); });
  const misure = h('div.n-misure', null, ...[12, 16, 20, 28, 36, 48, 64, 88].map((px) => h('div', null, h('small', null, `${px}`), h('p', { style: { fontSize: `${px}px` } }, frase))));
  const glifi = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789àèéìòùÀÈÉÌÒÙçñß&@#%€$£¥!?¿¡.,;:…«»"\'()[]{}+-×÷=<>/\\|*~^_';
  const installa = ['.ttf', '.otf'].includes(c.scheda.estensione)
    ? h(`button.l-converti${r.installato ? '.fatto' : ''}`, {
        disabled: r.installato,
        onclick: async (e: Event) => {
          const b = e.currentTarget as HTMLButtonElement;
          try { await chiedi('installaFont'); b.classList.add('fatto'); b.disabled = true; b.lastChild!.textContent = 'Installato'; c.hud('Installato per te', ic.ok); }
          catch (err) { c.hud((err as Error).message); }
        },
      }, h('span', { html: r.installato ? ic.ok : ic.scarica }), r.installato ? 'Installato' : 'Installa')
    : null;
  c.palco.replaceChildren(h('div.n-box', { style: { '--font': `'${famiglia}'` } },
    h('div.n-eroe', null,
      h('div.n-aa', null, 'Aa'),
      h('div', null, h('h1', null, nome), h('span.c-nota', null, 'Scrivi nella riga qui sotto per provarlo con le tue parole')),
      h('span.l-spazio'),
      installa),
    prova,
    misure,
    h('div.n-glifi', null, ...[...glifi].map((g) => h('span', { title: `U+${g.codePointAt(0)!.toString(16).toUpperCase().padStart(4, '0')}` }, g)))));
  return {
    tasti: [{ k: ['ctrl+i'], etichetta: 'Ctrl I', cosa: 'Installa per te', fai: () => (installa as HTMLButtonElement | null)?.click() }],
    info: () => 'Carattere',
    smonta() { document.fonts.delete(ff); },
  };
}
