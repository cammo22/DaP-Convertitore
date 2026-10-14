namespace DaP.Convertitore.Prove;

/// <summary>Lo slider del peso delle foto: la stima deve somigliare al file vero, e il peso chiesto deve starci.</summary>
[Collection("banco")]
public class ProveStimaImmagini(Banco b)
{
    /// <summary>Una foto con dettagli veri (rumore sfumato): una tinta unita peserebbe niente a qualsiasi qualità.</summary>
    string Foto(string nome) => b.Ffmpeg(nome, "-f", "lavfi", "-i", "testsrc2=size=1600x1200:rate=1", "-vf", "noise=alls=25:allf=t,format=yuv420p", "-frames:v", "1", "-update", "1");

    [Fact]
    public async Task LaStimaDelJpgSomigliaAlFileVero()
    {
        var png = Foto("stima.png");
        var s = await StimaImmagini.Fai(b.Strumenti, png, ".jpg", 0, 80, 0, true, CancellationToken.None);
        var l = await b.Converti(png, "img.jpg", new Opzioni { Immagine = { Qualita = 80 } });
        Assert.Equal(StatoLavoro.Fatto, l.Stato);
        var vero = new FileInfo(l.Uscita!).Length;
        Assert.InRange(s.Peso / (double)vero, 0.9, 1.1); // il JPG si codifica per intero: deve essere quasi uguale
        Assert.True(s.Esatta);
        Assert.True(s.Min < s.Peso && s.Peso < s.Max, $"{s.Min} < {s.Peso} < {s.Max}");
    }

    [Fact]
    public async Task ConUnPesoVoluto_LaStimaDiceLaQualitaEIlFileCiStaDavvero()
    {
        var png = Foto("peso.png");
        var pieno = await StimaImmagini.Fai(b.Strumenti, png, ".jpg", 0, 95, 0, false, CancellationToken.None);
        var voluto = pieno.Peso / 3;
        var s = await StimaImmagini.Fai(b.Strumenti, png, ".jpg", 0, 88, voluto, false, CancellationToken.None);
        Assert.True(s.Peso <= voluto, $"{s.Peso} > {voluto}");
        Assert.True(s.Qualita < 95);

        var l = await b.Converti(png, "img.jpg", new Opzioni { Immagine = { Qualita = StimaImmagini.QualitaPerPeso, PesoMaxByte = voluto } });
        Assert.Equal(StatoLavoro.Fatto, l.Stato);
        Assert.True(new FileInfo(l.Uscita!).Length <= voluto);
        // stessa ricerca, stesso risultato (a meno di qualche byte): è la promessa dello slider
        Assert.InRange(new FileInfo(l.Uscita!).Length / (double)s.Peso, 0.98, 1.02);
    }

    [Fact]
    public async Task ConUnPesoPiccolissimoLaFotoSiRimpicciolisce()
    {
        var png = Foto("minuscola.png");
        var s = await StimaImmagini.Fai(b.Strumenti, png, ".jpg", 0, 88, 8_000, false, CancellationToken.None);
        Assert.True(s.Peso <= 8_000);
        Assert.True(s.Scala < 1, "per stare in 8 KB una foto così va rimpicciolita");
        Assert.True(s.Larghezza < 1600);
    }

    [Fact]
    public async Task IlWebpSiStimaDaUnRitaglioEsenzaFarSiMale()
    {
        if (!b.Strumenti.HaFfmpeg) return;
        var png = Foto("webp.png");
        var s = await StimaImmagini.Fai(b.Strumenti, png, ".webp", 0, 80, 0, false, CancellationToken.None);
        Assert.False(s.Esatta);
        var l = await b.Converti(png, "img.webp", new Opzioni { Immagine = { Qualita = 80 } });
        var vero = new FileInfo(l.Uscita!).Length;
        Assert.InRange(s.Peso / (double)vero, 0.4, 2.5); // una stima, non una misura: basta l'ordine di grandezza
    }
}
