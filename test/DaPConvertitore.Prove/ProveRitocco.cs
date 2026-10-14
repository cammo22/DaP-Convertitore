using Windows.Graphics.Imaging;
using DaP.Convertitore.Lettore;

namespace DaP.Convertitore.Prove;

/// <summary>Il ritocco della foto: il file nuovo prende il posto dell'originale e non perde i dati dello scatto.</summary>
[Collection("banco")]
public class ProveRitocco(Banco b)
{
    static async Task<int> Orientamento(string percorso)
    {
        var dati = await File.ReadAllBytesAsync(percorso);
        using var m = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        await m.WriteAsync(System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(dati));
        m.Seek(0);
        var dec = await BitmapDecoder.CreateAsync(m);
        var p = await dec.BitmapProperties.GetPropertiesAsync(["System.Photo.Orientation"]);
        return p.TryGetValue("System.Photo.Orientation", out var v) && v?.Value is not null ? Convert.ToInt32(v.Value) : 0;
    }

    string Jpg(string nome, int lato) => b.Ffmpeg(nome, "-f", "lavfi", "-i", $"testsrc2=size={lato}x{lato * 3 / 4}:rate=1", "-frames:v", "1", "-update", "1", "-q:v", "3");

    [Fact]
    public async Task IlRitoccoPrendeIlPostoDelFileEPortaConSeIDatiDelloScatto()
    {
        // una foto «scattata col telefono in piedi»: orientamento 6 scritto nel file
        var foto = Jpg("ritocco.jpg", 400);
        await Modifiche.RuotaFoto(foto, 90, false);
        Assert.Equal(6, await Orientamento(foto));

        // la pagina manda la foto già dritta e ritoccata (qui: un'altra JPG, senza dati)
        var nuova = await File.ReadAllBytesAsync(Jpg("nuova.jpg", 300));
        Assert.Equal(0, await Orientamento(Path.Combine(b.Cartella, "nuova.jpg")));

        var dove = Ritocco.Salva(foto, nuova);
        Assert.Equal(foto, dove);
        Assert.Equal(300, (await Immagini.Misure(foto))!.Value.w); // i pixel sono quelli nuovi
        Assert.Equal(1, await Orientamento(foto)); // i dati dello scatto ci sono, con la foto «dritta»
        Assert.False(File.Exists(Nomi.Bozza(foto)));
    }

    [Fact]
    public void UnPngRitoccatoPrendeIlPostoDelPng()
    {
        var png = b.Ffmpeg("r.png", "-f", "lavfi", "-i", "testsrc2=size=200x150:rate=1", "-frames:v", "1", "-update", "1");
        var nuovo = File.ReadAllBytes(b.Ffmpeg("r2.png", "-f", "lavfi", "-i", "color=c=red:size=200x150:rate=1", "-frames:v", "1", "-update", "1"));
        Assert.Equal(png, Ritocco.Salva(png, nuovo));
        Assert.Equal(nuovo, File.ReadAllBytes(png));
    }

    [Fact]
    public void UnFormatoCheNonSiRiscriveEsceAccantoInPng()
    {
        var bmp = b.Ffmpeg("r.bmp", "-f", "lavfi", "-i", "testsrc2=size=100x80:rate=1", "-frames:v", "1", "-update", "1");
        var prima = File.ReadAllBytes(bmp);
        var png = File.ReadAllBytes(b.Ffmpeg("r3.png", "-f", "lavfi", "-i", "color=c=blue:size=100x80:rate=1", "-frames:v", "1", "-update", "1"));
        var dove = Ritocco.Salva(bmp, png);
        Assert.NotEqual(bmp, dove);
        Assert.EndsWith("(ritoccata).png", dove);
        Assert.Equal(prima, File.ReadAllBytes(bmp)); // l'originale non si tocca
    }

    [Fact]
    public void UnRitoccoRottoNonSalvaNiente()
    {
        var jpg = Jpg("integro.jpg", 200);
        var prima = File.ReadAllBytes(jpg);
        Assert.Throws<ErroreConversione>(() => Ritocco.Salva(jpg, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14]));
        Assert.Equal(prima, File.ReadAllBytes(jpg));
        // PNG mandato per un file JPG: formati che non coincidono, esce accanto e il JPG resta
        var png = File.ReadAllBytes(b.Ffmpeg("p.png", "-f", "lavfi", "-i", "color=c=green:size=50x50:rate=1", "-frames:v", "1", "-update", "1"));
        Assert.NotEqual(jpg, Ritocco.Salva(jpg, png));
        Assert.Equal(prima, File.ReadAllBytes(jpg));
    }
}
