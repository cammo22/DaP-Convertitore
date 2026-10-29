using DaP.Convertitore.Lettore;

namespace DaP.Convertitore.Prove;

/// <summary>Gli archivi col 7-Zip vero: nomi con gli accenti, 7Z fatti da 7-Zip, tar.gz, password, livelli e niente doppioni.</summary>
[Collection("banco")]
public class ProveArchivi(Banco b)
{
    string Cartella(string nome)
    {
        var c = Path.Combine(b.Cartella, nome);
        if (Directory.Exists(c)) Directory.Delete(c, true);
        Directory.CreateDirectory(Path.Combine(c, "sotto cartella"));
        File.WriteAllText(Path.Combine(c, "è già così.txt"), "ciao ciao ciao");
        File.WriteAllText(Path.Combine(c, "uno.txt"), "uno");
        File.WriteAllText(Path.Combine(c, "sotto cartella", "città [1].txt"), "dentro");
        var caso = new Random(7);
        var parole = Enumerable.Range(0, 400).Select(_ => string.Concat(Enumerable.Range(0, caso.Next(3, 9)).Select(_ => (char)('a' + caso.Next(26))))).ToArray();
        File.WriteAllText(Path.Combine(c, "sotto cartella", "grande.txt"), string.Join(' ', Enumerable.Range(0, 60000).Select(_ => parole[caso.Next(parole.Length)])));
        return c;
    }

    /// <summary>Fa un archivio con il 7-Zip di sistema (non con il nostro codice), come quelli che Cammo si trova in giro.</summary>
    async Task<string> Sette(string cartella, string archivio, params string[] extra)
    {
        if (File.Exists(archivio)) File.Delete(archivio);
        var r = await Processi.Esegui(b.Strumenti.SetteZip!, ["a", "-sccUTF-8", "-scsUTF-8", .. extra, archivio, "*"], CancellationToken.None, cartella: cartella);
        Assert.True(r.Codice == 0, r.Errori + r.Uscita);
        return archivio;
    }

    /// <summary>Una cartella in un archivio: ZIP e 7Z escono dalla cartella; TAR.GZ e TAR.XZ dallo ZIP (come nell'app).</summary>
    async Task<Lavoro> Crea(string cartella, string formato, Opzioni? o = null)
    {
        if (formato is "arch.zip" or "arch.7z") return await b.Converti(cartella, "cartella." + formato[5..], o);
        var zip = await b.Converti(cartella, "cartella.zip");
        return await b.Converti(zip.Uscita!, formato, o);
    }

    [Fact]
    public void SetteZipCe() => Assert.True(b.Strumenti.HaSetteZip, "motori\\7zip\\7z.exe manca: scripts\\prendi-7zip.ps1");

    [Fact]
    public async Task L7zConGliAccentiSiLeggeConINomiGiusti()
    {
        var z = await Sette(Cartella("a7-origine"), Path.Combine(b.Cartella, "accenti.7z"), "-mx=5");
        var v = await Contenuti.Archivio(b.Strumenti, z);
        Assert.Contains(v, x => x.Percorso == "è già così.txt" && x.Peso == 14 && !x.Cartella);
        Assert.Contains(v, x => x.Percorso == "sotto cartella/" && x.Cartella);
        Assert.Contains(v, x => x.Percorso == "sotto cartella/città [1].txt" && x.Peso == 6);
        Assert.DoesNotContain(v, x => x.Percorso.Contains('�'));
    }

    [Fact]
    public async Task LeVociDiUn7zConAccentiSiPortanoFuori()
    {
        var z = await Sette(Cartella("a7-fuori"), Path.Combine(b.Cartella, "fuori.7z"));
        var percorsi = await Trascinamento.Prepara(b.Strumenti, z, ["è già così.txt", "sotto cartella/"]);
        Assert.Equal(2, percorsi.Count);
        Assert.Equal("ciao ciao ciao", File.ReadAllText(percorsi[0]));
        Assert.Equal("dentro", File.ReadAllText(Path.Combine(percorsi[1], "città [1].txt")));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(percorsi[0])!, "uno.txt")));
    }

    [Fact]
    public async Task EstraiLeVociScelteInUnaCartellaSola()
    {
        var z = await Sette(Cartella("a7-voci"), Path.Combine(b.Cartella, "voci.7z"));
        var dest = await Scrittura.EstraiVoci(b.Strumenti, z, ["uno.txt", "sotto cartella/"]);
        Assert.Equal("voci", Path.GetFileName(dest));
        Assert.Equal("uno", File.ReadAllText(Path.Combine(dest, "uno.txt")));
        Assert.True(File.Exists(Path.Combine(dest, "sotto cartella", "città [1].txt")));
        Assert.False(File.Exists(Path.Combine(dest, "è già così.txt")));
        // nessuna copia: accanto all'archivio c'è solo la cartella «voci», non «voci (2)»
        Assert.False(Directory.Exists(Path.Combine(b.Cartella, "voci (2)")));
    }

    [Fact]
    public async Task EstraiQuiDaUn7zDi7ZipDaUnaCartellaSolaECopiaUna()
    {
        var z = await Sette(Cartella("a7-qui"), Path.Combine(b.Cartella, "qui.7z"), "-mx=9");
        var l = await b.Converti(z, "arch.estrai");
        Assert.Equal(StatoLavoro.Fatto, l.Stato);
        Assert.Equal("qui", Path.GetFileName(l.Uscita));
        Assert.Equal("ciao ciao ciao", File.ReadAllText(Path.Combine(l.Uscita!, "è già così.txt")));
        Assert.True(File.Exists(Path.Combine(l.Uscita!, "sotto cartella", "grande.txt")));
        Assert.False(Directory.Exists(Path.Combine(b.Cartella, "qui (2)")));
        Assert.Empty(Directory.GetFileSystemEntries(b.Cartella, ".~*"));
    }

    [Theory]
    [InlineData("arch.zip", "zip")]
    [InlineData("arch.7z", "7z")]
    [InlineData("arch.tgz", "tar.gz")]
    [InlineData("arch.txz", "tar.xz")]
    public async Task SiCreaEsiRiapre(string formato, string nome)
    {
        var c = Cartella("crea-" + nome.Replace('.', '-'));
        var l = await Crea(c, formato);
        Assert.Equal(StatoLavoro.Fatto, l.Stato);
        Assert.EndsWith("." + nome, l.Uscita);
        var v = await Contenuti.Archivio(b.Strumenti, l.Uscita!);
        Assert.Contains(v, x => x.Percorso.EndsWith("è già così.txt"));
        Assert.Contains(v, x => x.Percorso.EndsWith("città [1].txt"));
        var fuori = await b.Converti(l.Uscita!, "arch.estrai");
        Assert.Equal(StatoLavoro.Fatto, fuori.Stato);
        var tutti = Directory.GetFiles(fuori.Uscita!, "*", SearchOption.AllDirectories).Select(Path.GetFileName).ToList();
        Assert.Contains("è già così.txt", tutti);
        Assert.Contains("grande.txt", tutti);
    }

    [Fact]
    public async Task IlLivelloPiuAltoPesaMenoEUnZipSiRicomprime()
    {
        var c = Cartella("livelli");
        var o1 = new Opzioni { Archivio = { Livello = 1 } };
        var o9 = new Opzioni { Archivio = { Livello = 9 } };
        var zip1 = await Crea(c, "arch.zip", o1);
        var zip9 = await Crea(c, "arch.zip", o9);
        Assert.Equal(StatoLavoro.Fatto, zip1.Stato);
        Assert.Equal(StatoLavoro.Fatto, zip9.Stato);
        Assert.True(zip9.PesoDopo < zip1.PesoDopo, $"{zip9.PesoDopo} dovrebbe pesare meno di {zip1.PesoDopo}");

        // lo ZIP debole, ricompresso al massimo, pesa meno e dentro c'è la stessa roba
        var ripreso = await b.Converti(zip1.Uscita!, "arch.zip", o9);
        Assert.Equal(StatoLavoro.Fatto, ripreso.Stato);
        Assert.True(ripreso.PesoDopo < new FileInfo(zip1.Uscita!).Length);
        var v = await Contenuti.Archivio(b.Strumenti, ripreso.Uscita!);
        Assert.Contains(v, x => x.Percorso.EndsWith("grande.txt"));

        var nessuno = await Crea(c, "arch.7z", new Opzioni { Archivio = { Livello = -1 } });
        var forte = await Crea(c, "arch.7z", o9);
        Assert.True(forte.PesoDopo < nessuno.PesoDopo);
    }

    [Fact]
    public async Task UnTarGzSiElencaEsiPortaFuoriUnaVoce()
    {
        var c = Cartella("tgz-origine");
        var l = await Crea(c, "arch.tgz");
        var v = await Contenuti.Archivio(b.Strumenti, l.Uscita!);
        Assert.Contains(v, x => x.Percorso.EndsWith("uno.txt"));
        var voce = v.First(x => x.Percorso.EndsWith("uno.txt")).Percorso;
        var percorsi = await Trascinamento.Prepara(b.Strumenti, l.Uscita!, [voce]);
        Assert.Equal("uno", File.ReadAllText(percorsi[0]));
    }

    [Fact]
    public async Task ConLaPasswordDiceChieProtetto()
    {
        var c = Cartella("cifrato");
        var z = await Sette(c, Path.Combine(b.Cartella, "cifrato.7z"), "-pSegreta1", "-mhe=on");
        var e = await Assert.ThrowsAsync<ErroreConversione>(() => Contenuti.Archivio(b.Strumenti, z));
        Assert.Contains("password", e.Message);
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => b.Converti(z, "arch.estrai"));
        Assert.Contains("protetto da password", ex.Message);
    }

    [Fact]
    public async Task ConLaPasswordGiustaSiApreESiEstrae()
    {
        var z = await Sette(Cartella("pw-origine"), Path.Combine(b.Cartella, "pw.7z"), "-pSegreta1", "-mhe=on");
        var v = await Contenuti.Archivio(b.Strumenti, z, "Segreta1");
        Assert.Contains(v, x => x.Percorso == "uno.txt");
        var sbagliata = await Assert.ThrowsAsync<ErroreConversione>(() => Contenuti.Archivio(b.Strumenti, z, "Sbagliata9"));
        Assert.Contains("non è giusta", sbagliata.Message);
        var l = await b.Converti(z, "arch.estrai", new Opzioni { Archivio = { Password = "Segreta1" } });
        Assert.Equal("uno", File.ReadAllText(Path.Combine(l.Uscita!, "uno.txt")));
        var voci = await Trascinamento.Prepara(b.Strumenti, z, ["uno.txt"], "Segreta1");
        Assert.Equal("uno", File.ReadAllText(voci[0]));
    }

    [Theory]
    [InlineData("arch.zip", "zip")]
    [InlineData("arch.7z", "7z")]
    public async Task SiCreaUnArchivioCifrato(string formato, string estensione)
    {
        var c = Cartella("pw-crea-" + estensione);
        var l = await Crea(c, formato, new Opzioni { Archivio = { Password = "Chiave-77" } });
        Assert.EndsWith("." + estensione, l.Uscita);
        // senza la password non si apre (nello ZIP i nomi si vedono, ma i file no); con la password sì, e il contenuto è quello
        var e = await Assert.ThrowsAsync<ErroreConversione>(() => estensione == "7z"
            ? Contenuti.Archivio(b.Strumenti, l.Uscita!)
            : Scrittura.EstraiVoci(b.Strumenti, l.Uscita!, null));
        Assert.Contains("protetto da password", e.Message);
        var v = await Contenuti.Archivio(b.Strumenti, l.Uscita!, "Chiave-77");
        Assert.Contains(v, x => x.Percorso.EndsWith("uno.txt"));
        var fuori = await b.Converti(l.Uscita!, "arch.estrai", new Opzioni { Archivio = { Password = "Chiave-77" } });
        Assert.Contains("uno.txt", Directory.GetFiles(fuori.Uscita!, "*", SearchOption.AllDirectories).Select(Path.GetFileName));
        // la password non finisce nel registro
        Assert.DoesNotContain("Chiave-77", File.ReadAllText(Path.Combine(Strumenti.CartellaDati, "registro.txt")));
    }

    [Fact]
    public void LAnalisiDellElencoLeggeCartelleEDate()
    {
        var uscita = "Path = a/b.txt\r\nFolder = -\r\nSize = 12\r\nModified = 2026-10-29 14:07:12.1234567\r\nAttributes = A\r\n\r\nPath = a\r\nFolder = +\r\nSize = 0\r\nAttributes = D\r\n\r\n";
        var v = SetteZip.Analizza(uscita);
        Assert.Equal(2, v.Count);
        Assert.Equal(12, v[0].Peso);
        Assert.Equal(2026, v[0].Data!.Value.Year);
        Assert.True(v[1].Cartella);
        Assert.Equal("a/", v[1].Percorso);
    }
}
