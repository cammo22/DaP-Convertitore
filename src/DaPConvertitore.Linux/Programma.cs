using Photino.NET;

namespace DaP.Convertitore.Linux;

/// <summary>DaP Convertitore su Linux: la stessa pagina del programma di Windows, dentro WebKit.</summary>
static class Programma
{
    public static string Versione
    {
        get
        {
            var v = typeof(Programma).Assembly.GetName().Version;
            return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}";
        }
    }

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Contains("--versione")) { Console.WriteLine(Versione); return 0; }
        if (args.Contains("--menu"))
        {
            var exe = Ambiente.Eseguibile();
            foreach (var p in MenuLinux.Installa(exe, Ambiente.Casa())) Console.WriteLine(p);
            return 0;
        }

        var file = args.Where(a => !a.StartsWith('-') && (File.Exists(a) || Directory.Exists(a))).Select(Path.GetFullPath).ToList();
        var ponte = new PonteLinux(Versione, file);
        var pagina = TrovaPagina();
        var finestra = new PhotinoWindow()
            .SetTitle("DaP Convertitore")
            .SetChromeless(true)
            .SetUseOsDefaultSize(false)
            .SetSize(1180, 760)
            .SetFileSystemAccessEnabled(true)
            .SetMediaAutoplayEnabled(true)
            .RegisterWebMessageReceivedHandler((_, messaggio) => ponte.Ricevi(messaggio));
        var icona = Path.Combine(AppContext.BaseDirectory, "icona.png");
        if (File.Exists(icona)) finestra.SetIconFile(icona);
        ponte.Collega(finestra);
        finestra.Load(pagina);
        finestra.WaitForClose();
        return 0;
    }

    static string TrovaPagina()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            foreach (var relativo in new[] { Path.Combine("ui", "index.html"), Path.Combine("ui", "dist", "index.html") })
            {
                var p = Path.Combine(d.FullName, relativo);
                if (File.Exists(p)) return p;
            }
        }
        throw new FileNotFoundException("Non trovo l'interfaccia (ui/index.html).");
    }
}

static class Ambiente
{
    public static string Casa() => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>L'AppImage, se ci siamo dentro: se no l'eseguibile vero.</summary>
    public static string Eseguibile() =>
        Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 } app && File.Exists(app)
            ? app
            : Environment.ProcessPath ?? "";
}
