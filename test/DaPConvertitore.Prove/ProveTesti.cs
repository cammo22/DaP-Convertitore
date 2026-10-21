using System.Text;
using DaP.Convertitore.Lettore;

namespace DaP.Convertitore.Prove;

/// <summary>
/// I file di testo (1.8.0): le codifiche (UTF-16 col BOM e senza, i vecchi TXT di Windows in ANSI), le estensioni che
/// prima erano «altri file», i file senza estensione, e i rientri che nel PDF e in Word si schiacciavano.
/// </summary>
[Collection("banco")]
public class ProveTesti(Banco b)
{
    const string Frase = "Perché il caffè costa 2 €? “Così” – disse l’oste.";

    string Scrivi(string nome, byte[] byte_)
    {
        var p = Path.Combine(b.Cartella, nome);
        File.WriteAllBytes(p, byte_);
        return p;
    }

    static byte[] ConBom(Encoding e, string s) => [.. e.GetPreamble(), .. e.GetBytes(s)];

    [Fact]
    public void Utf16ColBomSiLeggeGiusto()
    {
        // il Blocco note «Unicode» e PowerShell 5.1 con «>» scrivono così: prima la 1.8 usciva roba a caso
        var p = Scrivi("unicode.txt", ConBom(Encoding.Unicode, Frase));
        var t = Codifiche.Leggi(p);
        Assert.Equal(Frase, t.Testo);
        Assert.Equal("UTF-16 LE", t.Codifica);
        Assert.True(t.Bom);
        Assert.Equal(Frase, Testo.Leggi(p));
    }

    [Fact]
    public void Utf16BigEndianEUtf8ColBom()
    {
        Assert.Equal(Frase, Codifiche.Leggi(Scrivi("be.txt", ConBom(Encoding.BigEndianUnicode, Frase))).Testo);
        var u8 = Codifiche.Leggi(Scrivi("bom8.txt", ConBom(new UTF8Encoding(true), Frase)));
        Assert.Equal(Frase, u8.Testo);
        Assert.Equal("UTF-8", u8.Codifica);
        Assert.True(u8.Bom);
    }

    [Fact]
    public void Utf16SenzaBomSiRiconosce()
    {
        var t = Codifiche.Leggi(Scrivi("senza-bom.txt", Encoding.Unicode.GetBytes("nome\tcittà\r\nCammo\tNapoli\r\n")));
        Assert.Equal("UTF-16 LE", t.Codifica);
        Assert.False(t.Bom);
        Assert.Equal("nome\tcittà\r\nCammo\tNapoli\r\n", t.Testo);
    }

    [Fact]
    public void IVecchiTxtDiWindowsSonoAnsiENonLatin1()
    {
        // in Latin-1 € e le virgolette curve (0x80, 0x93, 0x94, 0x92, 0x96) diventano caratteri di controllo
        var p = Scrivi("ansi.txt", Codifiche.Ansi.GetBytes(Frase));
        var t = Codifiche.Leggi(p);
        Assert.Equal(Frase, t.Testo);
        Assert.True(t.Vecchia);
        Assert.Equal(Frase, Contenuti.Testo(p).Testo);
    }

    [Fact]
    public void UnPezzoTroncatoNonRompeLUtf8()
    {
        // «è» sono due byte: se il taglio cade a metà, il pezzo resta UTF-8 (prima diventava ANSI)
        var tutto = Encoding.UTF8.GetBytes(new string('a', 9) + "è" + "bbb");
        var t = Codifiche.Decodifica(tutto[..10], troncato: true);
        Assert.Equal("UTF-8", t.Codifica);
        Assert.Equal(new string('a', 9), t.Testo);
    }

    [Theory]
    [InlineData("server.log")]
    [InlineData("impostazioni.ini")]
    [InlineData("config.yaml")]
    [InlineData("progetto.xml")]
    [InlineData("script.py")]
    [InlineData("Programma.cs")]
    [InlineData("leggimi.nfo")]
    public void ITestiSempliciSiConvertono(string nome)
    {
        Assert.Equal(Categoria.Testo, Catalogo.CategoriaDi(@"C:\x\" + nome));
        Assert.Contains(Catalogo.FormatiPer(@"C:\x\" + nome), f => f.Id == "testo.pdf");
        Assert.False(Catalogo.Prosa(nome));
    }

    [Fact]
    public void IlTsRestaVideoEGliScriptRestanoFuori()
    {
        Assert.Equal(Categoria.Video, Catalogo.CategoriaDi(@"C:\x\diretta.ts"));
        Assert.Equal(Categoria.Altro, Catalogo.CategoriaDi(@"C:\x\avvia.bat"));
        Assert.Equal(Categoria.Altro, Catalogo.CategoriaDi(@"C:\x\avvia.ps1"));
        Assert.True(Catalogo.Prosa("lettera.txt"));
    }

    [Fact]
    public void SenzaEstensioneSeDentroETestoETesto()
    {
        var licenza = Scrivi("LICENSE", Encoding.UTF8.GetBytes("MIT License\n\nCopyright (c) 2026 DaProd"));
        Assert.Equal(Categoria.Testo, Catalogo.CategoriaDi(licenza));
        var unicode = Scrivi("NOTE", ConBom(Encoding.Unicode, "appunti"));
        Assert.Equal(Categoria.Testo, Catalogo.CategoriaDi(unicode));
        var binario = Scrivi("dati.bin", [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0x08, 0x00, 0xFF, 0x10, 0x00, 0x01]);
        Assert.Equal(Categoria.Altro, Catalogo.CategoriaDi(binario));
        // numeri a 16 bit: tanti zeri come l'UTF-16, ma non è testo
        var numeri = Scrivi("campioni.raw", Enumerable.Range(0, 2000).SelectMany(i => BitConverter.GetBytes((short)(i % 30))).ToArray());
        Assert.Equal(Categoria.Altro, Catalogo.CategoriaDi(numeri));
    }

    [Fact]
    public void NelPdfIRientriEGliSpaziNonSiSchiacciano()
    {
        var html = Testo.DaTesto("Lista:\n    - uno\n\tdue  spazi\n\nSecondo paragrafo");
        Assert.Contains("&nbsp;&nbsp;&nbsp;&nbsp;- uno", html);
        Assert.Contains("&nbsp;&nbsp;&nbsp;&nbsp;due &nbsp;spazi", html);
        Assert.Equal(2, html.Split("<p>").Length - 1);
        var codice = Testo.DaCodice("if (a < b)\n\treturn;\n");
        Assert.StartsWith("<pre class=\"codice\">", codice);
        Assert.Contains("if (a &lt; b)\n    return;", codice);
    }

    [Fact]
    public async Task Utf16DiventaUtf8ColTestoGiusto()
    {
        var p = Scrivi("esportato.txt", ConBom(Encoding.Unicode, "  rientro\r\n" + Frase + "\r\n"));
        var l = await b.Converti(p, "testo.txt");
        var uscita = File.ReadAllBytes(l.Uscita!);
        Assert.Equal([0xEF, 0xBB, 0xBF], uscita.Take(3));
        Assert.Equal("  rientro\r\n" + Frase + Environment.NewLine, Encoding.UTF8.GetString(uscita, 3, uscita.Length - 3));
    }

    [Fact]
    public async Task IlLogDiventaHtmlASpaziaturaFissa()
    {
        var p = Scrivi("server.log", Codifiche.Ansi.GetBytes("12:00 INFO avvio\r\n12:01 ERRORE   costo 3 €\r\n"));
        var l = await b.Converti(p, "testo.html");
        var html = File.ReadAllText(l.Uscita!);
        Assert.Contains("<pre class=\"codice\">12:00 INFO avvio\n12:01 ERRORE   costo 3 €</pre>", html);
    }

    [Fact]
    public async Task IlCsvInUnicodeSiLegge()
    {
        // Excel «Testo Unicode» e certi gestionali: CSV in UTF-16. Prima le colonne erano piene di byte zero
        var p = Scrivi("clienti-unicode.csv", ConBom(Encoding.Unicode, "nome;città\r\nCammo;Napoli\r\n"));
        var l = await b.Converti(p, "dati.json");
        var json = File.ReadAllText(l.Uscita!);
        Assert.Contains("\"città\": \"Napoli\"", json);
    }

    [Fact]
    public void IlLettoreSalvaLUtf16SenzaPerdereIlBom()
    {
        var p = Scrivi($"salva16-{Guid.NewGuid():N}.txt", ConBom(Encoding.Unicode, "uno\r\n"));
        Scrittura.SalvaTesto(p, "uno\ndue");
        var byte_ = File.ReadAllBytes(p);
        Assert.Equal([0xFF, 0xFE], byte_.Take(2));
        Assert.Equal("uno\r\ndue", Encoding.Unicode.GetString(byte_, 2, byte_.Length - 2));
    }

    [Fact]
    public void IlLettoreSalvaLAnsiInUtf8EIlSimboloDellEuroResta()
    {
        var p = Scrivi($"salva-ansi-{Guid.NewGuid():N}.txt", Codifiche.Ansi.GetBytes("costo 3 €\r\n"));
        Assert.Equal("costo 3 €\r\n", Contenuti.Testo(p).Testo);
        var nota = Scrittura.SalvaTesto(p, "costo 4 €\n");
        Assert.NotNull(nota);
        Assert.Equal("costo 4 €\r\n", File.ReadAllText(p, new UTF8Encoding(false)));
    }
}
