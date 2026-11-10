using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;

namespace DaP.Convertitore.App;

/// <summary>
/// Il pannello di registrazione. Si trascina da qualsiasi punto vuoto, si ridimensiona, si ricorda
/// dove stava e quanto era grande, e non esce dagli schermi. Si aggancia all'avatar e si stacca
/// trascinandolo. «Riduci» lo fa diventare la barra. È escluso dalla cattura, ma è opaco: così si vede.
/// </summary>
public sealed class MenuRegistrazione : Window
{
    static readonly SolidColorBrush Fondo = Colore(0x12, 0x10, 0x1A);
    static readonly SolidColorBrush Oro = Colore(0xFF, 0xD5, 0x4A);
    static readonly SolidColorBrush Testo = Colore(0xF4, 0xF0, 0xE6);
    static readonly SolidColorBrush Spento = Colore(0x2A, 0x26, 0x36);
    static readonly SolidColorBrush Muto = Colore(0xA1, 0x9D, 0xB0);
    static readonly SolidColorBrush Magenta = Colore(0xFF, 0x3D, 0xF2);

    static MenuRegistrazione? aperta;
    readonly Image quadro = new() { Stretch = Stretch.Uniform };
    readonly TextBlock soloAudioTesto;
    readonly TextBlock contoTesto;
    readonly TextBlock tempo;
    readonly TextBlock nota;
    readonly TextBlock aggancioTesto;
    readonly ComboBox monitor = Combo();
    readonly ComboBox microfono = Combo();
    readonly ComboBox uscita = Combo();
    readonly ComboBox finestre = Combo();
    readonly StackPanel impostazioni = new();
    readonly Border barraMic = BarraLivello();
    readonly Border barraPc = BarraLivello();
    readonly Button avvia, pausa, salva, annulla;
    readonly Button areaIntero, areaZona, areaFinestra, fps30, fps60, qLeggera, qBuona, qAlta;
    readonly CheckBox cursore, conto, solo;
    readonly AnteprimaSchermo anteprima;
    readonly DispatcherTimer orologio;
    AudioWasapi.Metro? metro;
    Window? ancora;
    Ancoraggio.Riquadro? zona;
    string area = "intero";
    string qualita = "buona";
    bool blocco = true;
    bool sistemo;
    int fps = 30;

    public static bool Visibile => aperta is { IsVisible: true };

    public static void Mostra(Window? accanto, string da)
    {
        if (Registrazione.Attiva && aperta is null) { BarraRegistrazione.Mostra(); return; }
        if (aperta is not null)
        {
            BarraRegistrazione.Chiudi();
            aperta.Aggancia(accanto);
            aperta.Show();
            aperta.Activate();
            TastiRegistrazione.Accendi();
            Registro.Scrivi("registrazione: il pannello era già aperto, lo porto davanti");
            return;
        }
        try
        {
            aperta = new MenuRegistrazione(accanto, da);
            Registrazione.Escludi(aperta);
            aperta.Closed += (_, _) =>
            {
                Registro.Scrivi("registrazione: pannello chiuso");
                PosizioneFinestre.Dimentica(aperta!);
                aperta = null;
                TastiRegistrazione.Sistema();
            };
            aperta.Show();
            aperta.Activate();
            aperta.Topmost = true;
            TastiRegistrazione.Accendi();
        }
        catch (Exception e)
        {
            aperta = null;
            Registro.Errore("registrazione: finestra", e);
            Vassoio.Dire("Registra non si è aperto", e.Message);
        }
    }

    public static void PortaDavanti()
    {
        if (aperta is null) { BarraRegistrazione.PortaDavanti(); return; }
        BarraRegistrazione.Chiudi();
        aperta.Show();
        aperta.Topmost = true;
        try { aperta.Activate(); } catch { /* già davanti */ }
    }

    public static void Aggiorna() => aperta?.Rinfresca();

    /// <summary>F9, o il tasto Pausa: parte, mette in pausa, o riprende.</summary>
    public static void TastoPausa()
    {
        if (Registrazione.Attiva)
        {
            if (Registrazione.InPausa) Registrazione.Riprendi();
            else Registrazione.Pausa();
            Aggiorna();
            BarraRegistrazione.Aggiorna();
            return;
        }
        if (aperta is not null) _ = aperta.Parti();
        else Mostra(Registrazione.Ancora, "tasto");
    }

    MenuRegistrazione(Window? accanto, string da)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.CanResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        MinWidth = 380;
        MinHeight = 460;
        Width = 440;
        Height = 680;
        Background = Fondo;
        Title = "Registra";
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        });

        soloAudioTesto = new TextBlock
        {
            Text = "Solo audio", Foreground = Oro, FontSize = 22, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        contoTesto = new TextBlock
        {
            Foreground = Oro, FontSize = 72, FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        var palco = new Grid { Background = Brushes.Black, MinHeight = 120 };
        palco.Children.Add(quadro);
        palco.Children.Add(soloAudioTesto);
        palco.Children.Add(contoTesto);
        anteprima = new AnteprimaSchermo(quadro);

        tempo = new TextBlock
        {
            Text = "00:00", Foreground = Oro, FontFamily = new FontFamily("Consolas"), FontSize = 22,
            FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, MinWidth = 72,
        };
        var rigaTempo = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 6) };
        rigaTempo.Children.Add(tempo);
        rigaTempo.Children.Add(EtichettaLivello("Mic", barraMic));
        rigaTempo.Children.Add(EtichettaLivello("PC", barraPc));

        avvia = Tasto("Avvia", Magenta, () => _ = Parti());
        pausa = Tasto("Pausa", Spento, () => TastoPausa());
        salva = Tasto("Salva", Colore(0x1C, 0x9A, 0x62), Registrazione.Salva);
        annulla = Tasto("Annulla", Colore(0x8A, 0x2A, 0x3C), Registrazione.Annulla);
        pausa.Visibility = Visibility.Collapsed;
        avvia.ToolTip = "F9";
        pausa.ToolTip = "F9";
        salva.ToolTip = "F10";
        var rigaTasti = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        foreach (var b in new[] { avvia, pausa, salva, annulla }) { b.Margin = new Thickness(0, 0, 8, 8); rigaTasti.Children.Add(b); }

        areaIntero = Mini("Monitor", () => ScegliArea("intero"));
        areaZona = Mini("Zona", () => ScegliArea("zona"));
        areaFinestra = Mini("Finestra", () => ScegliArea("finestra"));
        var scegliZona = Mini("Scegli la zona", () => _ = PrendiZona());
        fps30 = Mini("30 fps", () => ScegliFps(30));
        fps60 = Mini("60 fps", () => ScegliFps(60));
        qLeggera = Mini("Leggera", () => ScegliQualita("leggera"));
        qBuona = Mini("Buona", () => ScegliQualita("buona"));
        qAlta = Mini("Alta", () => ScegliQualita("alta"));
        cursore = Spunta("Mostra il cursore");
        conto = Spunta("Conto alla rovescia");
        solo = Spunta("Solo audio");

        var griglia = new Grid { Margin = new Thickness(14, 10, 14, 12) };
        griglia.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        griglia.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 100 });
        griglia.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        griglia.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        griglia.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.2, GridUnitType.Star) });

        var testa = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
        var titolo = new TextBlock { Text = "REGISTRA", Foreground = Oro, FontWeight = FontWeights.Bold, FontSize = 13 };
        var comandi = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        aggancioTesto = Voce("Stacca", () => GiraAggancio());
        comandi.Children.Add(aggancioTesto);
        comandi.Children.Add(Voce("Riduci", () => Riduci()));
        comandi.Children.Add(Voce("Chiudi", () => Close()));
        DockPanel.SetDock(comandi, Dock.Right);
        testa.Children.Add(comandi);
        testa.Children.Add(titolo);
        Grid.SetRow(testa, 0);
        griglia.Children.Add(testa);
        Grid.SetRow(palco, 1);
        griglia.Children.Add(palco);
        var mezzo = new StackPanel();
        mezzo.Children.Add(rigaTempo);
        mezzo.Children.Add(rigaTasti);
        Grid.SetRow(mezzo, 2);
        griglia.Children.Add(mezzo);

        impostazioni.Children.Add(Etichetta("SORGENTE"));
        impostazioni.Children.Add(monitor);
        impostazioni.Children.Add(Riga(areaIntero, areaZona, areaFinestra, scegliZona));
        impostazioni.Children.Add(finestre);
        impostazioni.Children.Add(Etichetta("AUDIO"));
        impostazioni.Children.Add(new TextBlock { Text = "Microfono", Foreground = Muto, FontSize = 12, Margin = new Thickness(0, 2, 0, 0) });
        impostazioni.Children.Add(microfono);
        impostazioni.Children.Add(new TextBlock { Text = "Audio del PC", Foreground = Muto, FontSize = 12 });
        impostazioni.Children.Add(uscita);
        impostazioni.Children.Add(Etichetta("QUALITÀ"));
        impostazioni.Children.Add(Riga(fps30, fps60, qLeggera, qBuona, qAlta));
        impostazioni.Children.Add(cursore);
        impostazioni.Children.Add(conto);
        impostazioni.Children.Add(solo);
        nota = new TextBlock
        {
            Text = "Cerco microfoni e l'audio del PC…",
            Foreground = Muto, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0),
        };
        impostazioni.Children.Add(nota);
        impostazioni.Children.Add(new TextBlock
        {
            Text = "F9 avvia o mette in pausa, F10 salva. Questa finestra non entra nel video: nell'anteprima, dove sta lei, si vede un buco.",
            Foreground = Colore(0x6E, 0x6A, 0x7A), FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0),
        });
        var scorrere = new ScrollViewer { Content = impostazioni, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scorrere, 4);
        griglia.Children.Add(scorrere);
        Content = new Border { Background = Fondo, BorderBrush = Oro, BorderThickness = new Thickness(1), Child = griglia };

        monitor.SelectionChanged += (_, _) => { if (!blocco) { Ricorda(); AggiornaAnteprima(); } };
        microfono.SelectionChanged += (_, _) => Ricorda();
        uscita.SelectionChanged += (_, _) => Ricorda();
        finestre.SelectionChanged += (_, _) => { if (!blocco) { Ricorda(); AggiornaAnteprima(); } };
        cursore.Checked += (_, _) => Ricorda();
        cursore.Unchecked += (_, _) => Ricorda();
        conto.Checked += (_, _) => Ricorda();
        conto.Unchecked += (_, _) => Ricorda();
        solo.Checked += (_, _) => { if (!blocco) { Ricorda(); AggiornaAnteprima(); } };
        solo.Unchecked += (_, _) => { if (!blocco) { Ricorda(); AggiornaAnteprima(); } };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { RiduciOChiudi(); e.Handled = true; }
            else if (e.Key == Key.F9) { TastoPausa(); e.Handled = true; }
            else if (e.Key == Key.F10 && Registrazione.Attiva) { Registrazione.Salva(); e.Handled = true; }
        };
        MouseLeftButtonDown += (_, e) =>
        {
            if (Interattivo(e.OriginalSource) || e.LeftButton != MouseButtonState.Pressed) return;
            Stacca();
            try { DragMove(); } catch { /* il mouse è già stato rilasciato */ }
            RestaNegliSchermi();
            RicordaPosto();
        };

        orologio = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        orologio.Tick += (_, _) =>
        {
            if (!IsVisible) return;
            if (Registrazione.Attiva || Registrazione.InPausa) tempo.Text = Registrazione.TestoTempo();
            Livelli();
        };
        SourceInitialized += (_, _) => Posiziona(false);
        ContentRendered += (_, _) =>
        {
            Posiziona(true);
            Registro.Scrivi($"registrazione: pannello aperto da {da}");
        };
        IsVisibleChanged += (_, _) => VistaCambiata();
        SizeChanged += (_, _) =>
        {
            if (sistemo || !IsVisible || Mouse.LeftButton == MouseButtonState.Pressed) return;
            RestaNegliSchermi();
            RicordaPosto();
        };
        Loaded += async (_, _) =>
        {
            RiempiMonitori();
            RiempiFinestre();
            ApplicaSalvato();
            blocco = false;
            SegnaTutto();
            VistaCambiata();
            Rinfresca();
            DispositiviAudio d;
            try { d = await Task.Run(Registrazione.Elenca); }
            catch (Exception e)
            {
                Registro.Errore("registrazione: dispositivi", e);
                nota.Text = e.Message;
                return;
            }
            if (!IsLoaded) return;
            blocco = true;
            var imp = Impostazioni.Comune;
            RiempiAudio(microfono, d.Microfoni, imp.RegistrazioneMic);
            RiempiAudio(uscita, d.Uscite, imp.RegistrazionePc);
            blocco = false;
            nota.Text = d.Microfoni.Count == 0 && d.Uscite.Count == 0
                ? "Non vedo dispositivi audio. Li elenca Windows (WASAPI), non FFmpeg."
                : "Scegli e premi Avvia. La scelta resta per la prossima volta.";
            SegnaAggancio();
        };
        Closed += (_, _) =>
        {
            orologio.Stop();
            anteprima.Dispose();
            metro?.Dispose();
            metro = null;
            if (ancora is not null) ancora.LocationChanged -= Segui;
        };
        Aggancia(accanto);
    }

    async Task Parti()
    {
        var modo = ModoScelto();
        var mic = Valore(microfono);
        var pc = Valore(uscita);
        var titolo = Valore(finestre);
        var perche = ComandiRegistrazione.MotivoRifiuto(modo, mic, pc, zona is null, string.IsNullOrWhiteSpace(titolo));
        if (perche is not null) { nota.Text = perche; Registro.Scrivi("registrazione: " + perche); return; }
        avvia.IsEnabled = false;
        if (conto.IsChecked == true && modo != "audio")
        {
            if (!IsVisible) { BarraRegistrazione.Chiudi(); Show(); }
            for (var n = 3; n >= 1 && IsVisible; n--)
            {
                contoTesto.Text = n.ToString();
                contoTesto.Visibility = Visibility.Visible;
                await Task.Delay(1000);
            }
            if (!IsVisible || !IsLoaded) return;
            contoTesto.Visibility = Visibility.Collapsed;
        }
        ComandiRegistrazione.Schermo? schermo = null;
        if (modo == "zona" && zona is { } z) schermo = new(z.X, z.Y, z.L, z.A);
        else if (modo is "intero" or "schermo") { var m = MonitorScelto(); schermo = new(m.X, m.Y, m.L, m.A); }
        var modoFf = modo == "audio" ? "audio" : modo == "finestra" ? "finestra" : modo == "zona" ? "zona" : "schermo";
        Registro.Scrivi($"registrazione: scelto {modoFf}, mic «{mic ?? "no"}», pc «{pc ?? "no"}», {fps} fps");
        var ok = Registrazione.Inizia(modoFf, mic, pc, schermo, fps, ComandiRegistrazione.CrfDi(qualita), cursore.IsChecked == true, titolo);
        if (!ok) nota.Text = "Non è partita. Il motivo è nel registro.";
        Rinfresca();
        AggiornaAnteprima();
    }

    string ModoScelto() => solo.IsChecked == true ? "audio" : area;

    void ScegliArea(string quale)
    {
        area = quale;
        finestre.Visibility = quale == "finestra" ? Visibility.Visible : Visibility.Collapsed;
        SegnaTutto();
        Ricorda();
        AggiornaAnteprima();
        if (quale == "finestra") RiempiFinestre();
    }

    async Task PrendiZona()
    {
        var m = MonitorScelto();
        nota.Text = "Trascina la zona sul monitor. Esc annulla.";
        Hide();
        Ancoraggio.Riquadro? presa;
        try { presa = await SceltaZona.Chiedi(m); }
        finally { Show(); try { Activate(); } catch { /* già chiusa */ } }
        if (presa is { } r && Ancoraggio.Tocca(r, m))
        {
            zona = Ancoraggio.Blocca(r, m);
            ScegliArea("zona");
            nota.Text = $"Zona {zona.Value.L}×{zona.Value.A}.";
            Registro.Scrivi($"registrazione: zona {Ancoraggio.Chiave(zona.Value)}");
        }
    }

    void ScegliFps(int n) { fps = n; SegnaTutto(); Ricorda(); }
    void ScegliQualita(string q) { qualita = q; SegnaTutto(); Ricorda(); }

    void AggiornaAnteprima()
    {
        if (!IsVisible) { anteprima.Ferma(); return; }
        var audio = solo.IsChecked == true;
        soloAudioTesto.Visibility = audio ? Visibility.Visible : Visibility.Collapsed;
        if (audio) { anteprima.Ferma(); quadro.Source = null; return; }
        Ancoraggio.Riquadro r = area == "zona" && zona is { } z ? z
            : area == "finestra" && Valore(finestre) is { } titolo && RettangoloDi(titolo) is { } fr ? fr
            : MonitorScelto();
        anteprima.Guarda(r.X, r.Y, r.L, r.A);
    }

    void Rinfresca()
    {
        var on = Registrazione.Attiva;
        avvia.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
        avvia.IsEnabled = !on;
        pausa.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        salva.IsEnabled = on;
        annulla.IsEnabled = on;
        pausa.Content = Registrazione.InPausa ? "Riprendi" : "Pausa";
        tempo.Text = Registrazione.TestoTempo();
        impostazioni.IsEnabled = !on;
    }

    void Livelli()
    {
        metro ??= new AudioWasapi.Metro();
        barraMic.Width = 72 * metro.Microfono(Valore(microfono));
        barraPc.Width = 72 * metro.Uscita(Valore(uscita));
    }

    void VistaCambiata()
    {
        if (IsVisible)
        {
            if (!orologio.IsEnabled) orologio.Start();
            AggiornaAnteprima();
        }
        else
        {
            orologio.Stop();
            anteprima.Ferma();
            metro?.Dispose();
            metro = null;
        }
    }

    void Riduci()
    {
        RicordaPosto();
        Hide();
        BarraRegistrazione.Mostra();
    }

    void RiduciOChiudi()
    {
        if (Registrazione.Attiva) Riduci();
        else Close();
    }

    void Stacca()
    {
        var imp = Impostazioni.Comune;
        if (!imp.RegistrazioneAgganciata) return;
        imp.RegistrazioneAgganciata = false;
        imp.Salva();
        SegnaAggancio();
        Registro.Scrivi("registrazione: pannello staccato dall'avatar");
    }

    void GiraAggancio()
    {
        var imp = Impostazioni.Comune;
        imp.RegistrazioneAgganciata = !imp.RegistrazioneAgganciata;
        imp.Salva();
        SegnaAggancio();
        if (imp.RegistrazioneAgganciata) Posiziona(false);
        Registro.Scrivi(imp.RegistrazioneAgganciata ? "registrazione: pannello agganciato all'avatar" : "registrazione: pannello staccato dall'avatar");
    }

    void SegnaAggancio() => aggancioTesto.Text = Impostazioni.Comune.RegistrazioneAgganciata ? "Stacca" : "Aggancia";

    void Aggancia(Window? a)
    {
        if (ancora is not null) ancora.LocationChanged -= Segui;
        ancora = a;
        if (a is not null) Registrazione.Ancora = a;
        if (ancora is not null) ancora.LocationChanged += Segui;
        Posiziona(false);
    }

    void Segui(object? sender, EventArgs e)
    {
        if (Impostazioni.Comune.RegistrazioneAgganciata && IsVisible) Posiziona(false);
    }

    void Posiziona(bool scrivi)
    {
        if (sistemo) return;
        sistemo = true;
        try
        {
            var imp = Impostazioni.Comune;
            if (!imp.RegistrazioneAgganciata && Ancoraggio.ProvaChiave(imp.RegistrazionePosizione, out var salvata))
                PosizioneFinestre.Applica(this, salvata);
            else
                PosizioneFinestre.Accanto(this, ancora, 440, 680);
            if (scrivi && PosizioneFinestre.Leggi(this) is { } r)
                Registro.Scrivi($"registrazione: pannello a {r.X},{r.Y} ({r.L}×{r.A} px)");
        }
        finally { sistemo = false; }
    }

    void RestaNegliSchermi()
    {
        if (PosizioneFinestre.Leggi(this) is not { } r) return;
        sistemo = true;
        try { PosizioneFinestre.Applica(this, r); }
        finally { sistemo = false; }
    }

    void RicordaPosto()
    {
        if (blocco || !IsLoaded) return;
        if (PosizioneFinestre.Leggi(this) is not { } r) return;
        var imp = Impostazioni.Comune;
        imp.RegistrazionePosizione = Ancoraggio.Chiave(r);
        imp.Salva();
    }

    void RiempiMonitori()
    {
        monitor.Items.Clear();
        try
        {
            var n = 1;
            foreach (var s in System.Windows.Forms.Screen.AllScreens)
            {
                var b = s.Bounds;
                var nome = s.Primary ? "Principale" : $"Monitor {n++}";
                monitor.Items.Add(new ComboBoxItem
                {
                    Content = $"{nome} · {b.Width}×{b.Height}",
                    Tag = new Ancoraggio.Riquadro(b.X, b.Y, b.Width, b.Height),
                });
            }
        }
        catch (Exception e) { Registro.Scrivi($"registrazione: monitor ({e.Message})"); }
        if (monitor.Items.Count == 0)
            monitor.Items.Add(new ComboBoxItem { Content = "Schermo", Tag = new Ancoraggio.Riquadro(0, 0, 1920, 1080) });
        monitor.SelectedIndex = 0;
    }

    Ancoraggio.Riquadro MonitorScelto() =>
        monitor.SelectedItem is ComboBoxItem { Tag: Ancoraggio.Riquadro r } ? r : new Ancoraggio.Riquadro(0, 0, 1920, 1080);

    void RiempiFinestre()
    {
        var scelto = Valore(finestre);
        finestre.Items.Clear();
        finestre.Items.Add(new ComboBoxItem { Content = "Scegli una finestra", Tag = "" });
        foreach (var t in ComandiRegistrazione.TitoliUtili(TitoliAperti()))
            finestre.Items.Add(new ComboBoxItem { Content = t, Tag = t });
        var trovato = finestre.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string?)i.Tag == scelto);
        finestre.SelectedItem = trovato ?? finestre.Items[0];
        finestre.Visibility = area == "finestra" ? Visibility.Visible : Visibility.Collapsed;
    }

    static IEnumerable<string> TitoliAperti()
    {
        var lista = new List<string>();
        try
        {
            EnumWindows((h, _) =>
            {
                if (!IsWindowVisible(h)) return true;
                var n = GetWindowTextLength(h);
                if (n <= 0) return true;
                var sb = new StringBuilder(n + 1);
                GetWindowText(h, sb, sb.Capacity);
                lista.Add(sb.ToString());
                return true;
            }, IntPtr.Zero);
        }
        catch (Exception e) { Registro.Scrivi($"registrazione: finestre ({e.Message})"); }
        return lista;
    }

    void ApplicaSalvato()
    {
        var imp = Impostazioni.Comune;
        area = imp.RegistrazioneArea is "zona" or "finestra" ? imp.RegistrazioneArea : "intero";
        fps = ComandiRegistrazione.FpsDi(imp.RegistrazioneFps ?? 30);
        qualita = imp.RegistrazioneQualita is "alta" or "leggera" ? imp.RegistrazioneQualita : "buona";
        cursore.IsChecked = imp.RegistrazioneCursore;
        conto.IsChecked = imp.RegistrazioneConto;
        solo.IsChecked = false;
        if (Ancoraggio.ProvaChiave(imp.RegistrazioneMonitor, out var voluto))
        {
            var i = monitor.Items.OfType<ComboBoxItem>().FirstOrDefault(x => x.Tag is Ancoraggio.Riquadro r && r.X == voluto.X && r.Y == voluto.Y);
            if (i is not null) monitor.SelectedItem = i;
        }
        if (Ancoraggio.ProvaChiave(imp.RegistrazioneZona, out var z) && Ancoraggio.Tocca(z, MonitorScelto())) zona = z;
        else if (area == "zona") area = "intero";
        if (!string.IsNullOrWhiteSpace(imp.RegistrazioneFinestra))
        {
            var i = finestre.Items.OfType<ComboBoxItem>().FirstOrDefault(x => (string?)x.Tag == imp.RegistrazioneFinestra);
            if (i is not null) finestre.SelectedItem = i;
        }
        finestre.Visibility = area == "finestra" ? Visibility.Visible : Visibility.Collapsed;
        SegnaAggancio();
    }

    void Ricorda()
    {
        if (blocco || !IsLoaded) return;
        var imp = Impostazioni.Comune;
        imp.RegistrazioneMic = Valore(microfono) ?? "";
        imp.RegistrazionePc = Valore(uscita) ?? "";
        imp.RegistrazioneArea = area;
        imp.RegistrazioneFps = fps;
        imp.RegistrazioneQualita = qualita;
        imp.RegistrazioneCursore = cursore.IsChecked == true;
        imp.RegistrazioneConto = conto.IsChecked == true;
        imp.RegistrazioneMonitor = Ancoraggio.Chiave(MonitorScelto());
        imp.RegistrazioneZona = zona is { } z ? Ancoraggio.Chiave(z) : null;
        imp.RegistrazioneFinestra = Valore(finestre);
        imp.Salva();
    }

    void SegnaTutto()
    {
        Segna(areaIntero, area == "intero");
        Segna(areaZona, area == "zona");
        Segna(areaFinestra, area == "finestra");
        Segna(fps30, fps == 30);
        Segna(fps60, fps == 60);
        Segna(qLeggera, qualita == "leggera");
        Segna(qBuona, qualita == "buona");
        Segna(qAlta, qualita == "alta");
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (Registrazione.Attiva)
        {
            e.Cancel = true;
            Riduci();
            return;
        }
        base.OnClosing(e);
    }

    static Ancoraggio.Riquadro? RettangoloDi(string titolo)
    {
        Ancoraggio.Riquadro? trovato = null;
        try
        {
            EnumWindows((h, _) =>
            {
                if (!IsWindowVisible(h)) return true;
                var n = GetWindowTextLength(h);
                if (n <= 0) return true;
                var sb = new StringBuilder(n + 1);
                GetWindowText(h, sb, sb.Capacity);
                if (!string.Equals(sb.ToString().Trim(), titolo, StringComparison.Ordinal)) return true;
                if (GetWindowRect(h, out var r))
                    trovato = new Ancoraggio.Riquadro(r.Left, r.Top, Math.Max(1, r.Right - r.Left), Math.Max(1, r.Bottom - r.Top));
                return false;
            }, IntPtr.Zero);
        }
        catch { /* senza il rettangolo si vede il monitor intero */ }
        return trovato;
    }

    static void RiempiAudio(ComboBox combo, IReadOnlyList<string> nomi, string? salvato)
    {
        combo.Items.Clear();
        combo.Items.Add(new ComboBoxItem { Content = "Nessuno", Tag = "" });
        foreach (var n in nomi) combo.Items.Add(new ComboBoxItem { Content = n, Tag = n });
        ComboBoxItem? scelto = null;
        if (salvato == "") scelto = (ComboBoxItem)combo.Items[0]!;
        else if (!string.IsNullOrEmpty(salvato))
            scelto = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string?)i.Tag == salvato);
        combo.SelectedItem = scelto ?? (nomi.Count > 0 ? combo.Items[1] : combo.Items[0]);
    }

    static string? Valore(ComboBox combo)
    {
        var t = (combo.SelectedItem as ComboBoxItem)?.Tag as string;
        return string.IsNullOrEmpty(t) ? null : t;
    }

    static bool Interattivo(object? o)
    {
        var d = o as DependencyObject;
        while (d is not null)
        {
            if (d is Button or ComboBox or CheckBox or TextBox or ScrollBar or Slider or RepeatButton) return true;
            d = VisualTreeHelper.GetParent(d);
        }
        return false;
    }

    static ComboBox Combo() => new()
    {
        Margin = new Thickness(0, 2, 0, 6), FontSize = 13,
        Background = Brushes.White, Foreground = Brushes.Black, MaxDropDownHeight = 220,
    };

    static TextBlock Etichetta(string testo) => new()
    {
        Text = testo, Foreground = Oro, FontSize = 10.5, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 8, 0, 2),
    };

    static TextBlock Voce(string testo, Action fai)
    {
        var t = new TextBlock { Text = testo, Foreground = Muto, FontSize = 12, Cursor = Cursors.Hand, Margin = new Thickness(10, 0, 0, 0) };
        t.MouseLeftButtonUp += (_, _) => fai();
        return t;
    }

    static StackPanel EtichettaLivello(string nome, Border barra)
    {
        var pista = new Border
        {
            Width = 72, Height = 8, CornerRadius = new CornerRadius(4), Background = Spento,
            VerticalAlignment = VerticalAlignment.Center, Child = barra, ClipToBounds = true,
        };
        var riga = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        riga.Children.Add(new TextBlock { Text = nome, Foreground = Muto, FontSize = 11, Width = 28, VerticalAlignment = VerticalAlignment.Center });
        riga.Children.Add(pista);
        return riga;
    }

    static Border BarraLivello() => new()
    {
        Width = 0, Height = 8, HorizontalAlignment = HorizontalAlignment.Left, Background = Oro, CornerRadius = new CornerRadius(4),
    };

    static WrapPanel Riga(params UIElement[] figli)
    {
        var r = new WrapPanel { Margin = new Thickness(0, 2, 0, 4) };
        foreach (var f in figli) r.Children.Add(f);
        return r;
    }

    static Button Mini(string testo, Action fai)
    {
        var b = new Button
        {
            Content = testo, Margin = new Thickness(0, 0, 6, 4), Padding = new Thickness(10, 5, 10, 5),
            FontSize = 12.5, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Background = Spento,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
        };
        b.Click += (_, _) => fai();
        return b;
    }

    static void Segna(Button b, bool su)
    {
        b.Background = su ? Oro : Spento;
        b.Foreground = su ? Colore(0x1A, 0x14, 0x08) : Brushes.White;
    }

    static Button Tasto(string testo, Brush sfondo, Action fai)
    {
        var b = new Button
        {
            Content = testo, Padding = new Thickness(14, 7, 14, 7), Background = sfondo, Foreground = Brushes.White,
            BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold, FontSize = 13, Cursor = Cursors.Hand,
        };
        b.Click += (_, _) => fai();
        return b;
    }

    static CheckBox Spunta(string testo) => new()
    {
        Content = testo, Foreground = Testo, FontSize = 13, Margin = new Thickness(0, 4, 0, 0),
    };

    static SolidColorBrush Colore(byte r, byte g, byte b)
    {
        var p = new SolidColorBrush(Color.FromRgb(r, g, b));
        p.Freeze();
        return p;
    }

    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RETT r);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
    [StructLayout(LayoutKind.Sequential)] struct RETT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
}
