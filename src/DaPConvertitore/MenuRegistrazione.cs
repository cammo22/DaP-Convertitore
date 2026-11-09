using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DaP.Convertitore.App;

/// <summary>
/// Dal vassoio o dall'avatar: schermo e audio, oppure solo audio, e quali dispositivi. Si chiude prima di partire,
/// e comunque non entra nella cattura.
/// </summary>
public sealed class MenuRegistrazione : Window
{
    static MenuRegistrazione? aperta;
    readonly ComboBox microfono = Combo();
    readonly ComboBox uscita = Combo();
    readonly TextBlock nota = new() { Text = "Cerco microfoni e l'audio del PC…", Foreground = Pennello("#a19db0"), FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    readonly Button schermo, solo;
    bool blocco = true;

    public static void Mostra(Window? accanto, string da)
    {
        if (Registrazione.Attiva) { BarraRegistrazione.PortaDavanti(); return; }
        if (aperta is not null) { aperta.Activate(); aperta.Topmost = true; Registro.Scrivi("registrazione: il menu era già aperto, lo porto davanti"); return; }
        try
        {
            aperta = new MenuRegistrazione(accanto, da);
            Registrazione.Escludi(aperta);
            aperta.Closed += (_, _) => { Registro.Scrivi("registrazione: menu chiuso"); aperta = null; };
            aperta.Show();
            aperta.Activate();
            aperta.Topmost = true;
            if (!aperta.IsVisible) Registro.Scrivi("registrazione: Show() è tornato ma il menu non è visibile");
        }
        catch (Exception e)
        {
            aperta = null;
            Registro.Errore("registrazione: menu", e);
            Vassoio.Dire("Registra non si è aperto", e.Message);
        }
    }

    MenuRegistrazione(Window? accanto, string da)
    {
        // niente AllowsTransparency: con WDA_EXCLUDEFROMCAPTURE una finestra layered su Windows 11
        // diventa invisibile anche per chi ci clicca. Il menu del vassoio «non apriva niente».
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        Background = new SolidColorBrush(Color.FromRgb(0x12, 0x10, 0x1a));
        Title = "Registra";
        MinWidth = 320;
        var area0 = SystemParameters.WorkArea;
        Left = area0.Right - 380;
        Top = area0.Bottom - 420;

        var pila = new StackPanel { Margin = new Thickness(16, 14, 16, 16), MinWidth = 300, MaxWidth = 380 };
        var testa = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var titolo = new TextBlock { Text = "REGISTRA", Foreground = Pennello("#ffd54a"), FontWeight = FontWeights.Bold, FontSize = 13, FontFamily = new FontFamily("Segoe UI") };
        var chiudi = new TextBlock { Text = "Chiudi", Foreground = Pennello("#a19db0"), FontSize = 12, Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Right };
        chiudi.MouseLeftButtonUp += (_, _) => Close();
        DockPanel.SetDock(chiudi, Dock.Right);
        testa.Children.Add(chiudi);
        testa.Children.Add(titolo);
        pila.Children.Add(testa);
        pila.Children.Add(Etichetta("MICROFONO"));
        pila.Children.Add(microfono);
        pila.Children.Add(Etichetta("AUDIO DEL PC"));
        pila.Children.Add(uscita);
        schermo = Tasto("Schermo e audio", "#ff3df2", () => Parti("schermo"));
        solo = Tasto("Solo audio", "#35e8ff", () => Parti("audio"));
        solo.Foreground = Pennello("#102028");
        schermo.IsEnabled = solo.IsEnabled = false;
        schermo.Margin = new Thickness(0, 12, 0, 0);
        pila.Children.Add(schermo);
        pila.Children.Add(solo);
        pila.Children.Add(nota);
        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x12, 0x10, 0x1a)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xff, 0xd5, 0x4a)),
            BorderThickness = new Thickness(1),
            Child = pila,
        };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };

        microfono.SelectionChanged += (_, _) => Ricorda();
        uscita.SelectionChanged += (_, _) => Ricorda();
        ContentRendered += (_, _) =>
        {
            Sistema(accanto);
            Registro.Scrivi($"registrazione: menu aperto da {da} a {Left:0},{Top:0} ({ActualWidth:0}×{ActualHeight:0})");
        };
        Loaded += async (_, _) =>
        {
            Sistema(accanto);
            DispositiviAudio d;
            try { d = await Task.Run(Registrazione.Elenca); }
            catch (Exception e)
            {
                Registro.Errore("registrazione: dispositivi", e);
                nota.Text = e.Message;
                schermo.IsEnabled = true;
                return;
            }
            if (!IsLoaded) return;
            var imp = Impostazioni.Comune;
            blocco = true;
            Riempi(microfono, d.Microfoni, imp.RegistrazioneMic);
            Riempi(uscita, d.Uscite, imp.RegistrazionePc);
            blocco = false;
            schermo.IsEnabled = solo.IsEnabled = true;
            nota.Text = d.Microfoni.Count == 0 && d.Uscite.Count == 0
                ? "Non vedo dispositivi. Il microfono arriva da DirectShow, l'audio del PC da WASAPI: servono su Windows, con l'FFmpeg dell'app."
                : "La barra e questo menu non entrano nel video. Lo schermo è quello principale, intero.";
        };
    }

    void Parti(string come)
    {
        var mic = Valore(microfono);
        var pc = Valore(uscita);
        var perche = ComandiRegistrazione.MotivoRifiuto(come, mic, pc);
        if (perche is not null) { nota.Text = perche; Registro.Scrivi("registrazione: " + perche); return; }
        Registro.Scrivi($"registrazione: scelto {come}, mic «{mic ?? "no"}», pc «{pc ?? "no"}»");
        Close();
        Registrazione.Inizia(come, mic, pc);
    }

    void Ricorda()
    {
        if (blocco || !IsLoaded) return;
        var imp = Impostazioni.Comune;
        imp.RegistrazioneMic = Valore(microfono) ?? "";
        imp.RegistrazionePc = Valore(uscita) ?? "";
        imp.Salva();
    }

    static void Riempi(ComboBox combo, IReadOnlyList<string> nomi, string? salvato)
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

    void Sistema(Window? accanto)
    {
        UpdateLayout();
        var w = ActualWidth > 8 ? ActualWidth : 340;
        var h = ActualHeight > 8 ? ActualHeight : 280;
        if (ActualWidth < 8 || ActualHeight < 8)
        {
            Width = w;
            Height = h;
            UpdateLayout();
            if (ActualWidth > 8) w = ActualWidth;
            if (ActualHeight > 8) h = ActualHeight;
            Registro.Scrivi("registrazione: il menu non si era misurato, gli do una misura fissa");
        }
        var area = SystemParameters.WorkArea;
        double left, top;
        if (accanto is not null)
        {
            left = accanto.Left + accanto.Width + 12;
            top = accanto.Top;
            if (left + w > area.Right) left = accanto.Left - w - 12;
        }
        else
        {
            left = area.Right - w - 18;
            top = area.Bottom - h - 18;
        }
        Left = Math.Clamp(left, area.Left, Math.Max(area.Left, area.Right - w));
        Top = Math.Clamp(top, area.Top, Math.Max(area.Top, area.Bottom - h));
    }

    static ComboBox Combo() => new()
    {
        MinWidth = 280,
        Margin = new Thickness(0, 2, 0, 8),
        Background = Brushes.White,
        Foreground = Brushes.Black,
    };

    static TextBlock Etichetta(string testo) => new()
    {
        Text = testo,
        Foreground = Pennello("#ffd54a"),
        FontSize = 10.5,
        FontWeight = FontWeights.Bold,
        Margin = new Thickness(0, 4, 0, 0),
    };

    static Button Tasto(string testo, string sfondo, Action fai)
    {
        var b = new Button
        {
            Content = testo,
            Margin = new Thickness(0, 6, 0, 0),
            Padding = new Thickness(12, 8, 12, 8),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(sfondo)),
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Cursor = Cursors.Hand,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        b.Click += (_, _) => fai();
        return b;
    }

    static SolidColorBrush Pennello(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
}
