using DaP.Convertitore.Lettore;

namespace DaP.Convertitore.Prove;

/// <summary>Portare fuori dall'archivio le voci scelte: i file veri, nei posti giusti, senza doppioni.</summary>
[Collection("banco")]
public class ProveTrascinamento(Banco b)
{
    [Fact]
    public void ChiScegliUnaCartellaNonPortaDueVolteQuelloCheCeDentro()
    {
        var voci = new[] { "foto/", "foto/mare.txt", "LEGGIMI.txt", "foto-vecchie/uno.txt", "LEGGIMI.txt" };
        var p = Trascinamento.PercorsiInUscita(@"C:\t", voci).ToList();
        Assert.Equal([@"C:\t\foto", @"C:\t\LEGGIMI.txt", @"C:\t\foto-vecchie\uno.txt"], p);
    }

    [Fact]
    public async Task LeVociSiEstraggonoInUnaCartellaTemporaneaECisonoFileVeri()
    {
        var zip = Path.Combine(b.Cartella, "t.zip");
        var origine = Path.Combine(b.Cartella, "t-dentro");
        Directory.CreateDirectory(Path.Combine(origine, "foto", "vacanza"));
        File.WriteAllText(Path.Combine(origine, "foto", "vacanza", "mare.txt"), "mare");
        File.WriteAllText(Path.Combine(origine, "LEGGIMI.txt"), "leggimi");
        File.WriteAllText(Path.Combine(origine, "altro.txt"), "altro");
        var r = await Processi.Esegui(b.Strumenti.Tar, ["-a", "-cf", zip, "-C", origine, "foto", "LEGGIMI.txt", "altro.txt"], CancellationToken.None);
        Assert.Equal(0, r.Codice);

        var percorsi = await Trascinamento.Prepara(b.Strumenti, zip, ["foto/", "LEGGIMI.txt"]);
        Assert.Equal(2, percorsi.Count);
        Assert.True(Directory.Exists(percorsi[0]) && Path.GetFileName(percorsi[0]) == "foto");
        Assert.Equal("mare", File.ReadAllText(Path.Combine(percorsi[0], "vacanza", "mare.txt")));
        Assert.Equal("leggimi", File.ReadAllText(percorsi[1]));
        Assert.StartsWith(Trascinamento.Cartella, percorsi[0]);
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(percorsi[1])!, "altro.txt"))); // solo quello che si è scelto
        // l'archivio non si tocca
        Assert.True(File.Exists(zip));
    }

    [Fact]
    public async Task SenzaVociONonTrovateDaErroreSenzaLasciareCartelleVuote()
    {
        var zip = Path.Combine(b.Cartella, "v.zip");
        File.WriteAllText(Path.Combine(b.Cartella, "x.txt"), "x");
        await Processi.Esegui(b.Strumenti.Tar, ["-a", "-cf", zip, "-C", b.Cartella, "x.txt"], CancellationToken.None);
        await Assert.ThrowsAsync<ErroreConversione>(() => Trascinamento.Prepara(b.Strumenti, zip, []));
        var prima = Directory.Exists(Trascinamento.Cartella) ? Directory.GetDirectories(Trascinamento.Cartella).Length : 0;
        await Assert.ThrowsAsync<ErroreConversione>(() => Trascinamento.Prepara(b.Strumenti, zip, ["non-c-e.txt"]));
        var dopo = Directory.Exists(Trascinamento.Cartella) ? Directory.GetDirectories(Trascinamento.Cartella).Length : 0;
        Assert.Equal(prima, dopo);
    }
}
