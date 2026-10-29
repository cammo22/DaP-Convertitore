using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DaP.Convertitore.Lettore;

namespace DaP.Convertitore;

/// <summary>
/// Il 7-Zip vero (7z.exe + 7z.dll, viaggiano con l'app) per tutti gli archivi: ZIP, 7Z, RAR, TAR, GZ, XZ, ZST, CAB, ISO…
/// Legge e scrive coi nomi in UTF-8 (gli accenti arrivano giusti) e apre anche quello che il tar di Windows non sa aprire.
/// </summary>
public static partial class SetteZip
{
    /// <summary>Una password finta: se l'archivio è cifrato 7-Zip risponde «password sbagliata» invece di restare ad aspettarla.</summary>
    const string PasswordFinta = "-pDaP-senza-password";

    static readonly string[] Comuni = ["-y", "-bb0", "-sccUTF-8", "-scsUTF-8"];

    public static void Controlla(Strumenti s)
    {
        if (!s.HaSetteZip) throw new ErroreConversione("Manca 7-Zip dentro il programma: reinstalla DaP Convertitore.");
    }

    [GeneratedRegex(@"(\d{1,3})%")]
    private static partial Regex Percento();

    static Action<string>? LeggiPercento(Action<double>? avanzamento) =>
        avanzamento is null ? null : riga =>
        {
            var m = Percento().Match(riga);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var p)) avanzamento(Math.Clamp(p / 100.0, 0, 1));
        };

    static ErroreConversione Errore(EsitoProcesso r, string predefinito)
    {
        var t = r.Errori + "\n" + r.Uscita;
        if (t.Contains("Wrong password", StringComparison.OrdinalIgnoreCase) || t.Contains("encrypted", StringComparison.OrdinalIgnoreCase))
            return new ErroreConversione("L'archivio è protetto da password.", t);
        if (t.Contains("Cannot open the file as archive", StringComparison.OrdinalIgnoreCase) || t.Contains("Can not open the file as archive", StringComparison.OrdinalIgnoreCase))
            return new ErroreConversione("Questo archivio non si riesce ad aprire.", t);
        if (t.Contains("Unsupported Method", StringComparison.OrdinalIgnoreCase))
            return new ErroreConversione("Questo archivio usa un metodo di compressione che 7-Zip non conosce.", t);
        if (t.Contains("There is not enough space", StringComparison.OrdinalIgnoreCase) || t.Contains("No space left", StringComparison.OrdinalIgnoreCase))
            return new ErroreConversione("Non c'è abbastanza spazio sul disco.", t);
        return new ErroreConversione(predefinito, t);
    }

    // ————————————————————————— cosa c'è dentro —————————————————————————

    /// <summary>Le voci dell'archivio senza estrarlo. I percorsi hanno «/», le cartelle sono segnate.</summary>
    public static async Task<List<VoceArchivio>> Elenca(Strumenti s, string archivio, CancellationToken ct = default)
    {
        Controlla(s);
        var r = await Processi.Esegui(s.SetteZip!, ["l", "-slt", "-ba", "-spd", .. Comuni, PasswordFinta, "--", Path.GetFullPath(archivio)], ct, silenzioso: true);
        if (r.Codice >= 2 && r.Uscita.Length == 0) throw Errore(r, "Questo archivio non si riesce ad aprire.");
        var voci = Analizza(r.Uscita);
        // .tar.gz e simili: 7-Zip vede solo il .tar dentro, e si guarda dentro anche a quello
        // (il .xz non porta il nome del .tar: 7-Zip non ne scrive nessuno)
        if (EDiTarCompresso(archivio) && (voci.Count == 0 || (voci.Count == 1 && !voci[0].Cartella && voci[0].Percorso.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))))
            return await ElencaTarCompresso(s, archivio, ct);
        return voci;
    }

    static bool EDiTarCompresso(string archivio)
    {
        var n = archivio.ToLowerInvariant();
        return n.EndsWith(".tgz") || n.EndsWith(".tbz2") || n.EndsWith(".txz") || n.EndsWith(".tzst")
            || n.EndsWith(".tar.gz") || n.EndsWith(".tar.bz2") || n.EndsWith(".tar.xz") || n.EndsWith(".tar.zst");
    }

    /// <summary>Il .tar compresso: la lista del .tar interno si ha mettendo in fila i due 7-Zip (l'uscita del primo è l'ingresso del secondo).</summary>
    static async Task<List<VoceArchivio>> ElencaTarCompresso(Strumenti s, string archivio, CancellationToken ct)
    {
        var cartella = Path.Combine(Strumenti.CartellaTemporanea, "tar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cartella);
        try
        {
            await Estrai(s, archivio, cartella, null, ct, apriIlTar: false);
            var tar = Directory.EnumerateFiles(cartella, "*.tar").FirstOrDefault();
            if (tar is null) return [];
            var r = await Processi.Esegui(s.SetteZip!, ["l", "-slt", "-ba", "-spd", .. Comuni, PasswordFinta, "--", tar], ct, silenzioso: true);
            return Analizza(r.Uscita);
        }
        finally { try { Directory.Delete(cartella, true); } catch { } }
    }

    /// <summary>L'uscita di «7z l -slt -ba»: blocchi di righe «Chiave = valore» separati da una riga vuota.</summary>
    public static List<VoceArchivio> Analizza(string uscita)
    {
        var l = new List<VoceArchivio>();
        string? percorso = null; long peso = 0; var cartella = false; DateTime? data = null; var attributi = "";
        void Chiudi()
        {
            if (percorso is not null)
            {
                var p = percorso.Replace('\\', '/');
                var eCartella = cartella || attributi.StartsWith('D') || p.EndsWith('/');
                if (eCartella && !p.EndsWith('/')) p += "/";
                l.Add(new VoceArchivio(p, eCartella ? 0 : peso, eCartella, data));
            }
            percorso = null; peso = 0; cartella = false; data = null; attributi = "";
        }
        foreach (var grezza in uscita.Split('\n'))
        {
            var riga = grezza.TrimEnd('\r');
            if (riga.Length == 0) { Chiudi(); continue; }
            var i = riga.IndexOf(" = ", StringComparison.Ordinal);
            if (i < 0) continue;
            var k = riga[..i]; var v = riga[(i + 3)..];
            switch (k)
            {
                case "Path": if (percorso is not null) Chiudi(); percorso = v; break;
                case "Size": long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out peso); break;
                case "Folder": cartella = v == "+"; break;
                case "Attributes": attributi = v; break;
                case "Modified":
                    if (DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var d) && d.Year > 1980) data = d;
                    break;
            }
        }
        Chiudi();
        return l;
    }

    // ————————————————————————— estrarre —————————————————————————

    /// <summary>
    /// Estrae in <paramref name="dest"/>: tutto, o solo le <paramref name="voci"/> (percorsi dentro l'archivio, con «/»;
    /// una cartella porta con sé quello che ha dentro). Nei .tar.gz e simili passa dal .tar in mezzo.
    /// </summary>
    public static async Task Estrai(Strumenti s, string archivio, string dest, IReadOnlyList<string>? voci, CancellationToken ct,
        Action<double>? avanzamento = null, bool apriIlTar = true)
    {
        Controlla(s);
        Directory.CreateDirectory(dest);
        if (!EDiTarCompresso(archivio))
        {
            var r = await Lancia(s, archivio, dest, voci, ct, avanzamento);
            if (r.Codice >= 2) throw Errore(r, "Non sono riuscito a estrarre l'archivio.");
            return;
        }

        // un .tar.gz è un .tar dentro un altro archivio: prima si tira fuori il .tar, poi si apre quello
        var appoggio = Path.Combine(Strumenti.CartellaTemporanea, "tar-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(appoggio);
            var r1 = await Lancia(s, archivio, appoggio, null, ct, p => avanzamento?.Invoke(p * 0.5));
            if (r1.Codice >= 2) throw Errore(r1, "Non sono riuscito a estrarre l'archivio.");
            var tar = Directory.EnumerateFiles(appoggio, "*.tar").FirstOrDefault();
            if (tar is null) throw new ErroreConversione("Questo archivio non si riesce ad aprire.", r1.Uscita);
            if (!apriIlTar) { File.Move(tar, Path.Combine(dest, Path.GetFileName(tar)), true); return; }
            var r2 = await Lancia(s, tar, dest, voci, ct, p => avanzamento?.Invoke(0.5 + p * 0.5));
            if (r2.Codice >= 2) throw Errore(r2, "Non sono riuscito a estrarre l'archivio.");
        }
        finally { try { Directory.Delete(appoggio, true); } catch { } }
    }

    static Task<EsitoProcesso> Lancia(Strumenti s, string archivio, string dest, IReadOnlyList<string>? voci, CancellationToken ct, Action<double>? avanzamento)
    {
        List<string> a = ["x", "-aoa", "-bsp1", "-spd", .. Comuni, PasswordFinta, "-o" + dest, "--", Path.GetFullPath(archivio)];
        if (voci is not null)
            a.AddRange(voci.Select(v => v.Replace('\\', '/').Trim('/')).Where(v => v.Length > 0).Distinct(StringComparer.Ordinal));
        return Processi.Esegui(s.SetteZip!, a, ct, LeggiPercento(avanzamento), tieniUscita: true, cartella: dest);
    }

    // ————————————————————————— creare —————————————————————————

    /// <summary>
    /// Mette <paramref name="voci"/> (nomi relativi a <paramref name="base_"/>, file o cartelle) in un archivio.
    /// <paramref name="formato"/> è zip, 7z, tgz o txz. <paramref name="livello"/>: -1 solo archiviare, 0 deciso da solo, 1…9.
    /// </summary>
    public static async Task Crea(Strumenti s, string archivio, string base_, IReadOnlyList<string> voci, string formato, int livello,
        CancellationToken ct, Action<double>? avanzamento = null)
    {
        Controlla(s);
        archivio = Path.GetFullPath(archivio);
        if (File.Exists(archivio)) File.Delete(archivio);
        var elenco = Path.Combine(Strumenti.CartellaTemporanea, "elenco-" + Guid.NewGuid().ToString("N") + ".txt");
        var lavoro = Path.Combine(Strumenti.CartellaTemporanea, "arch-" + Guid.NewGuid().ToString("N"));
        // l'elenco dei nomi sta in un file (UTF-8): niente limiti di lunghezza, niente nomi scambiati per opzioni.
        // (dopo «--» il «@» non vale più, quindi qui niente «--»: l'archivio e il file sono percorsi interi)
        await File.WriteAllLinesAsync(elenco, voci, new UTF8Encoding(false), ct);
        try
        {
            if (formato is "tgz" or "txz")
            {
                Directory.CreateDirectory(lavoro);
                var radice = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(Path.GetFileName(archivio))).TrimStart('.', '~');
                var tar = Path.Combine(lavoro, (radice.Length > 0 ? radice : "archivio") + ".tar");
                var r1 = await Processi.Esegui(s.SetteZip!, ["a", "-ttar", "-spd", "-bsp1", .. Comuni, tar, "@" + elenco], ct,
                    LeggiPercento(p => avanzamento?.Invoke(p * 0.5)), tieniUscita: true, cartella: base_);
                if (r1.Codice >= 2 || !File.Exists(tar)) throw Errore(r1, "Non sono riuscito a creare l'archivio.");
                var r2 = await Processi.Esegui(s.SetteZip!, ["a", formato == "tgz" ? "-tgzip" : "-txz", "-bsp1", .. Comuni, .. Livelli(formato, livello), archivio, tar], ct,
                    LeggiPercento(p => avanzamento?.Invoke(0.5 + p * 0.5)), tieniUscita: true, cartella: lavoro);
                if (r2.Codice >= 2 || !File.Exists(archivio)) throw Errore(r2, "Non sono riuscito a creare l'archivio.");
                return;
            }
            var tipo = formato == "zip" ? "-tzip" : "-t7z";
            var r = await Processi.Esegui(s.SetteZip!, ["a", tipo, "-spd", "-bsp1", .. Comuni, .. Livelli(formato, livello), archivio, "@" + elenco], ct,
                LeggiPercento(avanzamento), tieniUscita: true, cartella: base_);
            if (r.Codice >= 2 || !File.Exists(archivio)) throw Errore(r, "Non sono riuscito a creare l'archivio.");
        }
        finally
        {
            try { File.Delete(elenco); } catch { }
            try { if (Directory.Exists(lavoro)) Directory.Delete(lavoro, true); } catch { }
        }
    }

    /// <summary>Il livello della slider → opzioni di 7-Zip. Il massimo dello ZIP usa il Deflate più paziente; il 7Z, LZMA2 con dizionario grande.</summary>
    public static string[] Livelli(string formato, int livello)
    {
        var mx = LivelloVero(formato, livello);
        return formato switch
        {
            "zip" => mx == 0 ? ["-mx=0", "-mcu=on"] : mx >= 9 ? ["-mx=9", "-mm=Deflate", "-mfb=258", "-mpass=15", "-mcu=on"] : [$"-mx={mx}", "-mm=Deflate", "-mcu=on"],
            "7z" => mx == 0 ? ["-mx=0"] : [$"-mx={mx}", "-m0=LZMA2", "-mmt=on"],
            _ => [$"-mx={Math.Max(1, mx)}"],
        };
    }

    /// <summary>-1 = solo archiviare (0), 0 = il livello di partenza del formato.</summary>
    public static int LivelloVero(string formato, int livello) =>
        livello < 0 ? 0 : livello > 0 ? Math.Min(9, livello) : formato switch { "zip" => 5, _ => 7 };
}
