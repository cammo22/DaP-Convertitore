using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using DaP.Convertitore.Lettore;

namespace DaP.Convertitore.App;

/// <summary>
/// Il ponte fra la pagina del lettore e il motore. Stesso modo di parlare del convertitore ({id, cmd, args} e
/// risposta con lo stesso id), comandi suoi: cosa c'è nel file, i file accanto, e le cose da fare col file.
/// </summary>
public sealed class PonteLettore
{
    readonly FinestraLettore finestra;
    readonly WebView2 vista;
    readonly Strumenti strumenti = Strumenti.Trova();
    readonly Risorse risorse;
    Task<InfoHardware>? hardware;
    List<string> fratelli = [];
    string attuale;
    bool pronta;
    readonly Dictionary<string, SchedaMedia> schede = new(StringComparer.OrdinalIgnoreCase);

    static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);

    public PonteLettore(FinestraLettore finestra, WebView2 vista, IReadOnlyList<string> file)
    {
        this.finestra = finestra;
        this.vista = vista;
        risorse = new Risorse(finestra.Dispatcher);
        attuale = file[0];
        // più file scelti insieme: si scorrono quelli; uno solo: si scorre la sua cartella
        fratelli = file.Count > 1 ? [.. file] : [];
    }

    public void Collega(CoreWebView2Environment env)
    {
        risorse.Collega(vista.CoreWebView2, env);
        vista.CoreWebView2.WebMessageReceived += Ricevi;
    }

    public void Chiudi() => risorse.Ferma();

    /// <summary>Un altro file da Esplora file, mentre il lettore è già aperto.</summary>
    public void Apri(IReadOnlyList<string> file)
    {
        fratelli = file.Count > 1 ? [.. file] : [];
        attuale = file[0];
        risorse.Ferma();
        if (pronta) Manda("apri", Stato());
    }

    // ————————————————————————— messaggi dalla pagina —————————————————————————

    async void Ricevi(object? _, CoreWebView2WebMessageReceivedEventArgs e)
    {
        JsonNode? m;
        try { m = JsonNode.Parse(e.WebMessageAsJson); }
        catch { return; }
        if (m is null) return;
        var cmd = (string?)m["cmd"] ?? "";
        var id = m["id"]?.GetValue<int>();
        var args = m["args"] as JsonObject ?? [];
        if (cmd == "lasciati")
        {
            var percorsi = e.AdditionalObjects?.OfType<CoreWebView2File>().Select(f => f.Path).Where(File.Exists).ToList() ?? [];
            if (percorsi.Count > 0) Apri(percorsi);
            return;
        }
        try
        {
            var dati = await Esegui(cmd, args);
            if (id is not null) Rispondi(id.Value, true, dati, null);
        }
        catch (Exception ex)
        {
            if (ex is not ErroreConversione) Registro.Errore($"lettore {cmd}", ex);
            if (id is not null) Rispondi(id.Value, false, null, ex.Message);
        }
    }

    string P(JsonObject a) => (string?)a["percorso"] is { } p && File.Exists(p) ? p : attuale;

    async Task<object?> Esegui(string cmd, JsonObject a)
    {
        switch (cmd)
        {
            case "stato":
                pronta = true;
                _ = Task.Run(async () =>
                {
                    var v = await Aggiornamenti.PreparaUnaVolta();
                    if (v is not null) Manda("aggiornamento", new { versione = v });
                });
                return Stato();
            case "vai":
            {
                var p = (string?)a["percorso"];
                if (p is null || !File.Exists(p)) throw new ErroreConversione("Il file non c'è più.");
                attuale = p;
                risorse.Ferma();
                finestra.Title = Path.GetFileName(p) + " · DaP Convertitore";
                return Scheda(p);
            }
            case "fratelli":
                return await Fratelli();

            // ——— video e musica ———
            case "media":
            {
                var p = P(a);
                var s = await Scheda_(p);
                var i = s.Info;
                var copertina = i.Video is null ? await Media.Copertina(strumenti, p, i.Copertina) : null;
                return new
                {
                    durata = i.Durata, contenitore = i.Contenitore, bitrate = i.Bitrate,
                    video = i.Video is { } v ? new { v.Codec, v.Larghezza, v.Altezza, v.Fps, v.Hdr, v.Bit, v.PixFmt } : null,
                    audio = s.Audio.Select((x, n) => new
                    {
                        x.Indice, x.Lingua, x.Titolo, x.Codec,
                        canali = n < i.Audio.Count ? i.Audio[n].Canali : 0,
                        campionamento = n < i.Audio.Count ? i.Audio[n].Campionamento : 0,
                        bitrate = n < i.Audio.Count ? i.Audio[n].Bitrate : 0,
                    }),
                    sottotitoli = s.Sottotitoli.Select(x => new { x.Indice, x.Lingua, x.Titolo, x.Codec, x.Testo }),
                    esterni = Media.SottotitoliAccanto(p).Select(f => new { nome = Path.GetFileName(f), percorso = f }),
                    tag = s.Tag,
                    capitoli = s.Capitoli.Select(c => new { inizio = double.Parse(c.Inizio, CultureInfo.InvariantCulture), titolo = c.Titolo }),
                    copertina = copertina is null ? null : $"data:image/jpeg;base64,{Convert.ToBase64String(copertina)}",
                };
            }
            case "audioCache":
            {
                var f = await Media.AudioInCache(strumenti, P(a), a["traccia"]?.GetValue<int>() ?? 0);
                return risorse.Url(f);
            }
            case "flusso":
            {
                var p = P(a);
                var s = await Scheda_(p);
                risorse.Ferma();
                hardware ??= Task.Run(() => Hardware.Rileva(strumenti));
                var f = Flusso.Avvia(strumenti, await hardware, p, s.Info, a["da"]?.GetValue<double>() ?? 0, a["traccia"]?.GetValue<int>() ?? 0, a["copia"]?.GetValue<bool>() == true);
                risorse.Aggiungi(f);
                return new { url = $"https://{Risorse.Host}/flusso/{f.Id}/", mime = f.Mime, da = f.Da };
            }
            case "fermaFlusso":
                risorse.Ferma();
                return true;
            case "onda":
                return await Media.Onda(strumenti, P(a), a["fette"]?.GetValue<int>() ?? 1600, a["traccia"]?.GetValue<int>() ?? 0);
            case "vtt":
                return (string?)a["file"] is { } sub && File.Exists(sub)
                    ? await Media.Vtt(strumenti, sub, null)
                    : await Media.Vtt(strumenti, P(a), a["traccia"]?.GetValue<int>() ?? 0);

            // ——— foto ———
            case "immagine":
            {
                var p = P(a);
                var vera = Contenuti.FotoNativa(p) ? p : await Contenuti.FotoVisibile(strumenti, p);
                return new { url = risorse.Url(vera), convertita = vera != p };
            }
            case "exif":
                return (await Contenuti.Exif(P(a))).Select(x => new { x.Nome, x.Valore });
            case "sfondo":
            {
                var p = P(a);
                var vera = Contenuti.FotoNativa(p) && Path.GetExtension(p).ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".bmp" ? p : await Contenuti.FotoVisibile(strumenti, p);
                // una copia nostra: se sposti o cancelli la foto, lo sfondo resta
                var copia = Path.Combine(Strumenti.CartellaDati, "sfondo" + Path.GetExtension(vera));
                File.Copy(vera, copia, true);
                if (!SystemParametersInfo(0x0014, 0, copia, 0x01 | 0x02)) throw new ErroreConversione("Windows non ha cambiato lo sfondo.");
                return true;
            }

            // ——— carta ———
            case "pdf":
            {
                var p = P(a);
                var pagine = await Carte.Pagine(p);
                return new { radice = $"https://{Risorse.Host}/pdf/{risorse.Gettone(p)}/", pagine = pagine.Select(x => new { x.w, x.h }) };
            }
            case "impagina":
            {
                var p = P(a);
                var pdf = await Carte.InPdf(strumenti, p);
                var pagine = await Carte.Pagine(pdf);
                return new { radice = $"https://{Risorse.Host}/pdf/{risorse.Gettone(pdf)}/", pagine = pagine.Select(x => new { x.w, x.h }) };
            }
            case "docx":
                return new { html = await Task.Run(() => Carte.Docx(P(a))) };
            case "pptx":
                return (await Task.Run(() => Carte.Pptx(P(a)))).Select(d => new { d.Titolo, d.Punti });
            case "testo":
            {
                var (t, troncato) = await Task.Run(() => Contenuti.Testo(P(a)));
                return new { testo = t, troncato };
            }
            case "markdown":
            {
                var (t, troncato) = await Task.Run(() => Contenuti.Testo(P(a)));
                return new { html = Contenuti.Markdown(t), testo = t, troncato };
            }
            case "tabella":
            {
                var t = await Contenuti.Tabella(strumenti, P(a), (string?)a["foglio"]);
                return new { t.Fogli, t.Attivo, t.Colonne, t.Righe, t.Totale };
            }
            case "archivio":
                return (await Contenuti.Archivio(strumenti, P(a))).Select(v => new { v.Percorso, v.Peso, v.Cartella, v.Data });
            case "byte":
            {
                var da = a["da"]?.GetValue<long>() ?? 0;
                var b = Contenuti.Byte(P(a), da, Math.Clamp(a["quanti"]?.GetValue<int>() ?? 65536, 16, 1 << 20));
                return Convert.ToBase64String(b);
            }
            case "impronta":
                return await Contenuti.Impronta(P(a));
            case "font":
            {
                var p = P(a);
                return new { url = risorse.Url(p), installato = FontInstallato(p) };
            }
            case "installaFont":
                InstallaFont(P(a));
                return true;
            case "url":
                return risorse.Url(P(a));

            // ——— cose da fare col file ———
            case "converti":
                Regia.Converti(P(a));
                return true;
            case "estrai":
                Regia.Gestisci(new Richiesta("arch-estrai", [P(a)], false));
                return true;
            case "apriCon":
                Ambiente.ApriConAltro(finestra.Maniglia, P(a));
                return true;
            case "mostra":
                Programma.Mostra(P(a));
                return true;
            case "cestino":
            {
                var p = P(a);
                risorse.Ferma();
                Carte.Dimentica(p);
                var perche = Cestino.Sposta(p);
                if (perche is not null) throw new ErroreConversione("Non è andato nel Cestino: " + perche + ".");
                fratelli.RemoveAll(f => string.Equals(f, p, StringComparison.OrdinalIgnoreCase));
                return true;
            }
            case "finestra":
                switch ((string?)a["azione"])
                {
                    case "chiudi": finestra.Close(); break;
                    case "riduci": finestra.WindowState = WindowState.Minimized; break;
                    case "massimizza": finestra.Massimizza(); break;
                }
                return true;
            // ——— le piccole modifiche, sul file vero ———
            case "ruotaFoto":
            {
                var p = P(a);
                await Modifiche.RuotaFoto(p, a["gradi"]?.GetValue<int>() ?? 0, a["specchio"]?.GetValue<bool>() == true);
                risorse.Ferma();
                return true;
            }
            case "ruotaVideo":
            {
                var p = P(a);
                risorse.Ferma();
                await Modifiche.RuotaVideo(strumenti, p, a["gradi"]?.GetValue<int>() ?? 0);
                schede.Remove(p);
                return true;
            }
            case "taglia":
            {
                var p = P(a);
                var nuovo = await Modifiche.TagliaVideo(strumenti, p, a["da"]!.GetValue<double>(), a["a"]!.GetValue<double>());
                return new { percorso = nuovo, nome = Path.GetFileName(nuovo) };
            }
            case "ruotaPdf":
            {
                var p = P(a);
                Carte.Dimentica(p);
                await Task.Run(() => Modifiche.RuotaPdf(p, a["pagina"]?.GetValue<int>() ?? -1, a["gradi"]?.GetValue<int>() ?? 90));
                Carte.Dimentica(p);
                return true;
            }
            case "rapida":
            {
                // la conversione al volo parte nella finestrella in basso a destra, senza lasciare il lettore
                var id = (string?)a["azione"] ?? "";
                if (Catalogo.TrovaRapida(id) is null) throw new ErroreConversione("Questa conversione non c'è.");
                Regia.Gestisci(new Richiesta(id, [P(a)], false));
                return true;
            }
            case "aggiorna":
                await Aggiornamenti.Installa();
                return true;
            case "fotogramma":
            {
                // il fotogramma del video in PNG, accanto al video: «Film (fotogramma 0.12.34).png»
                var p = P(a);
                var dati = (string?)a["png"] ?? "";
                var b = Convert.FromBase64String(dati[(dati.IndexOf(',') + 1)..]);
                var t = TimeSpan.FromSeconds(a["tempo"]?.GetValue<double>() ?? 0);
                var radice = Path.Combine(Path.GetDirectoryName(p)!, $"{Path.GetFileNameWithoutExtension(p)} (fotogramma {(int)t.TotalHours}.{t.Minutes:00}.{t.Seconds:00})");
                var uscita = radice + ".png";
                for (var n = 2; File.Exists(uscita); n++) uscita = $"{radice} {n}.png";
                await File.WriteAllBytesAsync(uscita, b);
                return uscita;
            }
            case "apriFile":
            {
                var d = new Microsoft.Win32.OpenFileDialog { Title = "Apri nel lettore", Multiselect = true };
                if (d.ShowDialog(finestra) == true) Apri(d.FileNames);
                return true;
            }
        }
        throw new InvalidOperationException($"Comando sconosciuto: {cmd}");
    }

    async Task<SchedaMedia> Scheda_(string p)
    {
        if (schede.TryGetValue(p, out var s)) return s;
        s = await Media.Leggi(strumenti, p);
        if (schede.Count > 50) schede.Clear();
        schede[p] = s;
        return s;
    }

    object Stato() => new
    {
        versione = Ponte.Versione,
        file = Scheda(attuale),
        ffmpeg = strumenti.HaFfmpeg,
        office = strumenti.LibreOffice is not null || strumenti.Word,
        aggiornamento = (string?)null,
    };

    object Scheda(string p)
    {
        var fi = new FileInfo(p);
        finestra.Title = fi.Name + " · DaP Convertitore";
        return new
        {
            percorso = p, nome = fi.Name, cartella = fi.DirectoryName, estensione = fi.Extension.ToLowerInvariant(),
            peso = fi.Exists ? fi.Length : 0, creato = fi.CreationTime, modificato = fi.LastWriteTime,
            tipo = Vista.Tipo(p), url = risorse.Url(p),
            categoria = Catalogo.Chiave(Catalogo.CategoriaDi(p)),
            // «Converti» solo per quello che diventa altro davvero (un font o un .bin si comprimono e basta)
            convertibile = Catalogo.CategoriaDi(p) is not Categoria.Altro && Catalogo.FormatiPer(p).Any(),
            tipoWindows = TipoWindows(p),
            // le conversioni al volo di questo tipo di file («Fai subito»)
            rapide = Catalogo.CategoriaDi(p) is Categoria.Altro ? [] : RapidePer(p),
            // se la rotazione si può salvare nel file, o il perché no
            nota = Vista.Tipo(p) switch { "immagine" => Modifiche.SalvaFoto(p), "video" => Modifiche.SalvaVideo(p), _ => null },
        };
    }

    static IEnumerable<object> RapidePer(string p)
    {
        var formati = Catalogo.FormatiPer(p).Select(f => f.Id).ToHashSet();
        return Catalogo.Rapide.Where(r => r.Categoria == Catalogo.CategoriaDi(p) && formati.Contains(r.Formato) && !r.Id.EndsWith("-unisci") && !r.Id.StartsWith("img-pdf"))
            .Select(r => (object)new { r.Id, r.Etichetta }).ToList();
    }

    /// <summary>I file da scorrere con le frecce: quelli scelti insieme, o la cartella. Arrivano dopo, non fanno aspettare.</summary>
    async Task<object> Fratelli()
    {
        var l = fratelli.Count > 1 ? fratelli : await Task.Run(() => Vista.Fratelli(attuale, StrCmpLogicalW).ToList());
        if (fratelli.Count <= 1) fratelli = l;
        return new { file = l, indice = l.FindIndex(f => string.Equals(f, attuale, StringComparison.OrdinalIgnoreCase)) };
    }

    // ————————————————————————— font —————————————————————————

    static string CartellaFont => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");

    static bool FontInstallato(string p) =>
        File.Exists(Path.Combine(CartellaFont, Path.GetFileName(p))) || File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), Path.GetFileName(p)));

    /// <summary>Come «Installa» di Windows, solo per te (senza permessi da amministratore).</summary>
    static void InstallaFont(string p)
    {
        if (Path.GetExtension(p).ToLowerInvariant() is not (".ttf" or ".otf")) throw new ErroreConversione("Windows installa solo TTF e OTF.");
        Directory.CreateDirectory(CartellaFont);
        var dest = Path.Combine(CartellaFont, Path.GetFileName(p));
        File.Copy(p, dest, true);
        using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\Fonts"))
            k.SetValue(Path.GetFileNameWithoutExtension(p) + (p.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) ? " (OpenType)" : " (TrueType)"), dest);
        AddFontResource(dest);
        SendMessageTimeout(new IntPtr(0xFFFF), 0x001D, IntPtr.Zero, IntPtr.Zero, 0x0002, 1000, out _); // WM_FONTCHANGE a tutti
    }

    // ————————————————————————— verso la pagina —————————————————————————

    void Manda(string evento, object? dati)
    {
        var testo = JsonSerializer.Serialize(new { evento, dati }, json);
        finestra.Dispatcher.BeginInvoke(() => { try { vista.CoreWebView2?.PostWebMessageAsJson(testo); } catch { } });
    }

    void Rispondi(int id, bool ok, object? dati, string? errore)
    {
        var testo = JsonSerializer.Serialize(new { id, ok, dati, errore }, json);
        try { vista.CoreWebView2?.PostWebMessageAsJson(testo); } catch { }
    }

    /// <summary>Il nome del tipo come lo dice Esplora file («Documento di testo», «File MKV»…).</summary>
    static string TipoWindows(string p)
    {
        var i = new SHFILEINFO();
        return SHGetFileInfo(Path.GetExtension(p), 0x80, ref i, (uint)Marshal.SizeOf<SHFILEINFO>(), 0x400 | 0x10) != IntPtr.Zero ? i.szTypeName : "";
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct SHFILEINFO
    {
        public IntPtr hIcon; public int iIcon; public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path, uint attr, ref SHFILEINFO info, uint size, uint flags);
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)] static extern int StrCmpLogicalW(string a, string b);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool SystemParametersInfo(uint azione, uint p, string v, uint flag);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] static extern int AddFontResource(string file);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr r);
}
