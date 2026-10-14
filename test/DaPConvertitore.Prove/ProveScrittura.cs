using System.IO.Compression;
using System.Text;
using DaP.Convertitore.Lettore;

namespace DaP.Convertitore.Prove;

/// <summary>Salvare il testo modificato nel lettore, e estrarre dentro un archivio solo quello che si sceglie.</summary>
[Collection("banco")]
public class ProveScrittura(Banco b)
{
    [Fact]
    public void IlTestoTornaConBomECrLfDelFileOriginale()
    {
        var p = Path.Combine(b.Cartella, $"bom-{Guid.NewGuid():N}.txt");
        File.WriteAllText(p, "uno\r\ndue\r\n", new UTF8Encoding(true));

        var nota = Scrittura.SalvaTesto(p, "uno\ndue\ntre è città");

        Assert.Null(nota);
        var bytes = File.ReadAllBytes(p);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3));
        var testo = new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);
        Assert.Equal("uno\r\ndue\r\ntre è città", testo);
    }

    [Fact]
    public void UnFileSenzaBomEConLfRestaCosi()
    {
        var p = Path.Combine(b.Cartella, $"lf-{Guid.NewGuid():N}.txt");
        File.WriteAllText(p, "a\nb\n", new UTF8Encoding(false));

        Scrittura.SalvaTesto(p, "a\nb\nc\n");

        var bytes = File.ReadAllBytes(p);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Equal("a\nb\nc\n", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public void ILUnicodeConIlBomRestaUnicode()
    {
        var p = Path.Combine(b.Cartella, $"utf16-{Guid.NewGuid():N}.txt");
        File.WriteAllText(p, "ciao\nmondo", Encoding.Unicode);

        Scrittura.SalvaTesto(p, "ciao\nmondo!");

        var bytes = File.ReadAllBytes(p);
        Assert.Equal([0xFF, 0xFE], bytes.Take(2));
        Assert.Equal("ciao\nmondo!", Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));
    }

    [Fact]
    public void UnaCodificaVecchiaSiSalvaInUtf8ELoDice()
    {
        // «perché» in Latin-1 (é = 0xE9): non è UTF-8, quindi si salva in UTF-8 e la nota lo dice
        var p = Path.Combine(b.Cartella, $"latino-{Guid.NewGuid():N}.txt");
        File.WriteAllBytes(p, Encoding.Latin1.GetBytes("perché\n"));

        var nota = Scrittura.SalvaTesto(p, "perché €\n");

        Assert.NotNull(nota);
        Assert.Contains("UTF-8", nota);
        Assert.Equal("perché €\n", new UTF8Encoding(false, true).GetString(File.ReadAllBytes(p)));
    }

    [Fact]
    public async Task SiEstraggonoSoloLeVociScelte()
    {
        var zip = Path.Combine(b.Cartella, $"scelte-{Guid.NewGuid():N}.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            using (var w = new StreamWriter(z.CreateEntry("foto/uno.txt").Open())) w.Write("1");
            using (var w = new StreamWriter(z.CreateEntry("foto/due.txt").Open())) w.Write("2");
            using (var w = new StreamWriter(z.CreateEntry("foto/sotto/tre.txt").Open())) w.Write("3");
            using (var w = new StreamWriter(z.CreateEntry("altro/x.txt").Open())) w.Write("x");
            using (var w = new StreamWriter(z.CreateEntry("leggimi.md").Open())) w.Write("# ciao");
        }

        var dest = await Scrittura.EstraiVoci(b.Strumenti, zip, ["foto/"]);

        Assert.True(Directory.Exists(Path.Combine(dest, "foto")), "la cartella scelta deve esserci");
        Assert.True(File.Exists(Path.Combine(dest, "foto", "sotto", "tre.txt")), "anche quello che sta dentro");
        Assert.False(Directory.Exists(Path.Combine(dest, "altro")), "le altre cartelle no");
        Assert.False(File.Exists(Path.Combine(dest, "leggimi.md")), "i file fuori dalla scelta no");
    }

    [Fact]
    public async Task UnSingoloFileSiEstraeDaSolo()
    {
        var zip = Path.Combine(b.Cartella, $"singolo-{Guid.NewGuid():N}.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            using (var w = new StreamWriter(z.CreateEntry("a.txt").Open())) w.Write("a");
            using (var w = new StreamWriter(z.CreateEntry("b.txt").Open())) w.Write("b");
        }

        var dest = await Scrittura.EstraiVoci(b.Strumenti, zip, ["b.txt"]);

        Assert.True(File.Exists(Path.Combine(dest, "b.txt")));
        Assert.False(File.Exists(Path.Combine(dest, "a.txt")));
    }

    [Fact]
    public async Task SenzaVociNonSiFaNiente()
    {
        var zip = Path.Combine(b.Cartella, $"vuoto-{Guid.NewGuid():N}.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create)) { }
        await Assert.ThrowsAsync<ErroreConversione>(() => Scrittura.EstraiVoci(b.Strumenti, zip, []));
    }
}
