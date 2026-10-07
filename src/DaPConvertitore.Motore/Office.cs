namespace DaP.Convertitore;

/// <summary>
/// Documenti, fogli e presentazioni. Se c'è Microsoft Office si usa lui (impagina come lo vedi in Word);
/// se no LibreOffice, senza finestre e con un profilo suo, così non si scontra con un LibreOffice aperto.
/// LibreOffice converte un file alla volta: due insieme sullo stesso profilo si pestano i piedi.
/// </summary>
public static class Office
{
    static readonly SemaphoreSlim unoAllaVolta = new(1, 1);

    public static async Task Converti(Lavoro l, Contesto ctx)
    {
        var s = ctx.Strumenti;
        var est = l.Formato.Estensione;
        var app = AppOffice(l.Formato.Categoria, l.Sorgente, s);
        if (app is not null)
        {
            try
            {
                ctx.Riporta(new Avanzamento(-1, $"Ci pensa {app}"));
                await ConOffice(app, l.Sorgente, l.Bozza, est, ctx);
                if (File.Exists(l.Bozza)) return;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { Registro.Scrivi($"{app} non ce l'ha fatta: {e.Message}"); }
        }
        if (s.LibreOffice is null)
            throw new ErroreConversione("Per i documenti serve LibreOffice (è gratis) o Microsoft Office.", "libreoffice");
        await ConLibreOffice(ctx, l.Sorgente, est, l.Bozza, l.Formato.Categoria == Categoria.Pdf ? "writer_pdf_import" : null);
    }

    static string? AppOffice(Categoria c, string sorgente, Strumenti s) => c switch
    {
        Categoria.Documento or Categoria.Pdf when s.Word => "Word",
        Categoria.Foglio when s.Excel => "Excel",
        Categoria.Presentazione when s.PowerPoint => "PowerPoint",
        _ => null,
    };

    public static string FiltroLibreOffice(string est, bool foglio, bool presentazione) => est switch
    {
        ".pdf" => "pdf",
        ".docx" => "docx:MS Word 2007 XML",
        ".odt" => "odt",
        ".rtf" => "rtf:Rich Text Format",
        ".txt" => "txt:Text (encoded):UTF8",
        ".html" => "html:XHTML Writer File:UTF8",
        ".xlsx" => "xlsx:Calc MS Excel 2007 XML",
        ".ods" => "ods",
        ".csv" => "csv:Text - txt - csv (StarCalc):44,34,76,1",
        ".pptx" => "pptx:Impress MS PowerPoint 2007 XML",
        ".odp" => "odp",
        _ => est.TrimStart('.'),
    };

    public static async Task ConLibreOffice(Contesto ctx, string sorgente, string est, string uscita, string? filtroIngresso = null)
    {
        var soffice = ctx.Strumenti.LibreOffice ?? throw new ErroreConversione("Serve LibreOffice (è gratis).", "libreoffice");
        var console = Path.ChangeExtension(soffice, ".com");
        var programma = File.Exists(console) ? console : soffice;
        var cartella = ctx.Temporanea();
        var profilo = new Uri(Path.Combine(Strumenti.CartellaDati, "libreoffice")).AbsoluteUri;

        await unoAllaVolta.WaitAsync(ctx.Ct);
        try
        {
            ctx.Riporta(new Avanzamento(-1, "Ci pensa LibreOffice"));
            var a = new List<string> { $"-env:UserInstallation={profilo}", "--headless", "--norestore", "--nolockcheck", "--nodefault", "--nofirststartwizard" };
            if (filtroIngresso is not null) a.Add($"--infilter={filtroIngresso}");
            a.AddRange(["--convert-to", FiltroLibreOffice(est, false, false), "--outdir", cartella, sorgente]);
            var r = await Processi.Esegui(programma, a, ctx.Ct, cartella: Path.GetDirectoryName(soffice));
            var uscito = Directory.GetFiles(cartella).FirstOrDefault(f => !f.EndsWith(".log", StringComparison.OrdinalIgnoreCase));
            if (uscito is null)
                throw new ErroreConversione("LibreOffice non è riuscito a convertirlo. Il file è protetto o rovinato?", r.Errori + r.Uscita);
            File.Move(uscito, uscita, true);
        }
        finally { unoAllaVolta.Release(); }
    }

    /// <summary>Word, Excel e PowerPoint comandati da PowerShell: i percorsi passano dall'ambiente, niente virgolette da sbagliare.</summary>
    static async Task ConOffice(string app, string sorgente, string uscita, string est, Contesto ctx)
    {
        var script = app switch
        {
            "Word" => """
                $ErrorActionPreference = 'Stop'
                $w = New-Object -ComObject Word.Application
                $w.Visible = $false; $w.DisplayAlerts = 0
                try {
                  $d = $w.Documents.Open($env:DAP_IN, $false, $true, $false)
                  $d.SaveAs2($env:DAP_OUT, [int]$env:DAP_FMT)
                  $d.Close(0)
                } finally { $w.Quit() }
                """,
            "Excel" => """
                $ErrorActionPreference = 'Stop'
                $x = New-Object -ComObject Excel.Application
                $x.Visible = $false; $x.DisplayAlerts = $false
                try {
                  $b = $x.Workbooks.Open($env:DAP_IN, 0, $true)
                  if ($env:DAP_FMT -eq 'pdf') { $b.ExportAsFixedFormat(0, $env:DAP_OUT) } else { $b.SaveAs($env:DAP_OUT, [int]$env:DAP_FMT) }
                  $b.Close($false)
                } finally { $x.Quit() }
                """,
            _ => """
                $ErrorActionPreference = 'Stop'
                $p = New-Object -ComObject PowerPoint.Application
                try {
                  $s = $p.Presentations.Open($env:DAP_IN, -1, 0, 0)
                  $s.SaveAs($env:DAP_OUT, [int]$env:DAP_FMT)
                  $s.Close()
                } finally { $p.Quit() }
                """,
        };
        var formato = (app, est) switch
        {
            ("Word", ".pdf") => "17",
            ("Word", ".docx") => "16",
            ("Word", ".odt") => "23",
            ("Word", ".rtf") => "6",
            ("Word", ".txt") => "7",
            ("Word", ".html") => "10",
            ("Excel", ".pdf") => "pdf",
            ("Excel", ".xlsx") => "51",
            ("Excel", ".ods") => "60",
            ("Excel", ".csv") => "62",
            ("PowerPoint", ".pdf") => "32",
            ("PowerPoint", ".pptx") => "24",
            ("PowerPoint", ".odp") => "35",
            _ => throw new ErroreConversione($"{app} non fa {est}."),
        };
        var r = await Processi.Esegui("powershell.exe", ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Command", script], ctx.Ct,
            ambiente: new Dictionary<string, string> { ["DAP_IN"] = Path.GetFullPath(sorgente), ["DAP_OUT"] = Path.GetFullPath(uscita), ["DAP_FMT"] = formato });
        if (r.Codice != 0) throw new ErroreConversione($"{app} non è riuscito a convertirlo.", r.Errori);
    }
}
