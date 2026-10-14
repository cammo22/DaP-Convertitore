// Lo scheletro della pagina (testa, coda, banco, piede) e le due cose che servono a tutti: il logo e l'avviso.
import { chiedi } from '../ponte';
import { h } from '../util';
import { icone } from '../icone';
import { bu } from '../suoni';

export const app = document.getElementById('app')!;
export const testa = h('header.testa');
export const coda = h('aside.coda');
export const banco = h('section.banco');
export const piede = h('footer.piede');
export const corpo = h('main.corpo', null, coda, banco);

app.append(testa, corpo, piede);

export const logoSvg = `<svg viewBox="0 0 64 64"><defs>
<linearGradient id="lo" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fffbe0"/><stop offset=".45" stop-color="#ffd54a"/><stop offset="1" stop-color="#b87a14"/></linearGradient>
<linearGradient id="lm" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#ff7bf6"/><stop offset="1" stop-color="#8a2cff"/></linearGradient></defs>
<circle cx="32" cy="32" r="28" fill="#150e24" stroke="url(#lo)" stroke-width="4"/>
<path d="M20 26a13 13 0 0 1 23-6" fill="none" stroke="url(#lm)" stroke-width="4" stroke-linecap="round"/><path d="M45 13v8h-8" fill="none" stroke="url(#lm)" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"/>
<path d="M44 38a13 13 0 0 1-23 6" fill="none" stroke="url(#lo)" stroke-width="4" stroke-linecap="round"/><path d="M19 51v-8h8" fill="none" stroke="url(#lo)" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"/>
</svg>`;

export function mostraAvviso(testo: string) {
  bu();
  const a = h('div.avviso', null, h('span', { html: icone.attenzione }), testo,
    testo.includes('LibreOffice') ? h('button.link', { onclick: () => chiedi('link', { url: 'https://it.libreoffice.org/download/download/' }) }, 'Scarica LibreOffice') : null);
  banco.append(a);
  setTimeout(() => a.remove(), 5000);
}
