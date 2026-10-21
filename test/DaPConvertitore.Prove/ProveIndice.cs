using DaP.Convertitore;

namespace DaP.Convertitore.Prove;

/// <summary>La colonna a destra dell'anteprima per i file che non sono testo (1.9.0): pagine, fotogrammi, onde, figura sola.</summary>
[Collection("banco")]
public class ProveIndice(Banco b)
{
    static bool EImmagine(Anteprima? a) => a is { Dati.Length: > 100 } && a.Tipo.StartsWith("image/");

    [Fact]
    public async Task UnPdfHaUnaFigurinaPerPagina()
    {
        var a = b.Ffmpeg("indice-a.png", "-f", "lavfi", "-i", "testsrc2=s=400x300:d=1", "-frames:v", "1");
        var c = b.Ffmpeg("indice-b.png", "-f", "lavfi", "-i", "smptebars=s=400x300:d=1", "-frames:v", "1");
        var pdf = (await b.Converti([a, c], "img.pdf")).Uscita!;
        var i = await IndiceAnteprima.Descrivi(pdf, Categoria.Pdf, 0);
        Assert.Equal(("pagine", 2), (i.Tipo, i.Voci));
        Assert.Equal(1, IndiceAnteprima.Punto(i, 0));
        Assert.Equal(2, IndiceAnteprima.Punto(i, 1));
        var p1 = await IndiceAnteprima.Voce(pdf, Categoria.Pdf, i, 0, b.Strumenti, 160);
        var p2 = await IndiceAnteprima.Voce(pdf, Categoria.Pdf, i, 1, b.Strumenti, 160);
        Assert.True(EImmagine(p1) && EImmagine(p2));
        Assert.NotEqual(p1!.Dati, p2!.Dati);    // due pagine diverse, due figure diverse
    }

    [Fact]
    public async Task UnVideoHaDodiciFotogrammiDiversi()
    {
        var v = b.Ffmpeg("indice.mp4", "-f", "lavfi", "-i", "testsrc2=s=320x180:r=15:d=6", "-c:v", "libx264", "-pix_fmt", "yuv420p");
        var i = await IndiceAnteprima.Descrivi(v, Categoria.Video, 6);
        Assert.Equal(("tempo", IndiceAnteprima.PerTempo), (i.Tipo, i.Voci));
        Assert.Equal(0.25, IndiceAnteprima.Punto(i, 0), 2);
        Assert.Equal(5.75, IndiceAnteprima.Punto(i, 11), 2);
        var primo = await IndiceAnteprima.Voce(v, Categoria.Video, i, 0, b.Strumenti, 160);
        var ultimo = await IndiceAnteprima.Voce(v, Categoria.Video, i, 11, b.Strumenti, 160);
        Assert.True(EImmagine(primo) && EImmagine(ultimo));
        Assert.NotEqual(primo!.Dati, ultimo!.Dati);
    }

    [Fact]
    public async Task UnBranoHaLeOnde()
    {
        var m = b.Ffmpeg("indice.wav", "-f", "lavfi", "-i", "sine=f=440:d=4");
        var i = await IndiceAnteprima.Descrivi(m, Categoria.Audio, 4);
        Assert.Equal("tempo", i.Tipo);
        Assert.True(EImmagine(await IndiceAnteprima.Voce(m, Categoria.Audio, i, 3, b.Strumenti, 160)));
    }

    [Fact]
    public async Task LoScrubDelVideoDaIlFotogrammoDiOgniSecondo()
    {
        var v = b.Ffmpeg("scrub.mp4", "-f", "lavfi", "-i", "testsrc2=s=320x180:r=15:d=6", "-c:v", "libx264", "-pix_fmt", "yuv420p");
        var inizio = await IndiceAnteprima.Fotogramma(v, 0.2, 6, b.Strumenti, 240);
        var fine = await IndiceAnteprima.Fotogramma(v, 5.5, 6, b.Strumenti, 240);
        var oltre = await IndiceAnteprima.Fotogramma(v, 99, 6, b.Strumenti, 240);    // fuori dal video: si ferma sull'ultimo
        Assert.True(EImmagine(inizio) && EImmagine(fine) && EImmagine(oltre));
        Assert.NotEqual(inizio!.Dati, fine!.Dati);
    }

    [Fact]
    public async Task UnBranoHaUnOndaSolaPerTuttoIlPezzo()
    {
        var m = b.Ffmpeg("scrub.wav", "-f", "lavfi", "-i", "sine=f=440:d=4");
        var onda = await IndiceAnteprima.OndaIntera(m, 4, b.Strumenti, 720);
        Assert.True(EImmagine(onda));
        Assert.Equal("image/png", onda!.Tipo);
    }

    [Fact]
    public async Task UnaFotoEUnDocumentoHannoUnaFigurinaSola()
    {
        var f = b.Ffmpeg("indice-foto.png", "-f", "lavfi", "-i", "testsrc2=s=400x300:d=1", "-frames:v", "1");
        var i = await IndiceAnteprima.Descrivi(f, Categoria.Immagine, 0);
        Assert.Equal(("singola", 1), (i.Tipo, i.Voci));
        Assert.True(EImmagine(await IndiceAnteprima.Voce(f, Categoria.Immagine, i, 0, b.Strumenti, 400)));
        // nessun tipo resta senza anteprima: anche un file qualunque ha almeno l'icona di Windows
        var x = Path.Combine(b.Cartella, "indice.xyz");
        File.WriteAllText(x, "ciao");
        var j = await IndiceAnteprima.Descrivi(x, Categoria.Altro, 0);
        Assert.Equal("singola", j.Tipo);
        Assert.True(EImmagine(await IndiceAnteprima.Voce(x, Categoria.Altro, j, 0, b.Strumenti, 256)));
    }

    [Fact]
    public void UnPdfLungoSiCampiona()
    {
        var i = new IndiceAnteprima.Indice("pagine", IndiceAnteprima.MaxVoci, 1000);
        Assert.Equal(1, IndiceAnteprima.Punto(i, 0));
        Assert.Equal(1000 * 399 / 400 + 1, IndiceAnteprima.Punto(i, 399));
    }
}
