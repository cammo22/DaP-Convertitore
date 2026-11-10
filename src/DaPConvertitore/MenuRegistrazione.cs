using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DaP.Convertitore.App;

/// <summary>
/// La finestra di registrazione: l'anteprima dello schermo, i tasti e le impostazioni.
/// Sta accanto all'avatar (o al centro del monitor principale) e lo segue se si sposta.
/// È esclusa dalla cattura, quindi non entra nel video e non fa da specchio.
/// </summary>
public sealed class MenuRegistrazione : Window
{
    const double MisuraL = 400;
    const double MisuraA = 680;

    static MenuRegistrazione? aperta;
    readonly Image quadro = new() { Stretch = Stretch.Uniform };
    readonly TextBlock soloAudioTesto;
    readonly TextBlock contoTesto;
    readonly TextBlock tempo;
    readonly TextBlock nota;
    readonly TextBlock chiudiTesto;
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
        if (Registrazione.Attiva) { PortaDavanti(); return; }
        if (aperta is not null)
        {
            aperta.Aggancia(accanto);
            aperta.Show();
            aperta.Activate();
            Registro.Scrivi("registrazione: la finestra era già aperta, la porto davanti");
            return;
        }
        try
        {
            aperta = new MenuRegistrazione(accanto, da);
            Registrazione.Escludi(aperta);
            aperta.Closed += (_, _) => { Registro.Scrivi("registrazione: finestra chiusa"); aperta = null; };
            aperta.Show();
            aperta.Activate();
            aperta.Topmost = true;
            if (!aperta.IsVisible) Registro.Scrivi("registrazione: Show() è tornato ma la finestra non è visibile");
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
        aperta.Posiziona(true);
        aperta.Topmost = true;
        try { aperta.Activate(); } catch { /* già davanti */ }
    }

    public static void Aggiorna() => aperta?.Rinfresca();

    MenuRegistrazione(Window? accanto, string da)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = MisuraL;
        Height = MisuraA;
        Background = Sfondo();
        Title = "Registra";

        soloAudioTesto = new TextBlock
        {
            Text = "Solo audio",
            Foreground = Pennello("#ffd54a"),
            FontSize = 18,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        contoTesto = new TextBlock
        {
            Foreground = Pennello("#ffd54a"),
            FontSize = 64,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        var palco = new Grid { Height = 200, Background = Brushes.Black, Margin = new Thickness(0, 6, 0, 6) };
        palco.Children.Add(quadro);
        palco.Children.Add(soloAudioTesto);
        palco.Children.Add(contoTesto);
        anteprima = new AnteprimaSchermo(quadro);

        tempo = new TextBlock { Text = "00:00", Foreground = Pennello("#ffd54a"), FontFamily = new FontFamily("Consolas"), FontSize = 18, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, MinWidth = 58 };
        var rigaTempo = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        rigaTempo.Children.Add(tempo);
        rigaTempo.Children.Add(EtichettaLivello("Mic", barraMic));
        rigaTempo.Children.Add(EtichettaLivello("PC", barraPc));

        avvia = Tasto("Avvia", "#ff3df2", () => _ = Parti());
        pausa = Tasto("Pausa", "#3a3548", () => { if (Registrazione.InPausa) Registrazione.Riprendi(); else Registrazione.Pausa(); Rinfresca(); });
        salva = Tasto("Salva", "#1c9a62", Registrazione.Salva);
        annulla = Tasto("Annulla", "#8a2a3c", Registrazione.Annulla);
        pausa.Visibility = Visibility.Collapsed;
        var rigaTasti = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        foreach (var b in new[] { avvia, pausa, salva, annulla }) { b.Margin = new Thickness(0, 0, 6, 6); rigaTasti.Children.Add(b); }

        areaIntero = Mini("Intero", () => ScegliArea("intero"));
        areaZona = Mini("Zona", () => ScegliArea("zona"));
        areaFinestra = Mini("Finestra", () => ScegliArea("finestra"));
        var scegliZona = Mini("Scegli la zona", () => _ = PrendiZona());
        fps30 = Mini("30", () => ScegliFps(30));
        fps60 = Mini("60", () => ScegliFps(60));
        qLeggera = Mini("Leggera", () => ScegliQualita("leggera"));
        qBuona = Mini("Buona", () => ScegliQualita("buona"));
        qAlta = Mini("Alta", () => ScegliQualita("alta"));
        cursore = Spunta("Mostra il cursore");
        conto = Spunta("Conto alla rovescia");
        solo = Spunta("Solo audio");

        var pila = new StackPanel { Margin = new Thickness(14, 12, 14, 14) };
        var testa = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var titolo = new TextBlock { Text = "REGISTRA", Foreground = Pennello("#ffd54a"), FontWeight = FontWeights.Bold, FontSize = 13, FontFamily = new FontFamily("Segoe UI") };
        chiudiTesto = new TextBlock { Text = "Chiudi", Foreground = Pennello("#a19db0"), FontSize = 12, Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Right };
        chiudiTesto.MouseLeftButtonUp += (_, _) => ChiudiORiduci();
        DockPanel.SetDock(chiudiTesto, Dock.Right);
        testa.Children.Add(chiudiTesto);
        testa.Children.Add(titolo);
        pila.Children.Add(testa);
        pila.Children.Add(palco);
        pila.Children.Add(rigaTempo);
        pila.Children.Add(rigaTasti);

        impostazioni.Children.Add(Etichetta("MONITOR"));
        impostazioni.Children.Add(monitor);
        impostazioni.Children.Add(Etichetta("AREA"));
        impostazioni.Children.Add(Riga(areaIntero, areaZona, areaFinestra, scegliZona));
        impostazioni.Children.Add(finestre);
        impostazioni.Children.Add(Etichetta("FOTOGRAMMI E QUALITÀ"));
        impostazioni.Children.Add(Riga(fps30, fps60, qLeggera, qBuona, qAlta));
        impostazioni.Children.Add(Etichetta("MICROFONO"));
        impostazioni.Children.Add(microfono);
        impostazioni.Children.Add(Etichetta("AUDIO DEL PC"));
        impostazioni.Children.Add(uscita);
        impostazioni.Children.Add(cursore);
        impostazioni.Children.Add(conto);
        impostazioni.Children.Add(solo);
        pila.Children.Add(impostazioni);
        nota = new TextBlock
        {
            Text = "Cerco microfoni e l'audio del PC…",
            Foreground = Pennello("#a19db0"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
        };
        pila.Children.Add(nota);
        pila.Children.Add(new TextBlock
        {
            Text = "Questa finestra non entra nel video. Nell'anteprima, dove sta lei, si vede un buco: non è lo specchio.",
            Foreground = Pennello("#6e6a7a"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
        });
        Content = new Border
        {
            Background = Sfondo(),
            BorderBrush = Pennello("#ffd54a"),
            BorderThickness = new Thickness(1),
            Child = new ScrollViewer { Content = pila, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
        };

        monitor.SelectionChanged += (_, _) => { if (!blocco) { Ricorda(); AggiornaAnteprima(); } };
        microfono.SelectionChanged += (_, _) => Ricorda();
        uscita.SelectionChanged += (_, _) => Ricorda();
        finestre.SelectionChanged += (_, _) => Ricorda();
        cursore.Checked += (_, _) => Ricorda();
        cursore.Unchecked += (_, _) => Ricorda();
        conto.Checked += (_, _) => Ricorda();
        conto.Unchecked += (_, _) => Ricorda();
        solo.Checked += (_, _) => { if (!blocco) { Ricorda(); AggiornaAnteprima(); } };
        solo.Unchecked += (_, _) => { if (!blocco) { Ricorda(); AggiornaAnteprima(); } };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) ChiudiORiduci(); };

        orologio = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        orologio.Tick += (_, _) => { if (Registrazione.Attiva || Registrazione.InPausa) tempo.Text = Registrazione.TestoTempo(); Livelli(); };
        SourceInitialized += (_, _) => Posiziona(false);
        ContentRendered += (_, _) =>
        {
            Posiziona(true);
            Registro.Scrivi($"registrazione: finestra aperta da {da}");
        };
        Loaded += async (_, _) =>
        {
            orologio.Start();
            RiempiMonitori();
            RiempiFinestre();
            ApplicaSalvato();
            blocco = false;
            SegnaTutto();
            AggiornaAnteprima();
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
                ? "Non vedo dispositivi. Il microfono arriva da DirectShow, l'audio del PC da WASAPI."
                : "Scegli e premi Avvia. La scelta resta per la prossima volta.";
        };
        Closed += (_, _) =>
        {
            orologio.Stop();
            anteprima.Dispose();
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
        else if (modo == "intero") { var m = MonitorScelto(); schermo = new(m.X, m.Y, m.L, m.A); }
        var modoFf = modo == "audio" ? "audio" : modo == "finestra" ? "finestra" : "schermo";
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
        finally { Show(); Posiziona(false); try { Activate(); } catch { /* già chiusa */ } }
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
        chiudiTesto.Text = on ? "Riduci" : "Chiudi";
    }

    void Livelli()
    {
        barraMic.Width = 72 * LivelloAudio.Microfono(Valore(microfono));
        barraPc.Width = 72 * LivelloAudio.Uscita(Valore(uscita));
    }

    void ChiudiORiduci()
    {
        if (Registrazione.Attiva) { Hide(); BarraRegistrazione.Mostra(); return; }
        Close();
    }

    void Aggancia(Window? a)
    {
        if (ancora is not null) ancora.LocationChanged -= Segui;
        ancora = a;
        if (ancora is not null) ancora.LocationChanged += Segui;
        Posiziona(false);
    }

    void Segui(object? sender, EventArgs e) => Posiziona(false);

    void Posiziona(bool scrivi)
    {
        if (sistemo) return;
        sistemo = true;
        try { PosizioneFinestre.Sistema(this, ancora, MisuraL, MisuraA, scrivi); }
        finally { sistemo = false; }
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
                var sb = new System.Text.StringBuilder(n + 1);
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
            Hide();
            BarraRegistrazione.Mostra();
            return;
        }
        base.OnClosing(e);
    }

    /// <summary>Il rettangolo della finestra con quel titolo, in pixel veri. Serve all'anteprima.</summary>
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
                var sb = new System.Text.StringBuilder(n + 1);
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

    static ComboBox Combo() => new()
    {
        Margin = new Thickness(0, 2, 0, 6),
        FontSize = 13,
        Background = Brushes.White,
        Foreground = Brushes.Black,
        MaxDropDownHeight = 220,
    };

    static TextBlock Etichetta(string testo) => new()
    {
        Text = testo,
        Foreground = Pennello("#ffd54a"),
        FontSize = 10.5,
        FontWeight = FontWeights.Bold,
        Margin = new Thickness(0, 6, 0, 0),
    };

    static StackPanel EtichettaLivello(string nome, Border barra)
    {
        var pista = new Border
        {
            Width = 72, Height = 6, Margin = new Thickness(8, 0, 0, 0),
            Background = Pennello("#2a2636"), CornerRadius = new CornerRadius(3),
            VerticalAlignment = VerticalAlignment.Center, Child = barra, ClipToBounds = true,
        };
        var riga = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        riga.Children.Add(new TextBlock { Text = nome, Foreground = Pennello("#a19db0"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
        riga.Children.Add(pista);
        return riga;
    }

    static Border BarraLivello() => new()
    {
        Width = 0, Height = 6, HorizontalAlignment = HorizontalAlignment.Left,
        Background = Pennello("#ffd54a"), CornerRadius = new CornerRadius(3),
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
            Content = testo,
            Margin = new Thickness(0, 0, 6, 4),
            Padding = new Thickness(8, 4, 8, 4),
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            Background = Pennello("#2a2636"),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
        };
        b.Click += (_, _) => fai();
        return b;
    }

    static void Segna(Button b, bool su)
    {
        b.Background = su ? Pennello("#ffd54a") : Pennello("#2a2636");
        b.Foreground = su ? Pennello("#1a1408") : Brushes.White;
    }

    static Button Tasto(string testo, string sfondo, Action fai)
    {
        var b = new Button
        {
            Content = testo,
            Padding = new Thickness(12, 6, 12, 6),
            Background = Pennello(sfondo),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.Bold,
            FontSize = 13,
            Cursor = Cursors.Hand,
        };
        b.Click += (_, _) => fai();
        return b;
    }

    static CheckBox Spunta(string testo) => new()
    {
        Content = testo,
        Foreground = Pennello("#f4f0e6"),
        FontSize = 13,
        Margin = new Thickness(0, 3, 0, 0),
    };

    static SolidColorBrush Sfondo() => new(Color.FromRgb(0x12, 0x10, 0x1a));
    static SolidColorBrush Pennello(string hex) => new((Color)ColorConverter.ConvertFromString(hex));

    delegate bool EnumProc(IntPtr h, IntPtr l);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RETT r);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool EnumWindows(EnumProc f, IntPtr l);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct RETT { public int Left, Top, Right, Bottom; }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
}
