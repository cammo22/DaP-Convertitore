using System.IO.Compression;
using System.Text;
using DaP.Convertitore.Lettore;

namespace DaP.Convertitore.Prove;

/// <summary>Il lettore: che pagina per che file, e quello che il motore gli prepara (tracce, forma d'onda, Word, archivi).</summary>
[Collection("banco")]
public class ProveLettore(Banco b)
{
    [Theory]
    [InlineData("film.mkv", "video")]
    [InlineData("canzone.flac", "audio")]
    [InlineData("foto.HEIC", "immagine")]
    [InlineData("scatto.cr3", "immagine")]
    [InlineData("contratto.pdf", "pdf")]
    [InlineData("lettera.docx", "documento")]
    [InlineData("slide.pptx", "presentazione")]
    [InlineData("conti.xlsx", "tabella")]
    [InlineData("clienti.csv", "tabella")]
    [InlineData("LEGGIMI.md", "markdown")]
    [InlineData("pagina.html", "web")]
    [InlineData("dati.json", "json")]
    [InlineData("Program.cs", "codice")]
    [InlineData("note.txt", "testo")]
    [InlineData("film.srt", "sottotitoli")]
    [InlineData("pacco.7z", "archivio")]
    [InlineData("pacco.tar.gz", "archivio")]
    [InlineData("Roboto.ttf", "font")]
    [InlineData("vaso.stl", "modello")]
    public void OgniFileHaLaSuaPagina(string nome, string tipo) => Assert.Equal(tipo, Vista.Tipo(nome));

    [Fact]
    public void SenzaEstensioneSiAnnusa()
    {
        var testo = Path.Combine(b.Cartella, "LEGGIMI");
        File.WriteAllText(testo, "Ciao, sono un testo senza estensione. Città, perché.");
        var byte_ = Path.Combine(b.Cartella, "dati.xyz");
        File.WriteAllBytes(byte_, [0x4d, 0x5a, 0x90, 0x00, 0x03, 0x00]);
        Assert.Equal("testo", Vista.Tipo(testo));
        Assert.Equal("esadecimale", Vista.Tipo(byte_));
    }

    [Fact]
    public void ApriConNonTocchiGliScript()
    {
        var e = Vista.Estensioni.ToList();
        Assert.Contains(".mp4", e);
        Assert.Contains(".heic", e, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(".ttf", e);
        Assert.DoesNotContain(".ps1", e);
        Assert.DoesNotContain(".bat", e);
        Assert.Equal("Video", Vista.Gruppo(".mkv"));
        Assert.Equal("Immagine", Vista.Gruppo(".heic"));
    }

    [Fact]
    public void IFileAccantoSonoDelloStessoGenereInOrdineDiNome()
    {
        var c = Path.Combine(b.Cartella, "cartella-foto");
        Directory.CreateDirectory(c);
        foreach (var n in new[] { "foto 10.jpg", "foto 2.jpg", "foto 1.png", "canzone.mp3", "note.txt" }) File.WriteAllBytes(Path.Combine(c, n), [1]);
        var l = Vista.Fratelli(Path.Combine(c, "foto 2.jpg"), (x, y) => string.Compare(x, y, StringComparison.OrdinalIgnoreCase)).Select(Path.GetFileName).ToList();
        Assert.Equal(3, l.Count);
        Assert.DoesNotContain("canzone.mp3", l);
    }

    [Fact]
    public async Task VideoConTracceETitoli()
    {
        var mkv = b.Ffmpeg("serie.mkv", "-f", "lavfi", "-i", "testsrc2=s=640x360:r=25:d=3", "-f", "lavfi", "-i", "sine=f=440:d=3", "-f", "lavfi", "-i", "sine=f=660:d=3",
            "-map", "0", "-map", "1", "-map", "2", "-c:v", "libx264", "-c:a", "aac", "-metadata:s:a:0", "language=ita", "-metadata:s:a:1", "language=eng", "-metadata", "title=Episodio uno");
        var s = await Media.Leggi(b.Strumenti, mkv);
        Assert.Equal(2, s.Audio.Count);
        Assert.Equal("ita", s.Audio[0].Lingua);
        Assert.Equal("Episodio uno", s.Tag["title"]);
        Assert.Equal("h264", s.Info.Video!.Codec);
    }

    [Fact]
    public async Task LaFormaDOndaSeguelaMusica()
    {
        // metà silenzio, metà suono: i picchi della seconda metà sono alti, quelli della prima no
        var wav = b.Ffmpeg("onda.wav", "-f", "lavfi", "-i", "aevalsrc='if(lt(t,2),0,0.8*sin(2*PI*440*t))':s=8000:d=4");
        var p = await Media.Onda(b.Strumenti, wav, 100);
        Assert.Equal(100, p.Length);
        Assert.True(p.Take(40).Max() < 0.05);
        Assert.True(p.Skip(60).Min() > 0.5);
    }

    [Fact]
    public async Task IlWmaDiventaFlacInCache()
    {
        var wma = b.Ffmpeg("vecchio.wma", "-f", "lavfi", "-i", "sine=f=330:d=2", "-c:a", "wmav2");
        var flac = await Media.AudioInCache(b.Strumenti, wma);
        Assert.True(File.Exists(flac));
        Assert.Equal("flac", (await Sonda.Leggi(b.Strumenti, flac)).Audio[0].Codec);
        // la seconda volta è già lì
        Assert.Equal(flac, await Media.AudioInCache(b.Strumenti, wma));
    }

    [Fact]
    public async Task IlFlussoEsceAPezzi()
    {
        var avi = b.Ffmpeg("vecchio.avi", "-f", "lavfi", "-i", "testsrc2=s=640x360:r=25:d=4", "-f", "lavfi", "-i", "sine=f=440:d=4", "-c:v", "mpeg4", "-c:a", "mp3");
        var info = await Sonda.Leggi(b.Strumenti, avi);
        using var f = Flusso.Avvia(b.Strumenti, b.Hardware, avi, info, 1, 0, false);
        var tutto = new MemoryStream();
        for (var n = 0; ; n++)
        {
            var pezzo = await f.Pezzo(n).WaitAsync(TimeSpan.FromSeconds(30));
            if (pezzo is null) break;
            tutto.Write(pezzo);
        }
        var b2 = tutto.ToArray();
        Assert.True(b2.Length > 10_000);
        // un MP4 a frammenti: comincia con ftyp e ha i moof
        Assert.Equal("ftyp", Encoding.ASCII.GetString(b2, 4, 4));
        Assert.Contains("moof", Encoding.ASCII.GetString(b2));
        Assert.StartsWith("video/mp4", f.Mime);
    }

    [Fact]
    public void WordSiLeggeDaSe()
    {
        var docx = Path.Combine(b.Cartella, "lettera.docx");
        using (var z = ZipFile.Open(docx, ZipArchiveMode.Create))
        {
            using var w = new StreamWriter(z.CreateEntry("word/document.xml").Open());
            w.Write("""
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>
                <w:p><w:pPr><w:pStyle w:val="Heading1"/></w:pPr><w:r><w:t>Preventivo</w:t></w:r></w:p>
                <w:p><w:r><w:t xml:space="preserve">Caro </w:t></w:r><w:r><w:rPr><w:b/></w:rPr><w:t>Cammo</w:t></w:r><w:r><w:t>, ecco i prezzi &amp; le date.</w:t></w:r></w:p>
                <w:tbl><w:tr><w:tc><w:p><w:r><w:t>Video</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>800 €</w:t></w:r></w:p></w:tc></w:tr></w:tbl>
                </w:body></w:document>
                """);
        }
        var html = Carte.Docx(docx);
        Assert.Contains("<h2>Preventivo</h2>", html);
        Assert.Contains("<b>Cammo</b>", html);
        Assert.Contains("&amp; le date", html);
        Assert.Contains("<td>800 €</td>", html);
    }

    [Fact]
    public async Task DentroLoZipSenzaEstrarre()
    {
        var zip = Path.Combine(b.Cartella, "pacco.zip");
        File.Delete(zip);
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            using (var w = new StreamWriter(z.CreateEntry("foto/città.txt").Open())) w.Write("ciao ciao ciao");
            using (var w = new StreamWriter(z.CreateEntry("leggimi.md").Open())) w.Write("# Ciao");
        }
        var v = await Contenuti.Archivio(b.Strumenti, zip);
        Assert.Contains(v, x => x.Percorso == "foto/città.txt" && x.Peso == 14);
        Assert.Contains(v, x => x.Percorso == "leggimi.md");
    }

    [Fact]
    public async Task Dentro7zColTarDiWindows()
    {
        var cartella = Path.Combine(b.Cartella, "da7z");
        Directory.CreateDirectory(cartella);
        File.WriteAllText(Path.Combine(cartella, "uno.txt"), "uno");
        File.WriteAllText(Path.Combine(cartella, "due file.txt"), "due due");
        var z7 = Path.Combine(b.Cartella, "pacco.7z");
        File.Delete(z7);
        var r = await Processi.Esegui(b.Strumenti.Tar, ["-a", "-cf", z7, "-C", cartella, "uno.txt", "due file.txt"], CancellationToken.None);
        Assert.Equal(0, r.Codice);
        var v = await Contenuti.Archivio(b.Strumenti, z7);
        Assert.Contains(v, x => x.Percorso == "due file.txt" && x.Peso == 7);
        Assert.Equal(2, v.Count(x => !x.Cartella));
    }

    [Fact]
    public void TestiDiOgniEpoca()
    {
        var latino = Path.Combine(b.Cartella, "vecchio.txt");
        File.WriteAllBytes(latino, Encoding.Latin1.GetBytes("perché città"));
        Assert.Equal("perché città", Contenuti.Testo(latino).Testo);
        var utf16 = Path.Combine(b.Cartella, "windows.txt");
        File.WriteAllText(utf16, "però", Encoding.Unicode);
        Assert.Equal("però", Contenuti.Testo(utf16).Testo);
    }

    [Fact]
    public async Task LaTabellaDelCsv()
    {
        var csv = Path.Combine(b.Cartella, "clienti.csv");
        File.WriteAllText(csv, "Nome;Città;Importo\nMario;Napoli;1250,50\nLucia;Pozzuoli;980\n");
        var t = await Contenuti.Tabella(b.Strumenti, csv, null);
        Assert.Equal(["Nome", "Città", "Importo"], t.Colonne);
        Assert.Equal(2, t.Totale);
    }

    [Fact]
    public async Task IlPdfSiSfogliaPaginaPerPagina()
    {
        var png = b.Ffmpeg("pagina.png", "-f", "lavfi", "-i", "testsrc2=s=800x1100", "-frames:v", "1");
        var l = await b.Converti(png, "img.pdf");
        Assert.Equal(StatoLavoro.Fatto, l.Stato);
        var pagine = await Carte.Pagine(l.Uscita!);
        Assert.Single(pagine);
        var jpg = await Carte.Pagina(l.Uscita!, 0, 600);
        Assert.True(jpg.Length > 1000);
        Assert.Equal(0xFF, jpg[0]);
        Assert.Equal(0xD8, jpg[1]);
    }
}
