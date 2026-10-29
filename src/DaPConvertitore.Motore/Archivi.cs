namespace DaP.Convertitore;

/// <summary>
/// Archivi col 7-Zip vero (<see cref="SetteZip"/>): legge ZIP, 7Z, RAR, TAR, GZ, XZ, ZST, CAB, ISO…, e scrive ZIP, 7Z e
/// TAR compressi, col livello scelto con lo slider. Un archivio che diventa un altro passa da una cartella di lavoro.
/// </summary>
public static class Archivi
{
    public static async Task Converti(Lavoro l, Contesto ctx)
    {
        if (l.Formato.Id == "arch.estrai")
        {
            await Estrai(ctx, l.Sorgente, l.Bozza, true, l.Opzioni.Archivio.Password);
            return;
        }

        // con una password: se si parte da un archivio serve per aprirlo; se si parte da file e cartelle serve per cifrare il nuovo
        var nuovaPassword = l.Formato.Categoria == Categoria.Archivio ? null : l.Opzioni.Archivio.Password;
        if (l.Formato.Categoria == Categoria.Archivio)
        {
            var cartella = ctx.Temporanea();
            await Estrai(ctx, l.Sorgente, cartella, false, l.Opzioni.Archivio.Password);
            var dentro = Directory.EnumerateFileSystemEntries(cartella).Select(Path.GetFileName).ToList();
            if (dentro.Count == 0) throw new ErroreConversione("L'archivio è vuoto.");
            await Crea(ctx, l.Bozza, cartella, dentro!, l.Opzioni.Archivio.Livello, nuovaPassword);
            return;
        }

        // file e cartelle (anche tanti insieme) in un archivio solo
        var gruppi = l.Sorgenti.GroupBy(s => Path.GetDirectoryName(Path.GetFullPath(s.TrimEnd('\\')))!).ToList();
        if (gruppi.Count == 1)
        {
            await Crea(ctx, l.Bozza, gruppi[0].Key, gruppi[0].Select(s => Path.GetFileName(s.TrimEnd('\\'))).ToList(), l.Opzioni.Archivio.Livello, nuovaPassword);
            return;
        }
        // da cartelle diverse: si raccolgono prima in una cartella di lavoro
        var raccolta = ctx.Temporanea();
        foreach (var s in l.Sorgenti)
        {
            var dest = Path.Combine(raccolta, Path.GetFileName(s.TrimEnd('\\')));
            if (Directory.Exists(s)) CopiaCartella(s, dest); else File.Copy(s, dest, true);
        }
        await Crea(ctx, l.Bozza, raccolta, Directory.EnumerateFileSystemEntries(raccolta).Select(Path.GetFileName).ToList()!, l.Opzioni.Archivio.Livello, nuovaPassword);
    }

    static async Task Estrai(Contesto ctx, string archivio, string dest, bool appiattisci = true, string? password = null)
    {
        Directory.CreateDirectory(dest);
        ctx.Riporta(new Avanzamento(0, "Estraggo"));
        await SetteZip.Estrai(ctx.Strumenti, archivio, dest, null, ctx.Ct, p => ctx.Riporta(new Avanzamento(p, "Estraggo")), password: password);

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

    static async Task Crea(Contesto ctx, string archivio, string base_, IReadOnlyList<string> voci, int livello, string? password)
    {
        ctx.Riporta(new Avanzamento(0, "Comprimo"));
        var nome = archivio.ToLowerInvariant();
        var formato = nome.EndsWith(".7z") ? "7z" : nome.EndsWith(".tar.gz") ? "tgz" : nome.EndsWith(".tar.xz") ? "txz" : "zip";
        await SetteZip.Crea(ctx.Strumenti, archivio, base_, voci, formato, livello, ctx.Ct, p => ctx.Riporta(new Avanzamento(p, "Comprimo")), password);
    }

    static void CopiaCartella(string da, string a)
    {
        Directory.CreateDirectory(a);
        foreach (var f in Directory.GetFiles(da)) File.Copy(f, Path.Combine(a, Path.GetFileName(f)), true);
        foreach (var d in Directory.GetDirectories(da)) CopiaCartella(d, Path.Combine(a, Path.GetFileName(d)));
    }
}
