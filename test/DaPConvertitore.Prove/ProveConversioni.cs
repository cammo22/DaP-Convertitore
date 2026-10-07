using System.Text;
using System.Text.Json;
using PdfSharp.Drawing;
using PdfSharp.Fonts;

namespace DaP.Convertitore.Prove;

/// <summary>
/// I file di prova si fanno al volo (FFmpeg disegna video, foto e suoni; PDFsharp scrive un PDF) in test/.out,
/// poi si convertono davvero con la coda dell'app e si guarda cosa esce.
/// </summary>
public sealed class Banco : IDisposable
{
    public string Cartella { get; }
    public Strumenti Strumenti { get; } = Strumenti.Trova();
    public InfoHardware Hardware { get; }
    public Coda Coda { get; }

    public Banco()
    {
        var radice = new DirectoryInfo(AppContext.BaseDirectory);
        while (radice is not null && !File.Exists(Path.Combine(radice.FullName, "DaP-Convertitore.slnx"))) radice = radice.Parent;
        Cartella = Path.Combine(radice!.FullName, "test", ".out", DateTime.Now.ToString("HHmmss"));
        Directory.CreateDirectory(Cartella);
        Hardware = DaP.Convertitore.Hardware.Rileva(Strumenti).GetAwaiter().GetResult();
        Coda = new Coda(Strumenti, () => Hardware, () => null, () => "convertito");
    }

    public string Ffmpeg(string nome, params string[] argomenti)
    {
        var dest = Path.Combine(Cartella, nome);
        var r = Processi.Esegui(Strumenti.Ffmpeg, ["-hide_banner", "-loglevel", "error", "-y", .. argomenti, dest], CancellationToken.None).GetAwaiter().GetResult();
        Assert.True(r.Codice == 0, r.Errori);
        return dest;
    }

    public async Task<Lavoro> Converti(string sorgente, string formato, Opzioni? o = null) => await Converti([sorgente], formato, o);

    public async Task<Lavoro> Converti(IReadOnlyList<string> sorgenti, string formato, Opzioni? o = null)
    {
        var fine = new TaskCompletionSource<Lavoro>();
        Lavoro? mio = null;
        void Ascolta(Lavoro l)
        {
            if (l == mio && l.Stato is StatoLavoro.Fatto or StatoLavoro.Errore or StatoLavoro.Annullato) fine.TrySetResult(l);
        }
        Coda.Cambiato += Ascolta;
        try
        {
            mio = Coda.Aggiungi(sorgenti, Catalogo.Trova(formato)!, o ?? new Opzioni());
            if (mio.Stato is StatoLavoro.Fatto or StatoLavoro.Errore) fine.TrySetResult(mio);
            var l = await fine.Task.WaitAsync(TimeSpan.FromMinutes(3));
            Assert.True(l.Stato == StatoLavoro.Fatto, $"{formato}: {l.Errore}\n{l.Dettaglio}");
            Assert.False(File.Exists(l.Bozza) || Directory.Exists(l.Bozza), "la bozza è rimasta");
            return l;
        }
        finally { Coda.Cambiato -= Ascolta; }
    }

    public void Dispose() { }
}

[CollectionDefinition("banco")]
public class CollezioneBanco : ICollectionFixture<Banco>;

[Collection("banco")]
public class ProveConversioni(Banco b)
{
    string Video8s() => File.Exists(Path.Combine(b.Cartella, "prova.mov")) ? Path.Combine(b.Cartella, "prova.mov")
        : b.Ffmpeg("prova.mov", "-f", "lavfi", "-i", "testsrc2=s=1280x720:r=30:d=8", "-f", "lavfi", "-i", "sine=f=440:d=8",
            "-c:v", "libx264", "-crf", "12", "-c:a", "aac", "-b:a", "256k", "-shortest");

    [Fact]
    public async Task Video_con_il_peso_ci_sta_davvero()
    {
        var src = Video8s();
        var o = new Opzioni();
        o.Video.Modo = "peso";
        o.Video.PesoByte = 600_000;
        var l = await b.Converti(src, "video.mp4", o);
        Assert.EndsWith("prova (convertito).mp4", l.Uscita);
        Assert.InRange(l.PesoDopo!.Value, 300_000, 600_000);
        var info = await Sonda.Leggi(b.Strumenti, l.Uscita!);
        Assert.Equal("h264", info.Video!.Codec);
        Assert.InRange(info.Durata, 7.5, 8.5);
    }

    [Fact]
    public async Task Video_in_av1_webm_e_mkv_hevc_cpu()
    {
        var src = Video8s();
        var webm = await b.Converti(src, "video.webm", new Opzioni { Video = { Modo = "qualita", Qualita = 40 } });
        Assert.Equal(b.Hardware.Ha("av1_nvenc") ? "av1" : "vp9", (await Sonda.Leggi(b.Strumenti, webm.Uscita!)).Video!.Codec);

        var mkv = await b.Converti(src, "video.mkv", new Opzioni { Video = { Modo = "peso", PesoByte = 500_000, Codec = "hevc", Motore = "cpu", Lato = 480 } });
        var info = await Sonda.Leggi(b.Strumenti, mkv.Uscita!);
        Assert.Equal("hevc", info.Video!.Codec);
        Assert.Equal(480, info.Video.Altezza);
        Assert.True(mkv.PesoDopo <= 500_000);
    }

    [Fact]
    public async Task Gif_mp3_e_cambio_di_scatola()
    {
        var src = Video8s();
        var gif = await b.Converti(src, "video.gif");
        Assert.Equal((byte)'G', File.ReadAllBytes(gif.Uscita!)[0]);
        var mp3 = await b.Converti(src, "video.mp3");
        Assert.Equal("mp3", (await Sonda.Leggi(b.Strumenti, mp3.Uscita!)).Audio[0].Codec);
        var mkv = await b.Converti(src, "video.mkv", new Opzioni { Video = { Modo = "copia" } });
        Assert.Equal("h264", (await Sonda.Leggi(b.Strumenti, mkv.Uscita!)).Video!.Codec);
    }

    [Fact]
    public async Task Audio_in_tutti_i_formati()
    {
        var wav = b.Ffmpeg("canzone.wav", "-f", "lavfi", "-i", "sine=f=330:d=4", "-ac", "2");
        foreach (var f in new[] { "audio.mp3", "audio.m4a", "audio.opus", "audio.ogg", "audio.flac", "audio.aiff", "audio.wma" })
        {
            var l = await b.Converti(wav, f, new Opzioni { Audio = { Normalizza = f == "audio.mp3" } });
            var info = await Sonda.Leggi(b.Strumenti, l.Uscita!);
            Assert.InRange(info.Durata, 3.8, 4.3);
        }
    }

    [Fact]
    public async Task Foto_in_tutti_i_formati_e_rimpicciolita()
    {
        var png = b.Ffmpeg("foto.png", "-f", "lavfi", "-i", "testsrc2=s=1600x900", "-frames:v", "1");
        foreach (var f in new[] { "img.jpg", "img.webp", "img.avif", "img.jxl", "img.tiff", "img.bmp", "img.gif", "img.ico", "img.png" })
        {
            var l = await b.Converti(png, f, new Opzioni { Immagine = { Lato = 800 } });
            Assert.True(l.PesoDopo > 100, f);
            // con Analisi: sui PC (e sui server di GitHub) senza l'estensione WebP le misure le dà FFprobe
            if (f is "img.jpg" or "img.png" or "img.webp")
                Assert.Equal((800, 450), (await new Analisi(b.Strumenti).Misure(l.Uscita!))!.Value);
        }
    }

    [Fact]
    public async Task La_foto_sta_sotto_il_peso_voluto()
    {
        var png = b.Ffmpeg("rumore.png", "-f", "lavfi", "-i", "nullsrc=s=1200x1200,geq=random(1)*255:128:128", "-frames:v", "1");
        var l = await b.Converti(png, "img.jpg", new Opzioni { Immagine = { PesoMaxByte = 150_000 } });
        Assert.True(l.PesoDopo <= 150_000, $"{l.PesoDopo}");
    }

    [Fact]
    public async Task Le_foto_in_un_pdf_e_il_pdf_in_pagine()
    {
        var a = b.Ffmpeg("pag1.png", "-f", "lavfi", "-i", "testsrc2=s=800x1100", "-frames:v", "1");
        var c = b.Ffmpeg("pag2.jpg", "-f", "lavfi", "-i", "testsrc=s=1200x800", "-frames:v", "1");
        var pdf = await b.Converti([a, c], "img.pdf");
        Assert.Equal(2, await Pdf.Pagine(pdf.Uscita!));

        var pagine = await b.Converti(pdf.Uscita!, "pdf.png");
        Assert.True(Directory.Exists(pagine.Uscita));
        Assert.Equal(2, Directory.GetFiles(pagine.Uscita!, "*.png").Length);

        var unito = await b.Converti([pdf.Uscita!, pdf.Uscita!], "pdf.unisci");
        Assert.Equal(4, await Pdf.Pagine(unito.Uscita!));
    }

    [Fact]
    public async Task Il_testo_esce_da_un_pdf()
    {
        GlobalFontSettings.UseWindowsFontsUnderWindows = true;
        var percorso = Path.Combine(b.Cartella, "lettera.pdf");
        var doc = new PdfSharp.Pdf.PdfDocument();
        var g = XGraphics.FromPdfPage(doc.AddPage());
        g.DrawString("Caro Cammo, questa è una prova del convertitore DaProd.", new XFont("Arial", 14), XBrushes.Black, 60, 100);
        g.Dispose();
        doc.Save(percorso);
        var l = await b.Converti(percorso, "pdf.txt");
        Assert.Contains("convertitore DaProd", File.ReadAllText(l.Uscita!));
        // e una pagina sola in JPG è un file, non una cartella
        var jpg = await b.Converti(percorso, "pdf.jpg");
        Assert.True(File.Exists(jpg.Uscita));
        Assert.EndsWith("(convertito).jpg", jpg.Uscita);
    }

    [Fact]
    public async Task Ocr_della_foto()
    {
        var png = b.Ffmpeg("cartello.png", "-f", "lavfi", "-i", "color=white:s=1000x300", "-frames:v", "1",
            "-vf", "drawtext=fontfile='C\\:/Windows/Fonts/arial.ttf':text='CONVERTITORE DAPROD':fontsize=72:fontcolor=black:x=40:y=110");
        var l = await b.Converti(png, "img.txt");
        Assert.Contains("CONVERTITORE", File.ReadAllText(l.Uscita!).ToUpperInvariant());
    }

    [Fact]
    public async Task Archivi_avanti_e_indietro()
    {
        var cartella = Path.Combine(b.Cartella, "Vacanze à Napoli");
        Directory.CreateDirectory(Path.Combine(cartella, "sotto"));
        File.WriteAllText(Path.Combine(cartella, "nota è.txt"), "ciao");
        File.WriteAllText(Path.Combine(cartella, "sotto", "b.txt"), "b");
        var zip = await b.Converti(cartella, "cartella.zip");
        var sette = await b.Converti(zip.Uscita!, "arch.7z");
        var estratto = await b.Converti(sette.Uscita!, "arch.estrai");
        Assert.True(File.Exists(Path.Combine(estratto.Uscita!, "nota è.txt")), string.Join(", ", Directory.GetFileSystemEntries(estratto.Uscita!)));
        Assert.True(File.Exists(Path.Combine(estratto.Uscita!, "sotto", "b.txt")));
    }

    [Fact]
    public async Task Dati_fra_csv_json_ed_excel()
    {
        var csv = Path.Combine(b.Cartella, "spese.csv");
        File.WriteAllText(csv, "voce;importo\r\naffitto;750,00\r\nluce;61,35\r\n", new UTF8Encoding(true));
        var json = await b.Converti(csv, "dati.json");
        var arr = JsonDocument.Parse(File.ReadAllText(json.Uscita!)).RootElement;
        Assert.Equal(61.35, arr[1].GetProperty("importo").GetDouble());
        var xlsx = await b.Converti(json.Uscita!, "dati.xlsx");
        var ritorno = await b.Converti(xlsx.Uscita!, "foglio.csv");
        Assert.Contains("luce", File.ReadAllText(ritorno.Uscita!));
    }

    [Fact]
    public async Task Sottotitoli_e_testi()
    {
        var srt = Path.Combine(b.Cartella, "film.srt");
        File.WriteAllText(srt, "1\n00:00:01,000 --> 00:00:02,500\nCiao Napoli\n\n2\n00:00:03,000 --> 00:00:04,000\nArrivederci\n");
        var vtt = await b.Converti(srt, "sub.vtt");
        Assert.StartsWith("WEBVTT", File.ReadAllText(vtt.Uscita!));
        var ass = await b.Converti(srt, "sub.ass");
        Assert.Contains("Ciao Napoli", File.ReadAllText(ass.Uscita!));

        var md = Path.Combine(b.Cartella, "leggimi.md");
        File.WriteAllText(md, "# Titolo\n\nUn **paragrafo**.\n\n| a | b |\n|---|---|\n| 1 | 2 |\n");
        var html = await b.Converti(md, "testo.html");
        Assert.Contains("<strong>paragrafo</strong>", File.ReadAllText(html.Uscita!));
        var txt = await b.Converti(md, "testo.txt");
        Assert.Contains("paragrafo", File.ReadAllText(txt.Uscita!));
    }

    [Fact]
    public async Task Documenti_con_libreoffice()
    {
        if (b.Strumenti.LibreOffice is null) return;
        var rtf = Path.Combine(b.Cartella, "lettera.rtf");
        File.WriteAllText(rtf, @"{\rtf1\ansi Caro Cammo,\par questa \b e' \b0 una prova.\par}");
        var pdf = await b.Converti(rtf, "doc.pdf");
        Assert.StartsWith("%PDF", File.ReadAllText(pdf.Uscita!)[..4]);
        var docx = await b.Converti(rtf, "doc.docx");
        Assert.Equal((byte)'P', File.ReadAllBytes(docx.Uscita!)[0]);
    }

    [Fact]
    public async Task L_annullo_non_lascia_niente()
    {
        var lungo = b.Ffmpeg("lungo.mp4", "-f", "lavfi", "-i", "testsrc2=s=1920x1080:r=30:d=40", "-c:v", "libx264", "-preset", "ultrafast");
        var fine = new TaskCompletionSource<Lavoro>();
        Lavoro? l = null;
        b.Coda.Cambiato += x => { if (x == l && x.Stato == StatoLavoro.Corre && x.Frazione > 0.05) b.Coda.Annulla(x.Id); if (x == l && x.Stato is StatoLavoro.Annullato or StatoLavoro.Fatto or StatoLavoro.Errore) fine.TrySetResult(x); };
        l = b.Coda.Aggiungi([lungo], Catalogo.Trova("video.webm")!, new Opzioni { Video = { Modo = "qualita", Motore = "cpu", Codec = "vp9" } });
        var r = await fine.Task.WaitAsync(TimeSpan.FromMinutes(2));
        Assert.Equal(StatoLavoro.Annullato, r.Stato);
        await Task.Delay(500);
        Assert.False(File.Exists(r.Bozza));
        Assert.False(File.Exists(r.Uscita));
    }
}
