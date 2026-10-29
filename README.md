<div align="center">

<img src="risorse/schermate/banner.png" alt="DaP Convertitore — tasto destro, fatto." width="100%">

<br>

[![Scarica per Windows](https://img.shields.io/badge/%E2%AC%87%20%20SCARICA%20PER%20WINDOWS-ffd54a?style=for-the-badge&labelColor=ffd54a&color=ffd54a)](https://github.com/cammo22/DaP-Convertitore/releases/latest/download/DaPConvertitore-win-Setup.exe)
[![Portatile](https://img.shields.io/badge/VERSIONE%20PORTATILE-ZIP-35e8ff?style=for-the-badge&labelColor=1a1428)](https://github.com/cammo22/DaP-Convertitore/releases/latest/download/DaPConvertitore-win-Portable.zip)
![Linux](https://img.shields.io/badge/LINUX-IN%20ARRIVO-5a5570?style=for-the-badge&labelColor=1a1428)
![macOS](https://img.shields.io/badge/MACOS-IN%20ARRIVO-5a5570?style=for-the-badge&labelColor=1a1428)

[![versione](https://img.shields.io/github/v/release/cammo22/DaP-Convertitore?label=versione&color=ffd54a&labelColor=1a1428&style=flat-square)](https://github.com/cammo22/DaP-Convertitore/releases/latest)
![offline](https://img.shields.io/badge/100%25-offline-5dffb4?labelColor=1a1428&style=flat-square)
![gratis](https://img.shields.io/badge/gratis-e%20senza%20pubblicit%C3%A0-ff3df2?labelColor=1a1428&style=flat-square)
[![Discord](https://img.shields.io/badge/Discord-DaProd-5865F2?logo=discord&logoColor=white&style=flat-square)](https://discord.gg/Njs52aacwq)

</div>

<br>

**DaP Convertitore** è un convertitore di file per Windows. Clicchi con il tasto destro su un file, scegli il formato, e il file nuovo compare accanto all'originale come `nome (convertito)`. Video, audio, foto, PDF, documenti e archivi: tutto lavora sul tuo computer, senza account e senza internet.

<br>

## Come si presenta

<p align="center"><img src="risorse/schermate/home.png" alt="La home di DaP Convertitore" width="100%"></p>
<p align="center"><sub>La home: le sei strade (video, audio, foto, PDF, documenti, archivi), gli ultimi file convertiti e lo stato del PC.</sub></p>

<p align="center"><img src="risorse/schermate/lettore-video.png" alt="Il lettore con l'anteprima del video" width="100%"></p>
<p align="center"><sub>Un lettore per tutto. Sui video passi il cursore sulla linea del tempo e vedi il fotogramma di quel punto, fluido come in QuickTime.</sub></p>

<p align="center"><img src="risorse/schermate/lettore-archivio.png" alt="Un archivio aperto, con le miniature" width="100%"></p>
<p align="center"><sub>Gli archivi si aprono come cartelle, con le miniature di foto e video. Dentro c'è 7-Zip vero.</sub></p>

<p align="center"><img src="risorse/schermate/compressione.png" alt="Lo slider della compressione" width="100%"></p>
<p align="center"><sub>Per comprimere c'è uno slider, dal «solo archivia» al massimo, e se vuoi la password.</sub></p>

<br>

## Cosa sa fare

- **Converte quasi tutto.** Video (MP4, MKV, WEBM, GIF…), audio (MP3, FLAC, OPUS…), foto (anche HEIC dell'iPhone e RAW), PDF, Word, Excel, PowerPoint, testi, archivi. Con lo **slider del peso** scegli quanto deve pesare il file finale e vedi la stima prima di partire.
- **Usa la scheda video.** Con una NVIDIA i video si fanno con NVENC, anche in AV1, molto più in fretta.
- **Il lettore.** Foto, video, musica, PDF, testi e codice si aprono con un doppio clic, con anteprima, scaletta, ricerca e le modifiche semplici (girare e ritoccare le foto).
- **Gli archivi.** Apre 7Z, ZIP, RAR, TAR; crea 7Z e ZIP con la password; trascini fuori solo le voci che ti servono.
- **Il tasto destro giusto.** Nel menu nuovo di Windows 11 e in quello classico. Il pulsante **Aggiusta tutto** sistema il menu e le app predefinite, senza giri strani nelle impostazioni.
- **Si aggiorna da solo** e si installa per il tuo utente, senza amministratore.

<br>

## L'avatar

Un piccolo **oggetto DaProd** che vive sul desktop: una musicassetta che ogni tanto saluta e ogni qualche minuto si trasforma in un VHS, un floppy, un CD, un vinile… **Ci trascini sopra un file** e lo apre, lo converte o lo comprime; la musica parte subito con il suo piccolo player. Con la rotella lo fai grande o piccolo. Sta fermo quando guardi un video o giochi a schermo intero. Si accende da **Impostazioni → Avatar**.

<p align="center"><img src="risorse/schermate/avatar.png" alt="L'avatar sul desktop" width="80%"></p>

<br>

## Com'è fatto

Il programma è in due pezzi, e questo lo rende veloce e semplice da mantenere:

- **Il motore** (`src/DaPConvertitore.Motore`) è scritto in **.NET 10**, senza finestre: fa tutto il lavoro. Un solo catalogo dice cosa si converte in cosa, e lo leggono l'interfaccia, il menu del tasto destro e le prove. Sotto ci sono **FFmpeg** per i video e l'audio, **7-Zip** per gli archivi e il motore immagini di Windows per le foto.
- **L'interfaccia** (`ui/`) è una pagina in **TypeScript** dentro una finestra **WPF** con **WebView2** (il motore di Edge). È nello stile DaProd: viola notte, oro e magenta, vetro e Orbitron.
- **Il menu di Windows 11** è una piccola libreria in C++ (`menu/`) che mette il programma nel menu del tasto destro nuovo.
- **L'avatar** è fatto di modelli 3D creati con [Meshy](https://www.meshy.ai) e fotografati in animazione: nel programma ci sono solo le immagini, quindi pesa poco e va offline.

### Come viene sviluppato

Ogni versione ha una voce nel [registro delle novità](CHANGELOG.md) e oltre **170 prove automatiche** che convertono file veri. Quando una versione è pronta, una pipeline su GitHub compila, impacchetta con **Velopack** e pubblica la release da sola. Il codice è aperto, con licenza [MIT](LICENSE).

<br>

## Installare

| | |
| :-: | --- |
| **1** | Premi **Scarica per Windows** in cima a questa pagina. |
| **2** | Apri il file scaricato: si installa da solo. |
| **3** | Tasto destro su un file → **DaP Convertitore**. |

> Windows può dire «Windows ha protetto il PC», perché l'installatore non ha ancora la firma a pagamento. Premi **Ulteriori informazioni → Esegui comunque**.

<br>

<div align="center">

### Un'app DaProd Produzioni

**[Guida completa](docs/GUIDA.md)** · **[Novità](CHANGELOG.md)** · **[Software di terzi](TERZE-PARTI.md)** · **[Discord](https://discord.gg/Njs52aacwq)**

<a href="https://discord.gg/Njs52aacwq"><img src=".github/discord-card.svg" alt="Entra nella community DaProd su Discord" width="640"></a>

<sub>Fatto con cura, in italiano, da DaProd.</sub>

</div>
