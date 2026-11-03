using PdfSharp.Pdf;

namespace DaP.Convertitore.Prove.Linux;

/// <summary>Quello che la versione Linux deve saper fare senza Windows: versioni, menu, PDF, Cestino, una foto con FFmpeg.</summary>
public class ProveLinux
{
    [Fact]
    public void UnaVersionePiùNuovaSiRiconosceDalLink()
    {
        Assert.False(Versioni.PiùNuova("2.3.0", "2.3.0"));
        Assert.False(Versioni.PiùNuova("2.3.0", "v2.2.9"));
        Assert.True(Versioni.PiùNuova("2.3.0", "v2.3.1"));
        Assert.True(Versioni.PiùNuova("2.3.0", "2.4.0"));
        Assert.Equal("2.3.1", Versioni.DalLink("https://github.com/cammo22/DaP-Convertitore/releases/tag/v2.3.1"));
        Assert.Null(Versioni.DalLink("https://github.com/cammo22/DaP-Convertitore/releases"));
    }

    [Fact]
    public void IlMenuNominaIlProgramma()
    {
        var exe = "/home/cammo/DaP Convertitore.AppImage";
        var script = MenuLinux.ScriptNautilus(exe);
        Assert.Contains("'/home/cammo/DaP Convertitore.AppImage'", script);
        Assert.Contains("NAUTILUS_SCRIPT_SELECTED_FILE_PATHS", script);
        Assert.Contains("NEMO_SCRIPT_SELECTED_FILE_PATHS", script);
        var dolphin = MenuLinux.ServizioDolphin(exe);
        Assert.Contains("Name=DaP Convertitore", dolphin);
        Assert.Contains("%F", dolphin);
        var casa = Directory.CreateTempSubdirectory("dap-menu").FullName;
        var fatti = MenuLinux.Installa(exe, casa);
        Assert.Equal(4, fatti.Count);
        Assert.Contains("DaP Convertitore", File.ReadAllText(fatti[0]));
        Assert.Contains("dap-convertitore.desktop", fatti[2]);
    }

    [Fact]
    public void IlCestinoLinuxNonCancella()
    {
        var origine = Path.Combine(Path.GetTempPath(), "dap-cestino-" + Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(origine, "ciao");
        Assert.Null(Cestino.Sposta(origine));
        Assert.False(File.Exists(origine));
        var nome = Path.GetFileName(origine);
        var nelCestino = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Trash", "files", nome);
        var info = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Trash", "info", nome + ".trashinfo");
        Assert.True(File.Exists(nelCestino));
        Assert.Contains("Path=", File.ReadAllText(info));
        File.Delete(nelCestino);
        File.Delete(info);
    }

    [Fact]
    public async Task UnPdfSiConta()
    {
        var p = Path.Combine(Path.GetTempPath(), "dap-" + Guid.NewGuid().ToString("N") + ".pdf");
        var doc = new PdfDocument();
        doc.AddPage();
        doc.AddPage();
        doc.Save(p);
        Assert.Equal(2, await Pdf.Pagine(p));
        File.Delete(p);
    }

    [Fact]
    public async Task UnaFotoJpgEsceDaFfmpeg()
    {
        var s = Strumenti.Trova();
        if (!s.HaFfmpeg) return;
        var cartella = Directory.CreateTempSubdirectory("dap-foto").FullName;
        var png = Path.Combine(cartella, "rossa.png");
        var fatto = await Processi.Esegui(s.Ffmpeg, ["-y", "-f", "lavfi", "-i", "color=c=red:s=64x48", "-frames:v", "1", png], CancellationToken.None);
        Assert.Equal(0, fatto.Codice);
        var formato = Catalogo.Trova("img.jpg")!;
        var lavoro = new Lavoro { Id = "t", Sorgenti = [png], Formato = formato, Opzioni = new Opzioni { Immagine = { Modo = "qualita", Qualita = 80 } } };
        lavoro.Bozza = Path.Combine(cartella, "rossa.jpg");
        var ctx = new Contesto(s, new InfoHardware("cpu", 1, [], []), CancellationToken.None, _ => { });
        await Immagini.Converti(lavoro, ctx);
        Assert.True(new FileInfo(lavoro.Bozza).Length > 100);
    }
}
