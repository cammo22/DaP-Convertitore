namespace DaP.Convertitore;

/// <summary>
/// Il tasto destro sui file, su Linux: uno script per Nautilus e Nemo, una voce per Dolphin,
/// e l'icona nel menu delle applicazioni. Niente rete: sono file di testo nella cartella dell'utente.
/// </summary>
public static class MenuLinux
{
    /// <summary>Virgolette da shell: le singole, perché il percorso può avere spazi.</summary>
    public static string Virgolette(string percorso) => "'" + percorso.Replace("'", "'\\''") + "'";

    /// <summary>Virgolette da file .desktop: le doppie, come le vuole la specifica.</summary>
    public static string Desktop(string percorso) => "\"" + percorso.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    public static string ScriptNautilus(string exe) =>
        "#!/bin/sh\n" +
        "# DaP Convertitore: i file scelti in Nautilus o Nemo.\n" +
        "APP=" + Virgolette(exe) + "\n" +
        "IFS='\n'\n" +
        "set --\n" +
        "for f in $NAUTILUS_SCRIPT_SELECTED_FILE_PATHS$NEMO_SCRIPT_SELECTED_FILE_PATHS; do\n" +
        "  [ -n \"$f\" ] && set -- \"$@\" \"$f\"\n" +
        "done\n" +
        "exec \"$APP\" \"$@\"\n";

    public static string ServizioDolphin(string exe) =>
        "[Desktop Entry]\n" +
        "Type=Service\n" +
        "ServiceTypes=KonqPopupMenu/Plugin\n" +
        "MimeType=all/all;\n" +
        "Actions=DaP\n" +
        "X-KDE-Priority=TopLevel\n" +
        "\n" +
        "[Desktop Action DaP]\n" +
        "Name=DaP Convertitore\n" +
        "Icon=dap-convertitore\n" +
        "Exec=" + Desktop(exe) + " %F\n";

    public static string Applicazione(string exe) =>
        "[Desktop Entry]\n" +
        "Type=Application\n" +
        "Name=DaP Convertitore\n" +
        "Comment=Converti video, musica, foto e documenti\n" +
        "Exec=" + Desktop(exe) + " %F\n" +
        "Icon=dap-convertitore\n" +
        "Terminal=false\n" +
        "Categories=AudioVideo;Utility;\n" +
        "MimeType=video/mp4;video/x-matroska;audio/mpeg;audio/flac;image/jpeg;image/png;application/pdf;\n";

    /// <summary>Scrive i tre file e restituisce i percorsi. <paramref name="casa"/> è la home dell'utente.</summary>
    public static IReadOnlyList<string> Installa(string exe, string casa)
    {
        var fatti = new List<string>();
        void Scrivi(string percorso, string testo, bool eseguibile)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
            File.WriteAllText(percorso, testo.Replace("\n", "\n"));
            if (eseguibile)
            {
                try { File.SetUnixFileMode(percorso, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute); }
                catch { /* su un disco senza i permessi Unix il file c'è lo stesso */ }
            }
            fatti.Add(percorso);
        }
        Scrivi(Path.Combine(casa, ".local", "share", "nautilus", "scripts", "DaP Convertitore"), ScriptNautilus(exe), true);
        Scrivi(Path.Combine(casa, ".local", "share", "nemo", "scripts", "DaP Convertitore"), ScriptNautilus(exe), true);
        Scrivi(Path.Combine(casa, ".local", "share", "kio", "servicemenus", "dap-convertitore.desktop"), ServizioDolphin(exe), false);
        Scrivi(Path.Combine(casa, ".local", "share", "applications", "dap-convertitore.desktop"), Applicazione(exe), false);
        return fatti;
    }
}
