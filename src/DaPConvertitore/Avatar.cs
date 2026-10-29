using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DaP.Convertitore.App;

/// <summary>
/// I fotogrammi di un oggetto dell'avatar: un'immagine sola (risorse/avatar/&lt;oggetto&gt;.png, fatta da scripts/avatar) con
/// dentro tutte le animazioni in fila e il suo .json che dice dove comincia ognuna.
/// </summary>
public sealed class FoglioAvatar
{
    public string Nome { get; }
    public int Lato { get; }
    public int Colonne { get; }
    public double Fps { get; }
    readonly Dictionary<string, (int Da, int N)> animazioni;
    readonly BitmapSource immagine;
    readonly Dictionary<int, BitmapSource> cache = [];

    FoglioAvatar(string nome, BitmapSource immagine, int lato, int colonne, double fps, Dictionary<string, (int, int)> animazioni)
    { Nome = nome; this.immagine = immagine; Lato = lato; Colonne = colonne; Fps = fps; this.animazioni = animazioni; }

    public static string Cartella => Path.Combine(AppContext.BaseDirectory, "avatar");

    /// <summary>Gli oggetti che ci sono (un .png con il suo .json), in ordine di nome.</summary>
    public static IReadOnlyList<string> Disponibili() =>
        Directory.Exists(Cartella)
            ? Directory.EnumerateFiles(Cartella, "*.json").Select(Path.GetFileNameWithoutExtension).Where(n => File.Exists(Path.Combine(Cartella, n + ".png"))).Order().ToList()!
            : [];

    public static FoglioAvatar? Carica(string nome)
    {
        try
        {
            var png = Path.Combine(Cartella, nome + ".png");
            var json = Path.Combine(Cartella, nome + ".json");
            if (!File.Exists(png) || !File.Exists(json)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(json));
            var r = doc.RootElement;
            var anim = new Dictionary<string, (int, int)>();
            foreach (var a in r.GetProperty("animazioni").EnumerateObject()) anim[a.Name] = (a.Value.GetProperty("da").GetInt32(), a.Value.GetProperty("n").GetInt32());
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(png);
            bmp.EndInit();
            bmp.Freeze();
            return new FoglioAvatar(nome, bmp, r.GetProperty("lato").GetInt32(), r.GetProperty("colonne").GetInt32(), r.TryGetProperty("fps", out var f) ? f.GetDouble() : 24, anim);
        }
        catch (Exception e) { Registro.Errore("avatar: foglio " + nome, e); return null; }
    }

    public bool Ha(string animazione) => animazioni.ContainsKey(animazione);
    public int Quanti(string animazione) => animazioni.TryGetValue(animazione, out var a) ? a.N : 0;

    public BitmapSource Fotogramma(string animazione, int i)
    {
        var (da, n) = animazioni[animazione];
        var k = da + Math.Clamp(i, 0, n - 1);
        if (cache.TryGetValue(k, out var f)) return f;
        var c = new CroppedBitmap(immagine, new Int32Rect(k % Colonne * Lato, k / Colonne * Lato, Lato, Lato));
        c.Freeze();
        return cache[k] = c;
    }
}

/// <summary>
/// L'avatar sul desktop: un oggetto piccolo (una musicassetta, poi un VHS, un floppy…) che sta fermo e ogni tanto fa un
/// movimento o si trasforma in un altro. Non dà fastidio: niente suoni, niente movimenti quando c'è un programma a schermo
/// intero. Ci si trascinano sopra dei file e chiede cosa farne: aprirli, convertirli o comprimerli.
/// </summary>
public sealed class FinestraAvatar : Window
{
    const double Dimensione = 150;
    double scala = 1;
    readonly Image figura = new() { Stretch = Stretch.Uniform, IsHitTestVisible = false, SnapsToDevicePixels = false };
    readonly Border alone = new() { CornerRadius = new CornerRadius(26), Opacity = 0, IsHitTestVisible = false,
        Background = new RadialGradientBrush(Color.FromArgb(120, 255, 61, 242), Colors.Transparent) };
    readonly DispatcherTimer orologio;
    readonly Random caso = new();
    FoglioAvatar? foglio;
    string animazione = "idle";
    int frame;
    bool ciclo = true;
    Action? alFinire;
    string? dopoLaUscita;           // l'oggetto in cui ci si trasforma quando l'uscita è finita
    DateTime prossimoMovimento, prossimaTrasformazione;
    bool trascinando;
    MenuAvatar? menu;
    MiniPlayer? player;
    readonly Canvas strato = new() { IsHitTestVisible = false };
    readonly System.Windows.Shapes.Ellipse anello = new() { Width = 26, Height = 26, StrokeThickness = 3.5, Stroke = Brushes.Gold, Visibility = Visibility.Collapsed, RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new RotateTransform(-90) };
    readonly System.Windows.Shapes.Ellipse fondoAnello = new() { Width = 26, Height = 26, StrokeThickness = 3.5, Stroke = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), Visibility = Visibility.Collapsed };
    readonly System.Windows.Shapes.Ellipse punto = new() { Width = 12, Height = 12, Fill = Brushes.Gold, Visibility = Visibility.Collapsed, Effect = new DropShadowEffect { Color = Colors.Gold, BlurRadius = 10, ShadowDepth = 0 } };
    DateTime fermoDa; Point ultimoPunto; bool rotellaPronta;
    public event Action? Spostato;

    public FinestraAvatar()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        ShowActivated = false;
        AllowDrop = true;
        Title = "DaP Convertitore · avatar";
        // un velo quasi invisibile: i pixel trasparenti sarebbero «bucati» e non prenderebbero né il mouse né i file
        var radice = new Grid { Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) };
        radice.Children.Add(alone);
        radice.Children.Add(figura);
        strato.Children.Add(fondoAnello); strato.Children.Add(anello); strato.Children.Add(punto);
        radice.Children.Add(strato);
        Content = radice;

        var imp = Impostazioni.Comune;
        scala = Math.Clamp(imp.AvatarScala ?? 1, 0.6, 2.2);
        Width = Height = Dimensione * scala;
        var oggetti = FoglioAvatar.Disponibili();
        if (oggetti.Count == 0) throw new InvalidOperationException("Niente immagini dell'avatar (cartella avatar accanto all'exe).");
        foglio = FoglioAvatar.Carica(imp.AvatarOggetto is { } o && oggetti.Contains(o) ? o : oggetti[0]) ?? throw new InvalidOperationException("Immagini dell'avatar illeggibili.");
        Suona("idle", true);
        var area = SystemParameters.WorkArea;
        Left = imp.AvatarX is { } x && x > area.Left - Width && x < area.Right ? x : area.Right - Width - 24;
        Top = imp.AvatarY is { } y && y > area.Top - Height && y < area.Bottom ? y : area.Bottom - Height - 24;

        orologio = new DispatcherTimer(DispatcherPriority.Render, Dispatcher) { Interval = TimeSpan.FromMilliseconds(1000.0 / foglio.Fps) };
        orologio.Tick += (_, _) => Avanti();
        Pianifica();
        Loaded += (_, _) => orologio.Start();
        // per le prove: DAP_AVATAR_TRASFORMA=1 si trasforma subito (e poi di nuovo ogni 4 secondi)
        if (Environment.GetEnvironmentVariable("DAP_AVATAR_TRASFORMA") == "1")
            ContentRendered += (_, _) => { var t = new DispatcherTimer(TimeSpan.FromSeconds(4), DispatcherPriority.Normal, (_, _) => Trasforma(), Dispatcher); t.Start(); };
        // per le prove: DAP_AVATAR_COMPATTO=percorso apre subito la mini interfaccia come dopo «Comprimi»
        if (Environment.GetEnvironmentVariable("DAP_AVATAR_COMPATTO") is { Length: > 0 } cp && File.Exists(cp))
            ContentRendered += (_, _) => Regia.Gestisci(new Richiesta(null, [cp], true, false, true, "video.mp4", "meta"));
        // per le prove e le foto: DAP_AVATAR_PROVA=percorso apre subito il fumetto come dopo un trascinamento
        if (Environment.GetEnvironmentVariable("DAP_AVATAR_PROVA") is { Length: > 0 } prova)
            ContentRendered += (_, _) => Chiedi([.. prova.Split('|').Where(p => File.Exists(p) || Directory.Exists(p))]);
        Closed += (_, _) => { orologio.Stop(); menu?.Close(); player?.Close(); };
        LocationChanged += (_, _) => Spostato?.Invoke();
        // come in DaProd Video: col cursore fermo sopra l'avatar si carica un anello (1 secondo); quando è pieno la rotella cambia la misura
        MouseMove += (_, e) => Cursore(e.GetPosition(this));
        MouseLeave += (_, _) => SpegniAnello();
        MouseWheel += (_, e) => { if (rotellaPronta) { Misura(scala + (e.Delta > 0 ? 0.1 : -0.1)); fermoDa = DateTime.Now; e.Handled = true; } };

        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ClickCount == 2) { Regia.Gestisci(new Richiesta(null, [], true)); return; }
            trascinando = true;
            try { DragMove(); } catch { /* il mouse è già stato rilasciato */ }
            trascinando = false;
            Salva();
        };
        MouseRightButtonUp += (_, e) => { ApriMenu(); e.Handled = true; };
        DragEnter += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; Suona("attento", true); Alone(true); };
        DragOver += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
        DragLeave += (_, _) => { Alone(false); Suona("idle", true); };
        Drop += (_, e) =>
        {
            Alone(false);
            Suona("saluta", false, () => Suona("idle", true));
            if (e.Data.GetData(DataFormats.FileDrop) is string[] file && file.Length > 0) Chiedi(file.Where(p => File.Exists(p) || Directory.Exists(p)).ToList());
        };
    }

    // ————————————————————————— le animazioni —————————————————————————

    void Suona(string nome, bool inCiclo, Action? finito = null)
    {
        if (foglio is null || !foglio.Ha(nome)) { if (nome != "idle" && foglio?.Ha("idle") == true) Suona("idle", true); return; }
        animazione = nome; frame = 0; ciclo = inCiclo; alFinire = finito;
        figura.Source = foglio.Fotogramma(nome, 0);
    }

    void Avanti()
    {
        if (foglio is null) return;
        if (!foglio.Ha(animazione)) { Suona("idle", true); return; }
        var n = foglio.Quanti(animazione);
        frame++;
        if (frame >= n)
        {
            if (ciclo) frame = 0;
            else { frame = n - 1; var fine = alFinire; alFinire = null; if (fine is not null) fine(); else Suona("idle", true); return; }
        }
        figura.Source = foglio.Fotogramma(animazione, frame);
        // ogni tanto, se nessuno guarda: un movimento o una trasformazione
        if (animazione == "idle" && !trascinando && DateTime.Now >= prossimoMovimento)
        {
            Pianifica();
            if (!Occupato() && menu is null) Movimento();
        }
    }

    /// <summary>Fra un movimento e l'altro passa del tempo, e mai due uguali di fila: non deve stancare.</summary>
    void Pianifica()
    {
        prossimoMovimento = DateTime.Now.AddSeconds(35 + caso.Next(0, 70));
        if (prossimaTrasformazione == default) prossimaTrasformazione = DateTime.Now.AddMinutes(3 + caso.NextDouble() * 4);
    }

    void Movimento()
    {
        if (DateTime.Now >= prossimaTrasformazione && Trasforma()) return;
        Suona(caso.Next(3) == 0 ? "gira" : "saluta", false, () => Suona("idle", true));
    }

    /// <summary>Esce in una pioggia di scintille e al suo posto arriva un altro oggetto.</summary>
    bool Trasforma(string? verso = null)
    {
        var altri = FoglioAvatar.Disponibili().Where(n => n != foglio?.Nome).ToList();
        if (altri.Count == 0 || foglio is null || !foglio.Ha("esci")) return false;
        dopoLaUscita = verso is not null && altri.Contains(verso) ? verso : altri[caso.Next(altri.Count)];
        prossimaTrasformazione = DateTime.Now.AddMinutes(3 + caso.NextDouble() * 5);
        Suona("esci", false, () =>
        {
            var nuovo = FoglioAvatar.Carica(dopoLaUscita!);
            if (nuovo is not null) { foglio = nuovo; var imp = Impostazioni.Comune; imp.AvatarOggetto = nuovo.Nome; imp.Salva(); }
            Suona(foglio.Ha("entra") ? "entra" : "idle", false, () => Suona("idle", true));
        });
        return true;
    }

    /// <summary>C'è un programma a schermo intero, una presentazione o un gioco: l'avatar sta fermo.</summary>
    static bool Occupato()
    {
        // 2 = occupato, 3 = gioco o video a schermo intero, 4 = presentazione
        try { return SHQueryUserNotificationState(out var s) == 0 && s is 2 or 3 or 4; }
        catch { return false; }
    }

    [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int stato);

    void Alone(bool su) => alone.BeginAnimation(OpacityProperty, new DoubleAnimation(su ? 1 : 0, TimeSpan.FromMilliseconds(180)));

    // ————————————————————————— la misura: cursore fermo + rotella —————————————————————————

    const double AttesaMs = 1000;

    void Cursore(Point p)
    {
        if (trascinando) { SpegniAnello(); return; }
        if (!anello.IsVisible && !punto.IsVisible || (p - ultimoPunto).Length > 5)
        {
            // si ricomincia da capo
            ultimoPunto = p; fermoDa = DateTime.Now; rotellaPronta = false;
            punto.Visibility = Visibility.Collapsed;
            fondoAnello.Visibility = anello.Visibility = Visibility.Visible;
            Canvas.SetLeft(anello, Math.Min(p.X + 12, Width - 30)); Canvas.SetTop(anello, Math.Min(p.Y + 12, Height - 30));
            Canvas.SetLeft(fondoAnello, Canvas.GetLeft(anello)); Canvas.SetTop(fondoAnello, Canvas.GetTop(anello));
            Canvas.SetLeft(punto, Canvas.GetLeft(anello) + 7); Canvas.SetTop(punto, Canvas.GetTop(anello) + 7);
            anello.StrokeDashArray = [0, 100];
            if (caricamento is null) { caricamento = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Render, (_, _) => Carica(), Dispatcher); }
            caricamento.Start();
        }
    }

    DispatcherTimer? caricamento;

    void Carica()
    {
        var f = Math.Clamp((DateTime.Now - fermoDa).TotalMilliseconds / AttesaMs, 0, 1);
        if (f >= 1 && !rotellaPronta)
        {
            rotellaPronta = true;
            anello.Visibility = fondoAnello.Visibility = Visibility.Collapsed;
            punto.Visibility = Visibility.Visible;     // il punto dorato: ora la rotella cambia la misura
            caricamento?.Stop();
            return;
        }
        // l'anello si riempie: i tratti sono in multipli dello spessore
        var giro = Math.PI * 26 / anello.StrokeThickness;
        anello.StrokeDashArray = [f * giro, giro];
    }

    void SpegniAnello()
    {
        caricamento?.Stop();
        anello.Visibility = fondoAnello.Visibility = punto.Visibility = Visibility.Collapsed;
        rotellaPronta = false;
    }

    void Misura(double nuova)
    {
        nuova = Math.Round(Math.Clamp(nuova, 0.6, 2.2), 2);
        if (nuova == scala) return;
        var cx = Left + Width / 2; var cy = Top + Height / 2;
        scala = nuova;
        Width = Height = Dimensione * scala;
        Left = cx - Width / 2; Top = cy - Height / 2;
        var imp = Impostazioni.Comune; imp.AvatarScala = scala; imp.AvatarX = Left; imp.AvatarY = Top; imp.Salva();
        Spostato?.Invoke();
    }

    // ————————————————————————— posizione e menu —————————————————————————

    void Salva()
    {
        var imp = Impostazioni.Comune;
        imp.AvatarX = Left; imp.AvatarY = Top;
        imp.Salva();
    }

    void ApriMenu()
    {
        var m = new ContextMenu();
        MenuItem Voce(string testo, Action fai) { var v = new MenuItem { Header = testo }; v.Click += (_, _) => fai(); return v; }
        m.Items.Add(Voce("Saluta", () => Suona("saluta", false, () => Suona("idle", true))));
        if (FoglioAvatar.Disponibili().Count > 1) m.Items.Add(Voce("Trasformati", () => Trasforma()));
        m.Items.Add(Voce("Apri DaP Convertitore", () => Regia.Gestisci(new Richiesta(null, [], true))));
        m.Items.Add(new Separator());
        m.Items.Add(Voce("Nascondi l'avatar", () => Regia.Avatar(false)));
        m.IsOpen = true;
    }

    // ————————————————————————— un file trascinato: cosa vuoi farne? —————————————————————————

    void Chiedi(IReadOnlyList<string> file)
    {
        if (file.Count == 0) return;
        // solo musica: parte subito, col suo piccolo player sull'avatar, senza aprire l'app (foto e video si aprono come sempre)
        if (file.All(f => File.Exists(f) && Catalogo.CategoriaDi(f) == Categoria.Audio)) { RiproduciMusica(file); return; }
        ApriFumetto(file);
    }

    /// <summary>«Cosa vuoi farne?»: apri, converti, comprimi.</summary>
    public void ApriFumetto(IReadOnlyList<string> file)
    {
        menu?.Close();
        menu = new MenuAvatar(file, this);
        menu.Closed += (_, _) => menu = null;
        menu.Show();
    }

    /// <summary>Fa partire la musica sul piccolo player attaccato all'avatar.</summary>
    public void RiproduciMusica(IReadOnlyList<string> brani)
    {
        menu?.Close();
        player?.Close();
        player = new MiniPlayer(brani, this);
        player.Closed += (_, _) => { player = null; this.Suona("idle", true); };
        player.StatoCambiato += (inRiproduzione) => this.Suona(inRiproduzione ? "attento" : "idle", true);
        player.Show();
    }

    /// <summary>Dove sta l'avatar (per mettere il menu accanto).</summary>
    public Rect Area => new(Left, Top, Width, Height);

    public void ConfermaAzione() => Suona("gira", false, () => Suona("idle", true));
}

/// <summary>
/// Il fumetto vicino all'avatar dopo un trascinamento: tre scelte, adattate al file. Vetro scuro come i popup di DaProd;
/// si chiude da solo dopo qualche secondo (il mouse sopra lo ferma).
/// </summary>
public sealed class MenuAvatar : Window
{
    readonly DispatcherTimer chiusura;

    public MenuAvatar(IReadOnlyList<string> file, FinestraAvatar avatar)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;

        var gruppi = file.Select(Catalogo.CategoriaDi).Distinct().ToList();
        var uno = gruppi.Count == 1 ? gruppi[0] : (Categoria?)null;
        var nome = file.Count == 1 ? Path.GetFileName(file[0].TrimEnd('\\')) : $"{file.Count} file";
        var (formatoComprimi, presetComprimi, comprimiNota) = Compressione(uno);

        var pila = new StackPanel { Margin = new Thickness(14, 12, 14, 14), MinWidth = 250, MaxWidth = 330 };
        pila.Children.Add(new TextBlock { Text = "COSA VUOI FARNE?", Foreground = Pennello("#ffd54a"), FontWeight = FontWeights.Bold, FontSize = 11.5, FontFamily = new FontFamily("Orbitron, Segoe UI"), Margin = new Thickness(0, 0, 0, 2) });
        pila.Children.Add(new TextBlock { Text = nome, Foreground = Pennello("#a19db0"), FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 10) });
        var apribile = file.Any(File.Exists);
        pila.Children.Add(Voce("Apri", apribile ? "nel lettore, senza convertire" : "mostra la cartella", "#35e8ff", () =>
        {
            if (apribile) Regia.Gestisci(new Richiesta(null, file.Where(File.Exists).ToList(), false, true));
            else Programma.Mostra(file[0]);
        }));
        // la mini interfaccia è quella del convertitore, stretta, accanto all'avatar: si decide tutto lì (niente parte da solo)
        pila.Children.Add(Voce("Converti", "mini interfaccia: scegli formato e opzioni", "#ff3df2", () => Regia.Gestisci(new Richiesta(null, file, true, false, true))));
        pila.Children.Add(Voce("Comprimi", comprimiNota, "#5dffb4", () => Regia.Gestisci(new Richiesta(null, file, true, false, true, formatoComprimi, presetComprimi))));
        var intera = new TextBlock { Text = "Apri nell'app intera", Foreground = Pennello("#a19db0"), FontSize = 12, Cursor = Cursors.Hand, Margin = new Thickness(4, 2, 0, 0), TextDecorations = TextDecorations.Underline };
        intera.MouseLeftButtonUp += (_, _) => { Close(); Regia.Gestisci(new Richiesta(null, file, true)); };
        pila.Children.Add(intera);

        Content = new Border
        {
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.FromArgb(214, 18, 16, 26)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(46, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(18),
            Effect = new DropShadowEffect { BlurRadius = 28, ShadowDepth = 8, Opacity = 0.5, Direction = 270 },
            Child = pila,
        };

        // vicino all'avatar, dalla parte dove c'è spazio
        Loaded += (_, _) =>
        {
            var a = avatar.Area;
            var area = SystemParameters.WorkArea;
            Left = Math.Clamp(a.Left + a.Width / 2 - ActualWidth / 2, area.Left, area.Right - ActualWidth);
            Top = a.Top - ActualHeight + 14 >= area.Top ? a.Top - ActualHeight + 14 : Math.Min(a.Bottom - 14, area.Bottom - ActualHeight);
        };
        chiusura = new DispatcherTimer(TimeSpan.FromSeconds(10), DispatcherPriority.Normal, (_, _) => Close(), Dispatcher);
        MouseEnter += (_, _) => chiusura.Stop();
        MouseLeave += (_, _) => { chiusura.Stop(); chiusura.Start(); };
        Deactivated += (_, _) => { };
        Closed += (_, _) => chiusura.Stop();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        chiusura.Start();
    }

    /// <summary>Cosa vuol dire «comprimire» per questo tipo di file: l'azione rapida e come si dice.</summary>
    static (string Formato, string? Preset, string Nota) Compressione(Categoria? c) => c switch
    {
        Categoria.Video => ("video.mp4", "meta", "parte da metà peso, H.265: scegli tu"),
        Categoria.Immagine => ("img.jpg", "piccola", "parte da più piccola, 1920 px: scegli tu"),
        Categoria.Audio => ("audio.mp3", null, "MP3, più leggero: scegli tu"),
        Categoria.Archivio => ("arch.7z", null, "ricomprimi in 7Z, più piccolo: scegli tu"),
        Categoria.Cartella => ("cartella.7z", null, "in un 7Z, il più piccolo: scegli tu"),
        _ => ("altro.7z", null, "in un 7Z: scegli tu"),
    };

    static SolidColorBrush Pennello(string colore) => (SolidColorBrush)new BrushConverter().ConvertFromString(colore)!;

    Button Voce(string titolo, string nota, string colore, Action fai)
    {
        var testi = new StackPanel();
        testi.Children.Add(new TextBlock { Text = titolo, Foreground = Pennello("#ebe7f4"), FontWeight = FontWeights.Bold, FontSize = 15 });
        testi.Children.Add(new TextBlock { Text = nota, Foreground = Pennello("#a19db0"), FontSize = 11.5 });
        var riga = new Grid();
        riga.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
        riga.ColumnDefinitions.Add(new ColumnDefinition());
        var barra = new Border { Background = Pennello(colore), CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 2, 0, 2) };
        riga.Children.Add(barra);
        testi.Margin = new Thickness(11, 0, 0, 0);
        Grid.SetColumn(testi, 1);
        riga.Children.Add(testi);
        var b = new Button
        {
            Content = riga, Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 0, 6), Padding = new Thickness(10, 7, 12, 7),
            HorizontalContentAlignment = HorizontalAlignment.Stretch, BorderThickness = new Thickness(0), Focusable = false,
            Background = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)),
            Template = ModelloBottone(),
        };
        b.Click += (_, _) => { Close(); fai(); avatar().ConfermaAzione(); };
        return b;

        FinestraAvatar avatar() => (FinestraAvatar)Application.Current.Windows.OfType<FinestraAvatar>().First();
    }

    /// <summary>Un bottone piatto che si accende al passaggio (quello di Windows è grigio e squadrato).</summary>
    static ControlTemplate ModelloBottone()
    {
        var t = new ControlTemplate(typeof(Button));
        var bordo = new FrameworkElementFactory(typeof(Border));
        bordo.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        bordo.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        bordo.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        bordo.Name = "b";
        var cont = new FrameworkElementFactory(typeof(ContentPresenter));
        bordo.AppendChild(cont);
        t.VisualTree = bordo;
        var su = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        su.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(46, 255, 213, 74)), "b"));
        t.Triggers.Add(su);
        return t;
    }
}


/// <summary>
/// Il piccolo player attaccato all'avatar: la musica trascinata sull'avatar parte subito, senza aprire l'app (foto e video si
/// aprono come sempre). Tasti: precedente, play/pausa, successivo, «apri nell'app», «⋯» (apri/converti/comprimi) e chiudi.
/// Vetro scuro come i popup di DaProd; segue l'avatar quando lo sposti.
/// </summary>
public sealed class MiniPlayer : Window
{
    readonly MediaPlayer mp = new();
    readonly IReadOnlyList<string> brani;
    readonly FinestraAvatar avatar;
    readonly TextBlock titolo = new() { Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 13.5, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly TextBlock tempo = new() { Foreground = new SolidColorBrush(Color.FromRgb(0xa1, 0x9d, 0xb0)), FontSize = 11.5, HorizontalAlignment = HorizontalAlignment.Right };
    readonly Border riempimento = new() { Background = new SolidColorBrush(Color.FromRgb(0xff, 0xd5, 0x4a)), CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
    readonly Border barra = new() { Height = 6, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), Cursor = Cursors.Hand, Margin = new Thickness(0, 6, 0, 4) };
    readonly Button bPlay;
    readonly DispatcherTimer orologio;
    int i;
    bool suona;
    public event Action<bool>? StatoCambiato;

    public MiniPlayer(IReadOnlyList<string> brani, FinestraAvatar avatar)
    {
        this.brani = brani; this.avatar = avatar;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Height; Width = 330;

        bPlay = Tasto("⏸", "Pausa", () => Gira());
        barra.Child = riempimento;
        barra.MouseLeftButtonDown += (_, e) => { var f = e.GetPosition(barra).X / Math.Max(1, barra.ActualWidth); if (mp.NaturalDuration.HasTimeSpan) mp.Position = TimeSpan.FromSeconds(mp.NaturalDuration.TimeSpan.TotalSeconds * Math.Clamp(f, 0, 1)); };

        var tasti = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        if (brani.Count > 1) tasti.Children.Add(Tasto("⏮", "Precedente", () => Vai(i - 1)));
        tasti.Children.Add(bPlay);
        if (brani.Count > 1) tasti.Children.Add(Tasto("⏭", "Successivo", () => Vai(i + 1)));
        var destra = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        destra.Children.Add(Tasto("↗", "Apri nell'app (con tutto il lettore)", () => { mp.Pause(); Regia.Gestisci(new Richiesta(null, [brani[i]], false, true)); Close(); }, true));
        destra.Children.Add(Tasto("⋯", "Apri, converti, comprimi", () => avatar.ApriFumetto(brani)));
        destra.Children.Add(Tasto("✕", "Chiudi", () => Close()));
        var riga = new Grid();
        riga.Children.Add(tasti);
        riga.Children.Add(destra);

        var pila = new StackPanel { Margin = new Thickness(14, 10, 14, 10) };
        pila.Children.Add(titolo);
        pila.Children.Add(barra);
        pila.Children.Add(tempo);
        pila.Children.Add(riga);
        Content = new Border
        {
            CornerRadius = new CornerRadius(16), Margin = new Thickness(14), Child = pila,
            Background = new SolidColorBrush(Color.FromArgb(214, 18, 16, 26)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(46, 255, 255, 255)), BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect { BlurRadius = 26, ShadowDepth = 8, Opacity = 0.5, Direction = 270 },
        };

        mp.MediaEnded += (_, _) => { if (i + 1 < brani.Count) Vai(i + 1); else Ferma(); };
        mp.MediaFailed += (_, _) =>
        {
            // un formato che Windows non suona da solo (OGG, OPUS…): lo suona il lettore dell'app
            Regia.Gestisci(new Richiesta(null, [brani[i]], false, true));
            Close();
        };
        orologio = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Normal, (_, _) => Aggiorna(), Dispatcher);
        Loaded += (_, _) => { Posiziona(); Vai(0); orologio.Start(); };
        avatar.Spostato += Posiziona;
        Closed += (_, _) => { avatar.Spostato -= Posiziona; orologio.Stop(); mp.Stop(); mp.Close(); StatoCambiato?.Invoke(false); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); else if (e.Key == Key.Space) Gira(); };
    }

    Button Tasto(string glifo, string tip, Action fai, bool oro = false)
    {
        var b = new Button
        {
            Content = new TextBlock { Text = glifo, FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = 15, Foreground = oro ? new SolidColorBrush(Color.FromRgb(0xff, 0xd5, 0x4a)) : Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            Width = 34, Height = 30, Margin = new Thickness(0, 0, 4, 0), ToolTip = tip, Cursor = Cursors.Hand, Focusable = false, BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
        };
        var t = new ControlTemplate(typeof(Button));
        var bordo = new FrameworkElementFactory(typeof(Border)) { Name = "b" };
        bordo.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
        bordo.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        bordo.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        t.VisualTree = bordo;
        var su = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        su.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(60, 255, 213, 74)), "b"));
        t.Triggers.Add(su);
        b.Template = t;
        b.Click += (_, _) => fai();
        return b;
    }

    void Posiziona()
    {
        var a = avatar.Area; var area = SystemParameters.WorkArea;
        Left = Math.Clamp(a.Left + a.Width / 2 - Width / 2, area.Left, area.Right - Width);
        var sotto = a.Bottom - 10;
        Top = sotto + ActualHeight <= area.Bottom ? sotto : Math.Max(area.Top, a.Top - ActualHeight + 10);
    }

    void Vai(int n)
    {
        i = (n + brani.Count) % brani.Count;
        titolo.Text = System.IO.Path.GetFileNameWithoutExtension(brani[i]);
        mp.Open(new Uri(brani[i]));
        mp.Play();
        Stato(true);
    }

    void Gira() { if (suona) { mp.Pause(); Stato(false); } else { mp.Play(); Stato(true); } }
    void Ferma() { mp.Stop(); Stato(false); }

    void Stato(bool si)
    {
        suona = si;
        ((TextBlock)bPlay.Content).Text = si ? "⏸" : "▶";
        bPlay.ToolTip = si ? "Pausa" : "Suona";
        StatoCambiato?.Invoke(si);
    }

    static string Tm(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";

    void Aggiorna()
    {
        if (!mp.NaturalDuration.HasTimeSpan) { tempo.Text = Tm(mp.Position); return; }
        var tot = mp.NaturalDuration.TimeSpan;
        tempo.Text = $"{Tm(mp.Position)} / {Tm(tot)}" + (brani.Count > 1 ? $"  ·  {i + 1} di {brani.Count}" : "");
        riempimento.Width = tot.TotalSeconds > 0 ? barra.ActualWidth * Math.Clamp(mp.Position.TotalSeconds / tot.TotalSeconds, 0, 1) : 0;
    }
}
