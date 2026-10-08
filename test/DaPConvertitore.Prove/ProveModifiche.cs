using Windows.Graphics.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using PdfSharp.Pdf.IO;
using DaP.Convertitore.Lettore;

namespace DaP.Convertitore.Prove;

/// <summary>Girare, specchiare e tagliare sul file vero: l'originale deve restare giusto, e senza ricodificare quando si può.</summary>
[Collection("banco")]
public class ProveModifiche(Banco b)
{
    [Theory]
    [InlineData(1, 90, false, 6)]
    [InlineData(6, 90, false, 3)]
    [InlineData(3, 90, false, 8)]
    [InlineData(8, 90, false, 1)]
    [InlineData(1, 0, true, 2)]
    [InlineData(2, 0, true, 1)]
    [InlineData(1, 180, false, 3)]
    [InlineData(1, 270, false, 8)]
    // girata di 90° e poi specchiata (sullo schermo): equivale a «specchia e gira di 270°»
    [InlineData(6, 0, true, 5)]
    public void LOrientamentoExifSiComponeGiusto(int prima, int gradi, bool specchio, int dopo) =>
        Assert.Equal(dopo, Modifiche.ComponiExif(prima, gradi, specchio));

    [Fact]
    public void QuattroGiriSonoUnGiroSolo()
    {
        for (var o = 1; o <= 8; o++)
        {
            var x = o;
            for (var i = 0; i < 4; i++) x = Modifiche.ComponiExif(x, 90, false);
            Assert.Equal(o, x);
            Assert.Equal(o, Modifiche.ComponiExif(Modifiche.ComponiExif(o, 0, true), 0, true));
        }
    }

    static SoftwareBitmap DaPixel(int w, int h, Func<int, int, (byte b, byte g, byte r)> colore)
    {
        var px = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var (bl, g, r) = colore(x, y);
                var i = (y * w + x) * 4;
                px[i] = bl; px[i + 1] = g; px[i + 2] = r; px[i + 3] = 255;
            }
        return SoftwareBitmap.CreateCopyFromBuffer(px.AsBuffer(), BitmapPixelFormat.Bgra8, w, h, BitmapAlphaMode.Premultiplied);
    }

    static (byte b, byte g, byte r) Leggi(SoftwareBitmap s, int x, int y)
    {
        var px = new byte[s.PixelWidth * s.PixelHeight * 4];
        s.CopyToBuffer(px.AsBuffer());
        var i = (y * s.PixelWidth + x) * 4;
        return (px[i], px[i + 1], px[i + 2]);
    }

    [Fact]
    public void IPixelSiGiranoInSensoOrario()
    {
        // 3×2: in alto a sinistra rosso (255 in r), il resto nero
        var f = DaPixel(3, 2, (x, y) => x == 0 && y == 0 ? ((byte)0, (byte)0, (byte)255) : ((byte)0, (byte)0, (byte)0));
        var d90 = Modifiche.Gira(f, 90, false);
        Assert.Equal((2, 3), (d90.PixelWidth, d90.PixelHeight));
        Assert.Equal(255, Leggi(d90, 1, 0).r);   // girato in senso orario, l'angolo in alto a sinistra va in alto a destra
        var d180 = Modifiche.Gira(f, 180, false);
        Assert.Equal(255, Leggi(d180, 2, 1).r);  // e con 180° va in basso a destra
        var d270 = Modifiche.Gira(f, 270, false);
        Assert.Equal(255, Leggi(d270, 0, 2).r);  // con 270° in basso a sinistra
        var specchiata = Modifiche.Gira(f, 0, true);
        Assert.Equal(255, Leggi(specchiata, 2, 0).r);
    }

    [Fact]
    public async Task LaPngGiratadSiSalvaNelFile()
    {
        var png = b.Ffmpeg("lunga.png", "-f", "lavfi", "-i", "testsrc2=s=400x200", "-frames:v", "1");
        Assert.Equal((400, 200), await Immagini.Misure(png));
        await Modifiche.RuotaFoto(png, 90, false);
        Assert.Equal((200, 400), await Immagini.Misure(png));
        await Modifiche.RuotaFoto(png, 270, false);
        Assert.Equal((400, 200), await Immagini.Misure(png));
        Assert.False(Directory.EnumerateFiles(Path.GetDirectoryName(png)!, ".~lunga*").Any());
    }

    [Fact]
    public async Task LaJpgCambiaSoloLOrientamento()
    {
        var jpg = b.Ffmpeg("scatto.jpg", "-f", "lavfi", "-i", "testsrc2=s=600x300", "-frames:v", "1", "-q:v", "2");
        var lunghezza = new FileInfo(jpg).Length;
        await Modifiche.RuotaFoto(jpg, 90, false);
        // la foto, come la vede Windows, adesso è in piedi; il file ha più o meno gli stessi byte (niente ricodifica)
        Assert.Equal((300, 600), await Immagini.Misure(jpg));
        Assert.True(Math.Abs(new FileInfo(jpg).Length - lunghezza) < 2000);
        await Modifiche.RuotaFoto(jpg, 270, false);
        Assert.Equal((600, 300), await Immagini.Misure(jpg));
    }

    [Fact]
    public async Task IFormatiCheNonSiSalvanoLoDicono()
    {
        var webp = b.Ffmpeg("foto-2.webp", "-f", "lavfi", "-i", "testsrc2=s=200x100", "-frames:v", "1");
        var e = await Assert.ThrowsAsync<ErroreConversione>(() => Modifiche.RuotaFoto(webp, 90, false));
        Assert.Contains("JPG o PNG", e.Message);
    }

    [Fact]
    public async Task IlVideoGiraSenzaRicodificare()
    {
        var mp4 = b.Ffmpeg("filmato.mp4", "-f", "lavfi", "-i", "testsrc2=s=640x360:r=25:d=3", "-f", "lavfi", "-i", "sine=d=3", "-c:v", "libx264", "-c:a", "aac");
        Assert.Equal(0, await Modifiche.RotazioneVideo(b.Strumenti, mp4));
        var prima = (await Sonda.Leggi(b.Strumenti, mp4)).Video!;
        await Modifiche.RuotaVideo(b.Strumenti, mp4, 90);
        var dopo = await Sonda.Leggi(b.Strumenti, mp4);
        // l'immagine dentro è la stessa (stesso codec); cambia solo l'etichetta: FFprobe la mette in piedi
        Assert.Equal(prima.Codec, dopo.Video!.Codec);
        Assert.True(dopo.Video.Verticale);
        Assert.Single(dopo.Audio);
        // un altro quarto di giro: 180° in tutto, di nuovo orizzontale
        await Modifiche.RuotaVideo(b.Strumenti, mp4, 90);
        Assert.False((await Sonda.Leggi(b.Strumenti, mp4)).Video!.Verticale);
        // altri 180° e si torna com'era
        await Modifiche.RuotaVideo(b.Strumenti, mp4, 180);
        Assert.Equal(0, ((await Modifiche.RotazioneVideo(b.Strumenti, mp4)) % 360 + 360) % 360);
    }

    [Fact]
    public async Task IlVideoNonMp4NonSiGiraDiNascosto()
    {
        var avi = b.Ffmpeg("vecchio-2.avi", "-f", "lavfi", "-i", "testsrc2=s=320x180:r=25:d=1", "-c:v", "mpeg4");
        var e = await Assert.ThrowsAsync<ErroreConversione>(() => Modifiche.RuotaVideo(b.Strumenti, avi, 90));
        Assert.Contains("MP4", e.Message);
    }

    [Fact]
    public async Task IlPezzoTagliatoVaAccantoEPesaMeno()
    {
        var mp4 = b.Ffmpeg("lungo.mp4", "-f", "lavfi", "-i", "testsrc2=s=640x360:r=25:d=10", "-f", "lavfi", "-i", "sine=d=10", "-c:v", "libx264", "-g", "25", "-c:a", "aac");
        var dest = await Modifiche.TagliaVideo(b.Strumenti, mp4, 3, 7);
        Assert.Contains("(tagliato)", dest);
        Assert.True(File.Exists(mp4));
        var info = await Sonda.Leggi(b.Strumenti, dest);
        Assert.InRange(info.Durata, 3, 6.5);
        await Assert.ThrowsAsync<ErroreConversione>(() => Modifiche.TagliaVideo(b.Strumenti, mp4, 5, 2));
    }

    [Fact]
    public async Task IlPdfSiGiraPaginaPerPagina()
    {
        var a = b.Ffmpeg("p1.png", "-f", "lavfi", "-i", "testsrc2=s=800x1100", "-frames:v", "1");
        var c = b.Ffmpeg("p2.png", "-f", "lavfi", "-i", "testsrc2=s=800x1100", "-frames:v", "1");
        var l = await b.Converti([a, c], "img.pdf");
        var pdf = l.Uscita!;
        Modifiche.RuotaPdf(pdf, 1, 90);
        using (var d = PdfReader.Open(pdf, PdfDocumentOpenMode.Import))
        {
            Assert.Equal(0, d.Pages[0].Rotate);
            Assert.Equal(90, d.Pages[1].Rotate);
        }
        Modifiche.RuotaPdf(pdf, -1, 270);
        using (var d = PdfReader.Open(pdf, PdfDocumentOpenMode.Import))
        {
            Assert.Equal(0, d.Pages[0].Rotate - 270);
            Assert.Equal(0, d.Pages[1].Rotate);
        }
        Assert.Equal(2, (await Carte.Pagine(pdf)).Count);
    }
}
