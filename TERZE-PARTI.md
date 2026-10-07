# Programmi di altri dentro DaP Convertitore

Il codice di DaP Convertitore è MIT (vedi `LICENSE`). L'installer porta con sé anche questi pezzi, ognuno con la
sua licenza.

| Cosa | Per cosa | Licenza | Dove |
| --- | --- | --- | --- |
| **FFmpeg 9.0** (build GPL "shared" di [BtbN](https://github.com/BtbN/FFmpeg-Builds)) | video, audio, immagini, sottotitoli | GPL v3 (con x264, x265, SVT-AV1, libaom, libvpx, libwebp, libjxl, librsvg, LAME, Opus, Vorbis…) | `motori\ffmpeg\` nell'installazione, licenza in `LICENSE-FFmpeg.txt`; sorgenti su [ffmpeg.org](https://ffmpeg.org/download.html) e [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds) |
| [PDFsharp](https://github.com/empira/PDFsharp) | unire PDF, foto in un PDF | MIT | |
| [PdfPig](https://github.com/UglyToad/PdfPig) | leggere il testo dei PDF | Apache 2.0 | |
| [Markdig](https://github.com/xoofx/markdig) | Markdown → pagina | BSD 2-Clause | |
| [MiniExcel](https://github.com/mini-software/MiniExcel) | leggere e scrivere Excel | Apache 2.0 | |
| [Velopack](https://github.com/velopack/velopack) | installazione e aggiornamenti | MIT | |
| [Microsoft WebView2](https://developer.microsoft.com/microsoft-edge/webview2/) | l'interfaccia | licenza Microsoft (redistribuibile) | |
| [Orbitron](https://fonts.google.com/specimen/Orbitron), [Rajdhani](https://fonts.google.com/specimen/Rajdhani) | caratteri | SIL OFL 1.1 | |
| [DSEG7](https://github.com/keshikan/DSEG) | i numeri a sette segmenti | SIL OFL 1.1 | `ui\src\font\DSEG-LICENSE.txt` |
| libc++ e runtime mingw-w64 (dentro `DaPConvertitore.Menu.dll`, compilata con [Zig](https://ziglang.org)) | il menu di Windows 11 | Apache 2.0 con eccezione LLVM · licenze permissive mingw-w64 | |

FFmpeg è un programma a parte, lanciato come processo: DaP Convertitore non lo modifica e non ci si collega.
Le altre cose che usa sono già in Windows (WIC per le immagini, Windows.Data.Pdf, OCR di Windows, `tar.exe`) o
sul tuo PC se le hai installate tu (LibreOffice, Microsoft Office).
