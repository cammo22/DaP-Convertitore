<p align="center">
  <a href="https://discord.gg/Njs52aacwq"><img src=".github/discord-card.svg" alt="Entra nella community DaProd su Discord" width="720"></a>
</p>

<p align="center">
  <img src="risorse/readme/hero.png" alt="DaP Convertitore — tasto destro, fatto." width="100%">
</p>

<p align="center">
  <b>Un convertitore per Windows che si usa dal tasto destro.</b><br>
  Video, audio, foto, PDF, documenti e archivi: tutto sul tuo PC, senza account e senza internet.
</p>

<p align="center">
  <a href="https://github.com/cammo22/DaP-Convertitore/releases/latest"><img src="https://img.shields.io/github/v/release/cammo22/DaP-Convertitore?label=versione&color=ffd54a&labelColor=1a1428&style=for-the-badge" alt="Versione"></a>
  <img src="https://img.shields.io/badge/100%25-offline-5dffb4?labelColor=1a1428&style=for-the-badge" alt="Offline">
  <img src="https://img.shields.io/badge/gratis-senza%20pubblicit%C3%A0-ff3df2?labelColor=1a1428&style=for-the-badge" alt="Gratis">
  <a href="LICENSE"><img src="https://img.shields.io/badge/licenza-MIT-a19db0?labelColor=1a1428&style=for-the-badge" alt="Licenza MIT"></a>
</p>

<br>

<h2 align="center">⬇&nbsp; Scarica</h2>

<p align="center">
  <a href="https://github.com/cammo22/DaP-Convertitore/releases/latest/download/DaPConvertitore-win-Setup.exe"><img src="risorse/readme/scarica-win.png" alt="Scarica l'installer per Windows" width="49%"></a>
  <a href="https://github.com/cammo22/DaP-Convertitore/releases/latest/download/DaPConvertitore-win-Portable.zip"><img src="risorse/readme/scarica-portatile.png" alt="Scarica la versione portatile per Windows" width="49%"></a>
  <img src="risorse/readme/scarica-linux.png" alt="Linux, in arrivo" width="49%">
  <img src="risorse/readme/scarica-macos.png" alt="macOS, in arrivo" width="49%">
</p>

<p align="center"><sub>I due pulsanti di Windows scaricano il file direttamente, senza passare dalla pagina delle release.</sub></p>

<br>

<h2 align="center">Cosa sa fare</h2>

<p align="center">
  <img src="risorse/readme/funzioni.png" alt="Tasto destro, video e audio, foto, PDF e documenti, archivi, un lettore per tutto" width="100%">
</p>

<br>

<h2 align="center">Guardalo all'opera</h2>

<p align="center">
  <img src="risorse/readme/vetrina-1.png" alt="La home e il lettore video" width="100%">
</p>
<p align="center">
  <img src="risorse/readme/vetrina-2.png" alt="Gli archivi con le miniature e lo slider della compressione" width="100%">
</p>

<br>

<h2 align="center">C'è anche un avatar</h2>

<p align="center">
  Un piccolo <b>oggetto DaProd</b> vive sul desktop: una musicassetta che ogni tanto saluta e, ogni qualche minuto,<br>
  si trasforma in un VHS, un floppy, un CD, un vinile… <b>Ci trascini sopra un file</b> e lo apre, lo converte o lo comprime.<br>
  La musica parte subito, con il suo piccolo player. Con la rotella lo fai grande o piccolo.<br>
  Si accende da <b>Impostazioni → Avatar</b>.
</p>

<p align="center">
  <img src="risorse/schermate/avatar.png" alt="L'avatar sul desktop" width="80%">
</p>

<br>

<h2 align="center">Come si installa</h2>

<p align="center">
  <img src="risorse/readme/passi.png" alt="1 Scarica, 2 Installa, 3 Tasto destro su un file" width="100%">
</p>

<p align="center"><sub>Windows può dire «Windows ha protetto il PC», perché l'installatore non ha ancora la firma a pagamento.<br>Premi <b>Ulteriori informazioni → Esegui comunque</b>. Si installa per il tuo utente, senza amministratore, e si aggiorna da solo.</sub></p>

<br>

## Com'è fatto

Due pezzi, ed è per questo che è veloce e semplice da mantenere.

| | |
| --- | --- |
| **Il motore** | Scritto in **.NET 10**, senza finestre: fa tutto il lavoro. Un solo catalogo dice cosa si converte in cosa, e lo leggono l'interfaccia, il menu del tasto destro e le prove. Sotto lavorano **FFmpeg** (video e audio), **7-Zip** (archivi) e il motore immagini di Windows (foto). |
| **L'interfaccia** | Una pagina in **TypeScript** dentro una finestra **WPF** con **WebView2**, il motore di Edge. Stile DaProd: viola notte, oro e magenta, vetro e Orbitron. |
| **Il menu di Windows 11** | Una piccola libreria in C++ mette il programma nel menu nuovo del tasto destro, non solo in «Mostra altre opzioni». |
| **L'avatar** | Modelli 3D creati con [Meshy](https://www.meshy.ai) e fotografati in animazione: nel programma ci sono solo le immagini, quindi pesa poco e va offline. |

### Come viene sviluppato

Ogni versione ha la sua voce nel [registro delle novità](CHANGELOG.md) e passa da **oltre 170 prove automatiche** che convertono file veri. Quando una versione è pronta, una pipeline su GitHub compila, impacchetta con **Velopack** e pubblica la release da sola. Il codice è aperto, con licenza [MIT](LICENSE).

<br>

<p align="center">
  <b><a href="docs/GUIDA.md">Guida completa</a></b> &nbsp;·&nbsp;
  <b><a href="CHANGELOG.md">Novità</a></b> &nbsp;·&nbsp;
  <b><a href="TERZE-PARTI.md">Software di terzi</a></b> &nbsp;·&nbsp;
  <b><a href="https://discord.gg/Njs52aacwq">Discord</a></b>
</p>

<p align="center"><sub>Un'app <b>DaProd Produzioni</b>, fatta con cura e in italiano.</sub></p>
