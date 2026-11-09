using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace DaP.Convertitore.Lettore;

/// <summary>Quello che torna dopo aver messo una copertina: i byte da mostrare, dove è finita accanto, se è entrata nel file.</summary>
public sealed record EsitoCopertina(byte[]? Immagine, string? Accanto, bool Dentro, string? Nota)
{
    /// <summary>Per la pagina: JPEG o PNG, si capisce dai primi byte.</summary>
    public string? ComeDataUrl()
    {
        if (Immagine is not { Length: > 8 }) return null;
        var png = Immagine[0] == 137 && Immagine[1] == 80;
        return "data:" + (png ? "image/png" : "image/jpeg") + ";base64," + Convert.ToBase64String(Immagine);
    }
}

/// <summary>
/// La copertina di un brano. Si salva accanto al file (mai sopra un file che c'è già) e, per MP3, M4A e FLAC,
/// si scrive dentro: una bozza accanto, poi prende il posto dell'originale. Se qualcosa va storto l'originale resta.
/// Le varianti disegnate sono sempre le stesse per lo stesso titolo: l'anteprima e il file usano lo stesso disegno.
/// </summary>
public static class Copertine
{
    public const int Lato = 480;
    public const int Quante = 6;

    static readonly Dictionary<char, string> glifi = new()
    {
        ['A'] = G("01110", "10001", "10001", "11111", "10001", "10001", "10001"),
        ['B'] = G("11110", "10001", "10001", "11110", "10001", "10001", "11110"),
        ['C'] = G("01111", "10000", "10000", "10000", "10000", "10000", "01111"),
        ['D'] = G("11110", "10001", "10001", "10001", "10001", "10001", "11110"),
        ['E'] = G("11111", "10000", "10000", "11110", "10000", "10000", "11111"),
        ['F'] = G("11111", "10000", "10000", "11110", "10000", "10000", "10000"),
        ['G'] = G("01111", "10000", "10000", "10011", "10001", "10001", "01111"),
        ['H'] = G("10001", "10001", "10001", "11111", "10001", "10001", "10001"),
        ['I'] = G("11111", "00100", "00100", "00100", "00100", "00100", "11111"),
        ['J'] = G("00111", "00010", "00010", "00010", "10010", "10010", "01100"),
        ['K'] = G("10001", "10010", "10100", "11000", "10100", "10010", "10001"),
        ['L'] = G("10000", "10000", "10000", "10000", "10000", "10000", "11111"),
        ['M'] = G("10001", "11011", "10101", "10101", "10001", "10001", "10001"),
        ['N'] = G("10001", "11001", "10101", "10011", "10001", "10001", "10001"),
        ['O'] = G("01110", "10001", "10001", "10001", "10001", "10001", "01110"),
        ['P'] = G("11110", "10001", "10001", "11110", "10000", "10000", "10000"),
        ['Q'] = G("01110", "10001", "10001", "10001", "10101", "10010", "01101"),
        ['R'] = G("11110", "10001", "10001", "11110", "10100", "10010", "10001"),
        ['S'] = G("01111", "10000", "10000", "01110", "00001", "00001", "11110"),
        ['T'] = G("11111", "00100", "00100", "00100", "00100", "00100", "00100"),
        ['U'] = G("10001", "10001", "10001", "10001", "10001", "10001", "01110"),
        ['V'] = G("10001", "10001", "10001", "10001", "10001", "01010", "00100"),
        ['W'] = G("10001", "10001", "10001", "10101", "10101", "10101", "01010"),
        ['X'] = G("10001", "10001", "01010", "00100", "01010", "10001", "10001"),
        ['Y'] = G("10001", "10001", "01010", "00100", "00100", "00100", "00100"),
        ['Z'] = G("11111", "00001", "00010", "00100", "01000", "10000", "11111"),
        ['0'] = G("01110", "10001", "10011", "10101", "11001", "10001", "01110"),
        ['1'] = G("00100", "01100", "00100", "00100", "00100", "00100", "01110"),
        ['2'] = G("01110", "10001", "00001", "00010", "00100", "01000", "11111"),
        ['3'] = G("11110", "00001", "00001", "01110", "00001", "00001", "11110"),
        ['4'] = G("10010", "10010", "10010", "11111", "00010", "00010", "00010"),
        ['5'] = G("11111", "10000", "10000", "11110", "00001", "00001", "11110"),
        ['6'] = G("01110", "10000", "10000", "11110", "10001", "10001", "01110"),
        ['7'] = G("11111", "00001", "00010", "00100", "01000", "01000", "01000"),
        ['8'] = G("01110", "10001", "10001", "01110", "10001", "10001", "01110"),
        ['9'] = G("01110", "10001", "10001", "01111", "00001", "00001", "01110"),
    };

    static Copertine()
    {
        foreach (var (k, v) in glifi)
            if (v.Length != 35) throw new InvalidOperationException($"Glifo {k} lungo {v.Length}.");
    }

    static string G(params string[] righe) => string.Concat(righe);

    /// <summary>MP3, M4A e FLAC tengono la copertina dentro il file. Gli altri no.</summary>
    public static bool DentroIlFile(string percorso) =>
        Path.GetExtension(percorso).ToLowerInvariant() is ".mp3" or ".m4a" or ".flac";

    /// <summary>«Brano (copertina).jpg», poi «(copertina 2)», «(copertina 3)»… non si scrive mai sopra un file che c'è già.</summary>
    public static string NomeLibero(string cartella, string radice, string estensione)
    {
        if (!estensione.StartsWith('.')) estensione = "." + estensione;
        var base_ = string.IsNullOrWhiteSpace(radice) ? "copertina" : radice.Trim();
        for (var n = 1; ; n++)
        {
            var etichetta = n == 1 ? "copertina" : $"copertina {n}";
            var candidato = Path.Combine(cartella, $"{base_} ({etichetta}){estensione}");
            if (!File.Exists(candidato) && !Directory.Exists(candidato)) return candidato;
        }
    }

    /// <summary>Tira fuori la copertina e la mette nella cartella del brano.</summary>
    public static async Task<string> SalvaAccanto(Strumenti s, string audio)
    {
        var jpg = await Media.Copertina(s, audio, true) ?? throw new ErroreConversione("Questo brano non ha una copertina.");
        var dest = NomeLibero(Path.GetDirectoryName(Path.GetFullPath(audio))!, Nomi.Radice(audio), ".jpg");
        await File.WriteAllBytesAsync(dest, jpg);
        return dest;
    }

    /// <summary>
    /// Mette l'immagine dentro il brano. Si scrive una bozza accanto e solo alla fine prende il posto del file:
    /// se FFmpeg si ferma, l'originale resta com'era.
    /// </summary>
    public static async Task ScriviNelFile(Strumenti s, string audio, string immagine, CancellationToken ct = default)
    {
        if (!DentroIlFile(audio)) throw new ErroreConversione("Solo MP3, M4A e FLAC tengono la copertina dentro il file.");
        if (!File.Exists(immagine)) throw new ErroreConversione("L'immagine non c'è.");
        var est = Path.GetExtension(audio).ToLowerInvariant();
        var bozza = Nomi.Bozza(audio);
        try
        {
            var a = new List<string>
            {
                "-hide_banner", "-loglevel", "error", "-y", "-i", audio, "-i", immagine,
                "-map", "0:a:0", "-map", "1:0", "-frames:v", "1", "-c:a", "copy",
                "-c:v", est == ".flac" ? "png" : "mjpeg",
                "-disposition:v:0", "attached_pic",
                "-metadata:s:v", "title=Album cover", "-metadata:s:v", "comment=Cover (front)",
                "-map_metadata", "0",
            };
            if (est == ".mp3") a.AddRange(["-id3v2_version", "3"]);
            a.AddRange(["-f", Ffmpeg.Muxer(est), bozza]);
            var r = await Processi.Esegui(s.Ffmpeg, a, ct, tieniUscita: false);
            if (r.Codice != 0 || !File.Exists(bozza) || new FileInfo(bozza).Length == 0)
                throw new ErroreConversione("Non sono riuscito a mettere la copertina.", r.Errori);
            var creato = File.GetCreationTime(audio);
            Modifiche.Sposta(bozza, audio);
            try { File.SetCreationTime(audio, creato); } catch { /* la data di creazione non è il punto */ }
        }
        finally { try { if (File.Exists(bozza)) File.Delete(bozza); } catch { } }
    }

    /// <summary>Mette l'immagine nel file se il formato la tiene, se no la salva accanto e lo dice.</summary>
    public static async Task<EsitoCopertina> Applica(Strumenti s, string audio, string immagine, CancellationToken ct = default)
    {
        if (DentroIlFile(audio))
        {
            await ScriviNelFile(s, audio, immagine, ct);
            var jpg = await Media.Copertina(s, audio, true, silenzioso: true);
            return new EsitoCopertina(jpg, null, true, "Copertina messa nel file.");
        }
        var bytes = await File.ReadAllBytesAsync(immagine, ct);
        var est = EstensioneImmagine(immagine);
        var dove = NomeLibero(Path.GetDirectoryName(Path.GetFullPath(audio))!, Nomi.Radice(audio), est);
        await File.WriteAllBytesAsync(dove, bytes, ct);
        var formato = Path.GetExtension(audio).TrimStart('.').ToUpperInvariant();
        return new EsitoCopertina(bytes, dove, false, $"{formato} non tiene la copertina dentro. L'ho salvata accanto al brano.");
    }

    /// <summary>Una delle copertine disegnate, scritta nel file (o accanto, se il formato non la tiene).</summary>
    public static async Task<EsitoCopertina> ApplicaGenerata(Strumenti s, string audio, string titolo, int variante, CancellationToken ct = default)
    {
        var png = Genera(string.IsNullOrWhiteSpace(titolo) ? Nomi.Radice(audio) : titolo, variante, null);
        var tmp = Path.Combine(Strumenti.CartellaTemporanea, $"copertina-{Guid.NewGuid():N}.png");
        await File.WriteAllBytesAsync(tmp, png, ct);
        try { return await Applica(s, audio, tmp, ct); }
        finally { try { File.Delete(tmp); } catch { } }
    }

    /// <summary>Un disegno quadrato, sempre uguale per lo stesso titolo e la stessa variante. PNG.</summary>
    public static byte[] Genera(string titolo, int variante, float[]? onda)
    {
        variante = ((variante % Quante) + Quante) % Quante;
        var w = Lato;
        var px = new byte[w * w * 4];
        var seme = Seme(titolo, variante);
        switch (variante)
        {
            case 0: Disco(px, w, seme); break;
            case 1: Onda(px, w, seme, onda); break;
            case 2: Cassetta(px, w); break;
            case 3: Notte(px, w, seme, titolo); break;
            case 4: Cornice(px, w, seme); break;
            default: Anelli(px, w, seme, titolo); break;
        }
        Vignetta(px, w);
        ScriviTitolo(px, w, string.IsNullOrWhiteSpace(titolo) ? "DAPROD" : titolo);
        return Png(px, w, w);
    }

    static int Seme(string titolo, int variante)
    {
        var h = variante * 9973 + 17;
        foreach (var c in titolo) h = unchecked(h * 33 + c);
        return h & 0x7fffffff;
    }

    static void Disco(byte[] px, int w, int seme)
    {
        Riempi(px, w, 18, 12, 32);
        var c = w / 2;
        var rng = new Random(seme);
        for (var i = 0; i < 18; i++)
        {
            var x = rng.Next(w);
            var y = rng.Next(w / 2);
            Cerchio(px, w, x, y, rng.Next(2, 5), 255, 213, 74);
        }
        Cerchio(px, w, c, c - 10, 168, 255, 213, 74);
        Cerchio(px, w, c, c - 10, 150, 90, 40, 120);
        Cerchio(px, w, c, c - 10, 118, 255, 61, 242);
        Cerchio(px, w, c, c - 10, 46, 20, 14, 28);
        Cerchio(px, w, c, c - 10, 14, 255, 243, 196);
    }

    static void Onda(byte[] px, int w, int seme, float[]? onda)
    {
        Gradiente(px, w, (12, 10, 22), (48, 16, 72), true);
        var n = 48;
        var picchi = new float[n];
        if (onda is { Length: > 4 })
        {
            for (var i = 0; i < n; i++) picchi[i] = onda[Math.Clamp((int)((long)i * onda.Length / n), 0, onda.Length - 1)];
        }
        else
        {
            var rng = new Random(seme);
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)(n - 1);
                picchi[i] = (float)(0.22 + 0.72 * Math.Abs(Math.Sin(t * 8.5 + seme * 0.01)) * (0.55 + 0.45 * rng.NextDouble()));
            }
        }
        var passo = (w - 64) / (double)n;
        var base_ = (int)(w * 0.72);
        var altoMax = (int)(w * 0.46);
        for (var i = 0; i < n; i++)
        {
            var alto = Math.Max(8, (int)(picchi[i] * altoMax));
            var x = 32 + (int)(i * passo);
            var t = i / (double)(n - 1);
            Rett(px, w, x, base_ - alto, Math.Max(3, (int)passo - 3), alto, (byte)(93 + t * 160), (byte)(255 - t * 180), (byte)(180 + t * 60));
        }
    }

    static void Cassetta(byte[] px, int w)
    {
        Riempi(px, w, 22, 14, 40);
        Rett(px, w, 36, 70, w - 72, w - 150, 28, 18, 48);
        Rett(px, w, 48, 86, w - 96, 150, 255, 213, 74);
        Rett(px, w, 60, 100, w - 120, 122, 18, 12, 28);
        Cerchio(px, w, w / 2 - 78, 250, 52, 40, 24, 64);
        Cerchio(px, w, w / 2 + 78, 250, 52, 40, 24, 64);
        Cerchio(px, w, w / 2 - 78, 250, 16, 255, 61, 242);
        Cerchio(px, w, w / 2 + 78, 250, 16, 255, 61, 242);
        Rett(px, w, 70, 300, w - 140, 8, 255, 213, 74);
    }

    static void Notte(byte[] px, int w, int seme, string titolo)
    {
        Gradiente(px, w, (8, 6, 16), (78, 20, 110), false);
        var rng = new Random(seme);
        for (var i = 0; i < 70; i++)
        {
            var x = rng.Next(w);
            var y = rng.Next((int)(w * 0.7));
            var r = rng.Next(1, 3);
            var caldo = rng.Next(2) == 0;
            Cerchio(px, w, x, y, r, caldo ? (byte)255 : (byte)255, caldo ? (byte)213 : (byte)255, caldo ? (byte)74 : (byte)255);
        }
        var lettera = PrimaLettera(titolo, seme);
        ScriviTesto(px, w, lettera.ToString(), w / 2 - 28, (int)(w * 0.28), 8, 255, 213, 74);
    }

    static void Cornice(byte[] px, int w, int seme)
    {
        Riempi(px, w, 14, 10, 24);
        for (var i = 0; i < w; i += 18)
        {
            var oro = ((i / 18) + seme) % 2 == 0;
            Rett(px, w, i, 0, 9, w, oro ? (byte)255 : (byte)255, oro ? (byte)213 : (byte)61, oro ? (byte)74 : (byte)242);
        }
        Rett(px, w, 28, 28, w - 56, w - 56, 16, 12, 28);
        Rett(px, w, 40, 40, w - 80, w - 80, 32, 18, 58);
    }

    static void Anelli(byte[] px, int w, int seme, string titolo)
    {
        Riempi(px, w, 20, 12, 36);
        var c = w / 2;
        var raggi = new[] { 200, 164, 128, 92, 56 };
        for (var i = 0; i < raggi.Length; i++)
        {
            var oro = (i + seme) % 2 == 0;
            Anello(px, w, c, c - 16, raggi[i], raggi[i] - 16, oro ? (byte)255 : (byte)255, oro ? (byte)213 : (byte)61, oro ? (byte)74 : (byte)242);
        }
        var lettere = Iniziali(titolo, seme);
        var scala = lettere.Length > 1 ? 5 : 7;
        var largo = lettere.Length * 6 * scala;
        ScriviTesto(px, w, lettere, c - largo / 2, c - 16 - 4 * scala, scala, 255, 251, 224);
    }

    static char PrimaLettera(string titolo, int seme)
    {
        foreach (var c in titolo)
        {
            var b = Base(c);
            if (glifi.ContainsKey(b) && char.IsLetter(b)) return b;
        }
        const string alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        return alfabeto[Math.Abs(seme) % alfabeto.Length];
    }

    static string Iniziali(string titolo, int seme)
    {
        var viste = new List<char>();
        var parola = true;
        foreach (var c in titolo)
        {
            var b = Base(c);
            if (!char.IsLetter(b)) { parola = true; continue; }
            if (parola && glifi.ContainsKey(b)) { viste.Add(b); parola = false; if (viste.Count == 2) break; }
        }
        if (viste.Count == 0) return PrimaLettera(titolo, seme).ToString();
        return new string(viste.ToArray());
    }

    static void Vignetta(byte[] p, int w)
    {
        var c = (w - 1) / 2.0;
        for (var y = 0; y < w; y++)
            for (var x = 0; x < w; x++)
            {
                var dx = (x - c) / c;
                var dy = (y - c) / c;
                var d = Math.Sqrt(dx * dx + dy * dy);
                if (d < 0.78) continue;
                var k = Math.Clamp((d - 0.78) / 0.5, 0, 0.62);
                var i = (y * w + x) * 4;
                p[i] = (byte)(p[i] * (1 - k));
                p[i + 1] = (byte)(p[i + 1] * (1 - k));
                p[i + 2] = (byte)(p[i + 2] * (1 - k));
            }
    }

    static void ScriviTitolo(byte[] px, int w, string titolo)
    {
        var righe = Spezza(titolo, 16);
        var scala = 3;
        var y = w - 28 - righe.Count * (8 * scala);
        foreach (var riga in righe)
        {
            var largo = riga.Length * 6 * scala;
            ScriviTesto(px, w, riga, Math.Max(8, (w - largo) / 2), y, scala, 255, 243, 196);
            y += 8 * scala;
        }
    }

    static List<string> Spezza(string titolo, int max)
    {
        var pulito = new string(titolo.Select(c =>
        {
            var b = Base(c);
            return char.IsLetterOrDigit(b) || b == ' ' ? b : ' ';
        }).ToArray());
        while (pulito.Contains("  ", StringComparison.Ordinal)) pulito = pulito.Replace("  ", " ", StringComparison.Ordinal);
        pulito = pulito.Trim();
        if (pulito.Length == 0) return ["DAPROD"];
        if (pulito.Length <= max) return [pulito];
        var righe = new List<string>();
        var riga = "";
        foreach (var parola in pulito.Split(' '))
        {
            var pezzo = parola.Length > max ? parola[..max] : parola;
            if (riga.Length == 0) riga = pezzo;
            else if (riga.Length + 1 + pezzo.Length <= max) riga += " " + pezzo;
            else
            {
                righe.Add(riga);
                riga = pezzo;
                if (righe.Count == 2) return righe;
            }
        }
        if (riga.Length > 0 && righe.Count < 2) righe.Add(riga);
        return righe;
    }

    static char Base(char c)
    {
        if (c == ' ') return ' ';
        var s = c.ToString().Normalize(NormalizationForm.FormD);
        var b = s.FirstOrDefault(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark);
        return char.ToUpperInvariant(b == 0 ? c : b);
    }

    static void ScriviTesto(byte[] px, int w, string testo, int x, int y, int scala, byte r, byte g, byte b)
    {
        var dx = x;
        foreach (var ch in testo)
        {
            if (ch == ' ' || !glifi.TryGetValue(ch, out var glyph)) { dx += 4 * scala; continue; }
            for (var row = 0; row < 7; row++)
                for (var col = 0; col < 5; col++)
                    if (glyph[row * 5 + col] == '1')
                        Rett(px, w, dx + col * scala, y + row * scala, scala, scala, r, g, b);
            dx += 6 * scala;
        }
    }

    static void Riempi(byte[] p, int w, byte r, byte g, byte b)
    {
        for (var i = 0; i < p.Length; i += 4) { p[i] = r; p[i + 1] = g; p[i + 2] = b; p[i + 3] = 255; }
    }

    static void Gradiente(byte[] p, int w, (byte R, byte G, byte B) a, (byte R, byte G, byte B) b, bool diagonale)
    {
        for (var y = 0; y < w; y++)
            for (var x = 0; x < w; x++)
            {
                var t = diagonale ? (x + y) / (2.0 * (w - 1)) : y / (double)(w - 1);
                Px(p, w, x, y, Mix(a.R, b.R, t), Mix(a.G, b.G, t), Mix(a.B, b.B, t));
            }
    }

    static byte Mix(byte a, byte b, double t) => (byte)(a + (b - a) * t);

    static void Rett(byte[] p, int w, int x, int y, int rw, int rh, byte r, byte g, byte b)
    {
        for (var yy = y; yy < y + rh; yy++)
            for (var xx = x; xx < x + rw; xx++)
                Px(p, w, xx, yy, r, g, b);
    }

    static void Cerchio(byte[] p, int w, int cx, int cy, int raggio, byte r, byte g, byte b)
    {
        var r2 = raggio * raggio;
        for (var y = cy - raggio; y <= cy + raggio; y++)
            for (var x = cx - raggio; x <= cx + raggio; x++)
            {
                var dx = x - cx;
                var dy = y - cy;
                if (dx * dx + dy * dy <= r2) Px(p, w, x, y, r, g, b);
            }
    }

    static void Anello(byte[] p, int w, int cx, int cy, int esterno, int interno, byte r, byte g, byte b)
    {
        var e2 = esterno * esterno;
        var i2 = interno * interno;
        for (var y = cy - esterno; y <= cy + esterno; y++)
            for (var x = cx - esterno; x <= cx + esterno; x++)
            {
                var d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                if (d <= e2 && d >= i2) Px(p, w, x, y, r, g, b);
            }
    }

    static void Px(byte[] p, int w, int x, int y, byte r, byte g, byte b)
    {
        if ((uint)x >= (uint)w || (uint)y >= (uint)w) return;
        var i = (y * w + x) * 4;
        p[i] = r;
        p[i + 1] = g;
        p[i + 2] = b;
        p[i + 3] = 255;
    }

    static byte[] Png(byte[] rgba, int w, int h)
    {
        var raw = new byte[h * (w * 4 + 1)];
        for (var y = 0; y < h; y++)
            Buffer.BlockCopy(rgba, y * w * 4, raw, y * (w * 4 + 1) + 1, w * 4);
        using var comp = new MemoryStream();
        using (var z = new ZLibStream(comp, CompressionLevel.Fastest, leaveOpen: true))
            z.Write(raw);
        var ms = new MemoryStream();
        ms.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13];
        Be(ihdr, 0, w);
        Be(ihdr, 4, h);
        ihdr[8] = 8;
        ihdr[9] = 6;
        Chunk(ms, "IHDR", ihdr);
        Chunk(ms, "IDAT", comp.ToArray());
        Chunk(ms, "IEND", []);
        return ms.ToArray();
    }

    static void Chunk(Stream ms, string tipo, byte[] dati)
    {
        var len = new byte[4];
        Be(len, 0, dati.Length);
        ms.Write(len);
        var t = Encoding.ASCII.GetBytes(tipo);
        ms.Write(t);
        ms.Write(dati);
        var crc = new byte[4];
        Be(crc, 0, Crc(t, dati));
        ms.Write(crc);
    }

    static void Be(byte[] b, int o, int v) => Be(b, o, (uint)v);

    static void Be(byte[] b, int o, uint v)
    {
        b[o] = (byte)(v >> 24);
        b[o + 1] = (byte)(v >> 16);
        b[o + 2] = (byte)(v >> 8);
        b[o + 3] = (byte)v;
    }

    static uint Crc(byte[] tipo, byte[] dati)
    {
        var c = 0xffffffffu;
        void Byte(byte b)
        {
            c ^= b;
            for (var i = 0; i < 8; i++) c = (c & 1) != 0 ? 0xedb88320u ^ (c >> 1) : c >> 1;
        }
        foreach (var b in tipo) Byte(b);
        foreach (var b in dati) Byte(b);
        return c ^ 0xffffffffu;
    }

    static string EstensioneImmagine(string percorso)
    {
        var e = Path.GetExtension(percorso).ToLowerInvariant();
        return e is ".png" or ".webp" or ".bmp" ? e : ".jpg";
    }
}
