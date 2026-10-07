namespace DaP.Convertitore.Prove;

public class ProveCatalogo
{
    [Fact]
    public void Ogni_voce_rapida_porta_a_un_formato_della_sua_categoria()
    {
        foreach (var r in Catalogo.Rapide)
        {
            var f = Catalogo.Trova(r.Formato);
            Assert.NotNull(f);
            Assert.Equal(r.Categoria, f!.Categoria);
        }
    }

    [Fact]
    public void Gli_id_non_si_ripetono()
    {
        Assert.Equal(Catalogo.Formati.Count, Catalogo.Formati.Select(f => f.Id).Distinct().Count());
        Assert.Equal(Catalogo.Rapide.Count, Catalogo.Rapide.Select(r => r.Id).Distinct().Count());
    }

    [Theory]
    [InlineData(@"C:\x\vacanze.MOV", Categoria.Video)]
    [InlineData(@"C:\x\foto.heic", Categoria.Immagine)]
    [InlineData(@"C:\x\scatto.CR3", Categoria.Immagine)]
    [InlineData(@"C:\x\backup.tar.gz", Categoria.Archivio)]
    [InlineData(@"C:\x\conti.xlsx", Categoria.Foglio)]
    [InlineData(@"C:\x\elenco.csv", Categoria.Dati)]
    [InlineData(@"C:\x\boh.xyz", Categoria.Altro)]
    public void La_categoria_si_capisce_dal_nome(string percorso, Categoria attesa) =>
        Assert.Equal(attesa, Catalogo.CategoriaDi(percorso));

    [Fact]
    public void La_gif_diventa_anche_video_ma_il_png_no()
    {
        Assert.Contains(Catalogo.FormatiPer(@"C:\x\a.gif"), f => f.Id == "img.mp4");
        Assert.DoesNotContain(Catalogo.FormatiPer(@"C:\x\a.png"), f => f.Id == "img.mp4");
    }

    [Fact]
    public void Ogni_categoria_col_menu_ha_almeno_una_voce_rapida()
    {
        foreach (var c in new[] { Categoria.Video, Categoria.Audio, Categoria.Immagine, Categoria.Pdf, Categoria.Documento, Categoria.Foglio,
                     Categoria.Presentazione, Categoria.Testo, Categoria.Dati, Categoria.Sottotitoli, Categoria.Archivio, Categoria.Cartella })
            Assert.Contains(Catalogo.Rapide, r => r.Categoria == c);
    }
}

public class ProveNomi
{
    [Fact]
    public void Accanto_all_originale_con_la_scritta_e_mai_sopra()
    {
        var dir = Directory.CreateTempSubdirectory("dap-nomi").FullName;
        try
        {
            var src = Path.Combine(dir, "vacanze.mov");
            File.WriteAllText(src, "x");
            var a = Nomi.Prenota(src, ".mp4");
            Assert.Equal(Path.Combine(dir, "vacanze (convertito).mp4"), a);
            var b = Nomi.Prenota(src, ".mp4");
            Assert.Equal(Path.Combine(dir, "vacanze (convertito 2).mp4"), b);
            File.WriteAllText(a, "y");
            Nomi.Libera(a);
            Nomi.Libera(b);
            Assert.Equal(Path.Combine(dir, "vacanze (convertito 2).mp4"), Nomi.Prenota(src, ".mp4"));
            Assert.Equal("backup", Nomi.Radice(@"C:\x\backup.tar.gz"));
            Assert.EndsWith(@"\.~vacanze (convertito).mp4", Nomi.Bozza(a));
        }
        finally { Directory.Delete(dir, true); }
    }
}

public class ProveDati
{
    [Fact]
    public void Il_csv_italiano_col_punto_e_virgola_si_legge_coi_numeri_giusti()
    {
        var t = Dati.LeggiCsv("nome;prezzo;cap\r\n\"Rossi; Mario\";1.234,50;00184\r\nBianchi;7;20121\r\n");
        Assert.Equal(["nome", "prezzo", "cap"], t.Colonne);
        Assert.Equal(2, t.Righe.Count);
        Assert.Equal("Rossi; Mario", t.Righe[0][0]);
        Assert.Equal(1234.5, t.Righe[0][1]);
        Assert.Equal("00184", t.Righe[0][2]);
    }

    [Fact]
    public void Il_json_annidato_diventa_colonne_col_punto()
    {
        var t = Dati.LeggiJson("""[{"a":1,"b":{"c":"x"}},{"a":2,"d":true}]""");
        Assert.Equal(["a", "b.c", "d"], t.Colonne);
        Assert.Equal(true, t.Righe[1][2]);
        Assert.Contains("\"b.c\": \"x\"", Dati.Json(t));
    }

    [Fact]
    public void Le_virgolette_e_gli_a_capo_nel_csv_restano_dentro_la_cella()
    {
        var righe = Dati.SpezzaCsv("a,b\n\"uno\ndue\",\"di \"\"lui\"\"\"\n", ',');
        Assert.Equal("uno\ndue", righe[1][0]);
        Assert.Equal("di \"lui\"", righe[1][1]);
    }
}

public class ProveSottotitoli
{
    [Fact]
    public void Dal_srt_restano_solo_le_battute()
    {
        var b = Sottotitoli.DaSrt("1\n00:00:01,000 --> 00:00:02,000\n<i>Ciao</i> a tutti\n\n2\n00:00:03,000 --> 00:00:04,000\nSecondo\n").ToList();
        Assert.Equal(["Ciao a tutti", "Secondo"], b);
        var a = Sottotitoli.DaAss("Dialogue: 0,0:00:01.00,0:00:02.00,Default,,0,0,0,,{\\i1}Ehi,\\Nvirgola\n").ToList();
        Assert.Equal(["Ehi, virgola"], a);
    }
}
