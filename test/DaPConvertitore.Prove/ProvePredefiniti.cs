namespace DaP.Convertitore.Prove;

/// <summary>La regola di Windows con le associazioni: il file giusto, solo per i tipi scelti. (Non si scrive niente nel sistema.)</summary>
public class ProvePredefiniti
{
    [Fact]
    public void IlFileHaSoloITipiScelti()
    {
        var xml = Predefiniti.Xml(["Video", "Pdf"]);
        var doc = System.Xml.Linq.XDocument.Parse(xml);
        var voci = doc.Root!.Elements("Association").ToList();
        Assert.NotEmpty(voci);
        Assert.Contains(voci, v => (string?)v.Attribute("Identifier") == ".mp4" && (string?)v.Attribute("ProgId") == "DaProd.Lettore.Video");
        Assert.Contains(voci, v => (string?)v.Attribute("Identifier") == ".pdf" && (string?)v.Attribute("ProgId") == "DaProd.Lettore.Pdf");
        Assert.DoesNotContain(voci, v => (string?)v.Attribute("Identifier") == ".mp3");
        Assert.All(voci, v => Assert.Equal("DaP Convertitore", (string?)v.Attribute("ApplicationName")));
    }

    [Fact]
    public void SenzaGruppiIlFileEVuoto() =>
        Assert.Empty(System.Xml.Linq.XDocument.Parse(Predefiniti.Xml([])).Root!.Elements());

    [Fact]
    public void LeDomandeSulloStatoNonSiRompono()
    {
        _ = Predefiniti.Supportata();
        _ = Predefiniti.Attiva();
        _ = Predefiniti.DiAltri();
    }
}
