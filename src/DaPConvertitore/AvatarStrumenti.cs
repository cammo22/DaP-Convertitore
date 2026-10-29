using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DaP.Convertitore.App;

/// <summary>I pezzi in comune delle finestrelle di vetro che stanno accanto all'avatar (cronometro, scelta del personaggio).</summary>
static class Pezzi
{
    public const string IcPlay = "M8 5v14l11-7z", IcPausa = "M7 5h3.6v14H7zM13.4 5H17v14h-3.6z";
    public const string IcChiudi = "M6 6l12 12M18 6L6 18";
    public const string IcAzzera = "M3 12a9 9 0 1 0 3-6.7L3 8M3 3v5h5";
    public const string IcGiro = "M5 21V4M5 4h12l-2.5 4L17 12H5";

    public static SolidColorBrush Col(string c) => (SolidColorBrush)new BrushConverter().ConvertFromString(c)!;

    public static UIElement Icona(string disegno, Brush colore, double misura, bool piena)
    {
        var d = new System.Windows.Shapes.Path { Data = Geometry.Parse(disegno) };
        if (piena) { d.Fill = colore; d.Stroke = colore; d.StrokeThickness = 1.2; d.StrokeLineJoin = PenLineJoin.Round; }
        else { d.Stroke = colore; d.StrokeThickness = 1.9; d.StrokeStartLineCap = d.StrokeEndLineCap = PenLineCap.Round; d.StrokeLineJoin = PenLineJoin.Round; }
        var tela = new Canvas { Width = 24, Height = 24 };
        tela.Children.Add(d);
        return new Viewbox { Width = misura, Height = misura, Child = tela, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    }

    /// <summary>Un tasto tondo con un'icona disegnata: pieno (oro), oro (solo l'icona) o bianco.</summary>
    public static Button Tasto(string disegno, string tip, Action fai, double lato = 34, bool pieno = false, bool oro = false, bool piena = false)
    {
        var colore = pieno ? Col("#09080d") : oro ? Col("#ffd54a") : Brushes.White;
        var b = new Button
        {
            Content = Icona(disegno, colore, lato >= 50 ? 26 : lato >= 40 ? 19 : lato >= 32 ? 17 : 13, piena),
            Width = lato, Height = lato, Margin = new Thickness(4, 0, 4, 0), ToolTip = tip, Cursor = Cursors.Hand, Focusable = false, BorderThickness = new Thickness(0),
            Background = pieno ? Col("#ffd54a") : new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
        };
        var t = new ControlTemplate(typeof(Button));
        var bordo = new FrameworkElementFactory(typeof(Border)) { Name = "b" };
        bordo.SetValue(Border.CornerRadiusProperty, new CornerRadius(lato / 2));
        bordo.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        bordo.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        t.VisualTree = bordo;
        var su = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        su.Setters.Add(new Setter(Border.BackgroundProperty, pieno ? Col("#ffe68a") : new SolidColorBrush(Color.FromArgb(60, 255, 213, 74)), "b"));
        t.Triggers.Add(su);
        b.Template = t;
        b.Click += (_, _) => fai();
        return b;
    }

    /// <summary>Finestra senza cornice, trasparente, sempre sopra: il vetro scuro dei popup di DaProd.</summary>
    public static void Vetro(Window w, double larghezza, bool attiva)
    {
        w.WindowStyle = WindowStyle.None; w.AllowsTransparency = true; w.Background = Brushes.Transparent;
        w.Topmost = true; w.ShowInTaskbar = false; w.ShowActivated = attiva; w.ResizeMode = ResizeMode.NoResize;
        w.SizeToContent = SizeToContent.Height; w.Width = larghezza;
    }

    public static Border Pannello(UIElement dentro) => new()
    {
        CornerRadius = new CornerRadius(16), Margin = new Thickness(14), Child = dentro,
        Background = new SolidColorBrush(Color.FromArgb(220, 18, 16, 26)),
        BorderBrush = new SolidColorBrush(Color.FromArgb(46, 255, 255, 255)), BorderThickness = new Thickness(1),
        Effect = new DropShadowEffect { BlurRadius = 26, ShadowDepth = 8, Opacity = 0.5, Direction = 270 },
    };

    public static TextBlock Titolo(string testo) => new() { Text = testo, Foreground = Col("#ffd54a"), FontWeight = FontWeights.Bold, FontSize = 11.5, FontFamily = new FontFamily("Orbitron, Segoe UI"), VerticalAlignment = VerticalAlignment.Center };
}

/// <summary>
/// Il cronometro: si apre dal tasto destro sull'avatar e parte subito, ma poi è una finestra a parte: si trascina dove vuoi (l'avatar
/// resta dov'è) e si ricorda dove l'hai lasciato. Il tasto «Giro» segna il tempo del giro, riparte da zero e dice se è stato più
/// veloce o più lento del precedente.
/// </summary>
public sealed class Cronometro : Window
{
    readonly FinestraAvatar avatar;
    readonly Stopwatch tempo = new();
    readonly List<TimeSpan> giri = [];
    readonly TextBlock cifre = new() { Foreground = Pezzi.Col("#ffd54a"), FontFamily = new FontFamily("Cascadia Mono, Consolas, Segoe UI"), FontWeight = FontWeights.Bold, FontSize = 34, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock stato = new() { Foreground = Pezzi.Col("#a19db0"), FontSize = 12, Margin = new Thickness(2, 2, 0, 0) };
    readonly Border contenitoreVia = new();
    readonly StackPanel elencoGiri = new();
    readonly ScrollViewer scorrevole;
    readonly DispatcherTimer orologio;

    public Cronometro(FinestraAvatar avatar)
    {
        this.avatar = avatar;
        Pezzi.Vetro(this, 372, false);

        var testa = new Grid();
        testa.Children.Add(Pezzi.Titolo("CRONOMETRO"));
        var chiudi = Pezzi.Tasto(Pezzi.IcChiudi, "Chiudi il cronometro", Close, 26);
        chiudi.HorizontalAlignment = HorizontalAlignment.Right; chiudi.Margin = new Thickness(0);
        testa.Children.Add(chiudi);

        var tasti = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        tasti.Children.Add(Pezzi.Tasto(Pezzi.IcGiro, "Giro: segna il tempo e riparte da zero", Giro, 38, oro: true));
        tasti.Children.Add(contenitoreVia);
        tasti.Children.Add(Pezzi.Tasto(Pezzi.IcAzzera, "Azzera (cancella anche i giri)", Azzera, 34));
        var riga = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        riga.ColumnDefinitions.Add(new ColumnDefinition());
        riga.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        riga.Children.Add(cifre);
        Grid.SetColumn(tasti, 1); riga.Children.Add(tasti);

        scorrevole = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 170, Margin = new Thickness(0, 10, 0, 0), Content = elencoGiri, Visibility = Visibility.Collapsed };

        var pila = new StackPanel { Margin = new Thickness(18, 14, 14, 14) };
        pila.Children.Add(testa);
        pila.Children.Add(riga);
        pila.Children.Add(stato);
        pila.Children.Add(scorrevole);
        Content = Pezzi.Pannello(pila);

        orologio = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Normal, (_, _) => Aggiorna(), Dispatcher);
        Loaded += (_, _) => { Posiziona(); tempo.Start(); orologio.Start(); Aggiorna(); };
        Closed += (_, _) => orologio.Stop();
        // si trascina da qualsiasi punto che non sia un tasto, e si ricorda dove l'hai messo: l'avatar non c'entra
        MouseLeftButtonDown += (_, e) => { try { DragMove(); } catch { /* il mouse è già stato rilasciato */ } Ricorda(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); else if (e.Key == Key.Space) Gira(); else if (e.Key is Key.L or Key.Enter) Giro(); };
    }

    void Gira() { if (tempo.IsRunning) tempo.Stop(); else tempo.Start(); Aggiorna(); }

    void Azzera() { tempo.Reset(); giri.Clear(); elencoGiri.Children.Clear(); scorrevole.Visibility = Visibility.Collapsed; Aggiorna(); }

    /// <summary>Segna il giro: il tempo di adesso entra nell'elenco (il più recente in cima) e si riparte da zero.</summary>
    void Giro()
    {
        var t = tempo.Elapsed;
        if (t < TimeSpan.FromMilliseconds(100)) return;
        var inCorso = tempo.IsRunning;
        tempo.Reset();
        if (inCorso) tempo.Start();
        var prima = giri.Count > 0 ? giri[^1] : (TimeSpan?)null;
        var migliore = giri.Count == 0 || t < giri.Min();
        giri.Add(t);

        var n = giri.Count;
        var voce = new Grid { Margin = new Thickness(0, 0, 8, 6) };
        voce.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
        voce.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
        voce.ColumnDefinitions.Add(new ColumnDefinition());
        voce.Children.Add(new TextBlock { Text = $"Giro {n}", Foreground = Pezzi.Col("#a19db0"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        var ora = new TextBlock { Text = Tm(t), Foreground = Brushes.White, FontFamily = new FontFamily("Cascadia Mono, Consolas, Segoe UI"), FontWeight = FontWeights.Bold, FontSize = 15, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(ora, 1); voce.Children.Add(ora);
        string testo; string colore;
        if (prima is null) { testo = "primo giro"; colore = "#a19db0"; }
        else
        {
            var d = (t - prima.Value).TotalSeconds;
            if (Math.Abs(d) < 0.05) { testo = "uguale al precedente"; colore = "#a19db0"; }
            else if (d < 0) { testo = $"▼ {Math.Abs(d):0.0} s più veloce".Replace('.', ','); colore = "#5dffb4"; }
            else { testo = $"▲ {d:0.0} s più lento".Replace('.', ','); colore = "#ff4d6d"; }
        }
        if (migliore && prima is not null) testo += "  ★ il migliore";
        var conf = new TextBlock { Text = testo, Foreground = Pezzi.Col(colore), FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(conf, 2); voce.Children.Add(conf);
        elencoGiri.Children.Insert(0, voce);
        scorrevole.Visibility = Visibility.Visible;
        scorrevole.ScrollToTop();
        Aggiorna();
    }

    static string Tm(TimeSpan t) => t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 100}" : $"{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 100}";

    void Aggiorna()
    {
        var t = tempo.Elapsed;
        cifre.Text = Tm(t);
        var qui = tempo.IsRunning ? "in corso" : t == TimeSpan.Zero && giri.Count == 0 ? "a zero" : "in pausa";
        stato.Text = giri.Count == 0 ? qui : $"{qui}  ·  {giri.Count} {(giri.Count == 1 ? "giro" : "giri")}  ·  totale {Tm(giri.Aggregate(t, (s, g) => s + g))}";
        // un solo tasto per partire e fermarsi: il disegno cambia, il tasto si rifà solo quando serve
        var inCorso = tempo.IsRunning;
        if (contenitoreVia.Tag as bool? != inCorso)
        {
            contenitoreVia.Tag = inCorso;
            contenitoreVia.Child = Pezzi.Tasto(inCorso ? Pezzi.IcPausa : Pezzi.IcPlay, inCorso ? "Pausa" : "Riprendi", Gira, 46, pieno: true, piena: true);
        }
    }

    /// <summary>La prima volta accanto all'avatar (sopra, se c'è posto); dopo, dove l'hai lasciato l'ultima volta.</summary>
    void Posiziona()
    {
        var area = SystemParameters.WorkArea;
        var imp = Impostazioni.Comune;
        if (imp.CronometroX is { } x && imp.CronometroY is { } y && x > area.Left - Width + 60 && x < area.Right - 60 && y > area.Top - 20 && y < area.Bottom - 60)
        { Left = x; Top = y; return; }
        var a = avatar.Area;
        Left = Math.Clamp(a.Left + a.Width / 2 - Width / 2, area.Left, area.Right - Width);
        var sopra = a.Top - ActualHeight + 10;
        Top = sopra >= area.Top ? sopra : Math.Min(a.Bottom - 10, area.Bottom - ActualHeight);
    }

    void Ricorda()
    {
        var imp = Impostazioni.Comune;
        imp.CronometroX = Left; imp.CronometroY = Top;
        imp.Salva();
    }
}

/// <summary>
/// La scelta del personaggio: le figurine di tutti gli oggetti dell'avatar (si tocca per cambiare) e l'interruttore del cambio
/// automatico. Spento, l'avatar non si trasforma più da solo: resta quello scelto finché non lo cambi tu.
/// </summary>
public sealed class ScegliPersonaggio : Window
{
    readonly FinestraAvatar avatar;
    readonly Dictionary<string, Border> carte = [];
    readonly Border levetta = new() { Width = 18, Height = 18, CornerRadius = new CornerRadius(9), Background = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
    readonly Border guida = new() { Width = 44, Height = 24, CornerRadius = new CornerRadius(12) };
    readonly TextBlock spiega = new() { Foreground = Pezzi.Col("#a19db0"), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 0) };

    public static string Nome(string oggetto) => oggetto switch
    {
        "cassetta" => "Musicassetta", "vhs" => "VHS", "floppy" => "Floppy", "cd" => "CD", "vinile" => "Vinile",
        "bobina" => "Bobina", "minidisc" => "MiniDisc", "cartuccia" => "Cartuccia",
        _ => oggetto.Length > 0 ? char.ToUpper(oggetto[0]) + oggetto[1..] : oggetto,
    };

    public ScegliPersonaggio(FinestraAvatar avatar)
    {
        this.avatar = avatar;
        Pezzi.Vetro(this, 432, true);

        var testa = new Grid();
        testa.Children.Add(Pezzi.Titolo("SCEGLI IL PERSONAGGIO"));
        var chiudi = Pezzi.Tasto(Pezzi.IcChiudi, "Chiudi", Close, 26);
        chiudi.HorizontalAlignment = HorizontalAlignment.Right; chiudi.Margin = new Thickness(0);
        testa.Children.Add(chiudi);

        var figurine = new WrapPanel { Margin = new Thickness(-4, 12, 0, 0) };
        foreach (var oggetto in FoglioAvatar.Disponibili())
        {
            var carta = Carta(oggetto);
            carte[oggetto] = carta;
            figurine.Children.Add(carta);
        }

        // il cambio automatico: acceso, ogni tanto l'avatar si trasforma da solo; spento, resta quello scelto
        var testi = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        testi.Children.Add(new TextBlock { Text = "Cambio automatico", Foreground = Pezzi.Col("#ebe7f4"), FontWeight = FontWeights.Bold, FontSize = 14 });
        testi.Children.Add(spiega);
        guida.Child = levetta;
        guida.Cursor = Cursors.Hand; guida.VerticalAlignment = VerticalAlignment.Center;
        guida.MouseLeftButtonUp += (_, _) => { var imp = Impostazioni.Comune; imp.AvatarCambioAuto = !imp.AvatarCambioAuto; imp.Salva(); Interruttore(); };
        var piede = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        piede.ColumnDefinitions.Add(new ColumnDefinition());
        piede.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        piede.Children.Add(testi);
        Grid.SetColumn(guida, 1); piede.Children.Add(guida);

        var linea = new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)), Margin = new Thickness(0, 12, 0, 0) };
        var pila = new StackPanel { Margin = new Thickness(18, 14, 18, 16) };
        pila.Children.Add(testa);
        pila.Children.Add(figurine);
        pila.Children.Add(linea);
        pila.Children.Add(piede);
        Content = Pezzi.Pannello(pila);

        Interruttore();
        Evidenzia();
        Loaded += (_, _) => Posiziona();
        avatar.Spostato += Posiziona;
        avatar.Trasformato += Evidenzia;
        Deactivated += (_, _) => Close();
        Closed += (_, _) => { avatar.Spostato -= Posiziona; avatar.Trasformato -= Evidenzia; };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    Border Carta(string oggetto)
    {
        var foto = new Image { Width = 70, Height = 70, Stretch = Stretch.Uniform };
        try { foto.Source = FoglioAvatar.Carica(oggetto)?.Fotogramma("idle", 0); } catch { /* resta senza figurina */ }
        var pila = new StackPanel();
        pila.Children.Add(foto);
        pila.Children.Add(new TextBlock { Text = Nome(oggetto), Foreground = Pezzi.Col("#ebe7f4"), FontSize = 12, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });
        var carta = new Border
        {
            Width = 96, Height = 106, Margin = new Thickness(4), Padding = new Thickness(0, 8, 0, 6), CornerRadius = new CornerRadius(12), Cursor = Cursors.Hand, Child = pila,
            Background = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)), BorderThickness = new Thickness(1.5), BorderBrush = Brushes.Transparent, ToolTip = Nome(oggetto),
        };
        carta.MouseEnter += (_, _) => { if (avatar.Oggetto != oggetto) carta.Background = new SolidColorBrush(Color.FromArgb(40, 255, 213, 74)); };
        carta.MouseLeave += (_, _) => { if (avatar.Oggetto != oggetto) carta.Background = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)); };
        carta.MouseLeftButtonUp += (_, _) => avatar.Cambia(oggetto);
        return carta;
    }

    void Evidenzia()
    {
        foreach (var (oggetto, carta) in carte)
        {
            var scelto = avatar.Oggetto == oggetto;
            carta.BorderBrush = scelto ? Pezzi.Col("#ffd54a") : Brushes.Transparent;
            carta.Background = scelto ? new SolidColorBrush(Color.FromArgb(46, 255, 213, 74)) : new SolidColorBrush(Color.FromArgb(18, 255, 255, 255));
        }
    }

    void Interruttore()
    {
        var auto = Impostazioni.Comune.AvatarCambioAuto;
        guida.Background = auto ? Pezzi.Col("#ffd54a") : new SolidColorBrush(Color.FromArgb(70, 255, 255, 255));
        levetta.Margin = new Thickness(auto ? 23 : 3, 0, 0, 0);
        levetta.HorizontalAlignment = HorizontalAlignment.Left;
        spiega.Text = auto ? "Ogni tanto si trasforma da solo in un altro." : "Bloccato: resta quello che hai scelto.";
    }

    void Posiziona()
    {
        var a = avatar.Area; var area = SystemParameters.WorkArea;
        Left = Math.Clamp(a.Left + a.Width / 2 - Width / 2, area.Left, area.Right - Width);
        var sopra = a.Top - ActualHeight + 10;
        Top = sopra >= area.Top ? sopra : Math.Min(a.Bottom - 10, Math.Max(area.Top, area.Bottom - ActualHeight));
    }
}
