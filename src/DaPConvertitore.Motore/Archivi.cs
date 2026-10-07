namespace DaP.Convertitore;

/// <summary>
/// Archivi con il tar.exe che Windows 11 ha già dentro (bsdtar/libarchive): legge ZIP, 7Z, RAR, TAR, GZ, XZ, ZST,
/// CAB e ISO, e scrive ZIP, 7Z e TAR compressi. Un archivio che diventa un altro passa da una cartella di lavoro.
/// </summary>
public static class Archivi
{
    public static async Task Converti(Lavoro l, Contesto ctx)
    {
        if (l.Formato.Id == "arch.estrai")
        {
            await Estrai(ctx, l.Sorgente, l.Bozza);
            return;
        }

        if (l.Formato.Categoria == Categoria.Archivio)
        {
            var cartella = ctx.Temporanea();
            await Estrai(ctx, l.Sorgente, cartella, appiattisci: false);
            var dentro = Directory.EnumerateFileSystemEntries(cartella).Select(Path.GetFileName).ToList();
            if (dentro.Count == 0) throw new ErroreConversione("L'archivio è vuoto.");
            await Crea(ctx, l.Bozza, cartella, dentro!);
            return;
        }

        // file e cartelle (anche tanti insieme) in un archivio solo
        var gruppi = l.Sorgenti.GroupBy(s => Path.GetDirectoryName(Path.GetFullPath(s.TrimEnd('\\')))!).ToList();
        if (gruppi.Count == 1)
        {
            await Crea(ctx, l.Bozza, gruppi[0].Key, gruppi[0].Select(s => Path.GetFileName(s.TrimEnd('\\'))).ToList());
            return;
        }
        // da cartelle diverse: si raccolgono prima in una cartella di lavoro
        var raccolta = ctx.Temporanea();
        foreach (var s in l.Sorgenti)
        {
            var dest = Path.Combine(raccolta, Path.GetFileName(s.TrimEnd('\\')));
            if (Directory.Exists(s)) CopiaCartella(s, dest); else File.Copy(s, dest, true);
        }
        await Crea(ctx, l.Bozza, raccolta, Directory.EnumerateFileSystemEntries(raccolta).Select(Path.GetFileName).ToList()!);
    }

    static async Task Estrai(Contesto ctx, string archivio, string dest, bool appiattisci = true)
    {
        Directory.CreateDirectory(dest);
        ctx.Riporta(new Avanzamento(-1, "Estraggo"));
        var r = await Processi.Esegui(ctx.Strumenti.Tar, ["-xf", Path.GetFullPath(archivio), "-C", dest], ctx.Ct, tieniUscita: false);
        if (r.Codice != 0)
            throw new ErroreConversione(r.Errori.Contains("passphrase", StringComparison.OrdinalIgnoreCase) || r.Errori.Contains("encrypt", StringComparison.OrdinalIgnoreCase)
                ? "L'archivio è protetto da password." : "Questo archivio non si riesce ad aprire.", r.Errori);

        // se dentro c'è una cartella sola, non la si lascia dentro un'altra cartella uguale
        if (!appiattisci) return;
        var voci = Directory.GetFileSystemEntries(dest);
        if (voci.Length == 1 && Directory.Exists(voci[0]))
        {
            var appoggio = dest.TrimEnd('\\') + ".~uno";
            Directory.Move(voci[0], appoggio);
            Directory.Delete(dest);
            Directory.Move(appoggio, dest);
        }
    }

    static async Task Crea(Contesto ctx, string archivio, string base_, IReadOnlyList<string> voci)
    {
        ctx.Riporta(new Avanzamento(-1, "Comprimo"));
        var a = new List<string> { "-a", "-cf", Path.GetFullPath(archivio) };
        if (archivio.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)) a.AddRange(["--options", "7zip:compression=lzma2,7zip:compression-level=7"]);
        else if (archivio.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) a.AddRange(["--options", "zip:hdrcharset=UTF-8"]);
        a.AddRange(["-C", base_]);
        a.AddRange(voci);
        var r = await Processi.Esegui(ctx.Strumenti.Tar, a, ctx.Ct, tieniUscita: false, cartella: base_);
        if (r.Codice != 0 || !File.Exists(archivio))
            throw new ErroreConversione(r.Errori.Contains("7zip", StringComparison.OrdinalIgnoreCase)
                ? "Il tar di questo Windows non sa scrivere 7Z: prova ZIP." : "Non sono riuscito a creare l'archivio.", r.Errori);
    }

    static void CopiaCartella(string da, string a)
    {
        Directory.CreateDirectory(a);
        foreach (var f in Directory.GetFiles(da)) File.Copy(f, Path.Combine(a, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(da)) CopiaCartella(d, Path.Combine(a, Path.GetFileName(d)));
    }
}
