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

    public static void Mostra(Window? accanto)
    {
        if (Registrazione.Attiva) { BarraRegistrazione.PortaDavanti(); return; }
        if (aperta is not null) { aperta.Activate(); return; }
        aperta = new MenuRegistrazione(accanto);
        Registrazione.Escludi(aperta);
        aperta.Closed += (_, _) => aperta = null;
        aperta.Show();
    }

    MenuRegistrazione(Window? accanto)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        SizeToContent = SizeToContent.WidthAndHeight;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Title = "Registra";

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
            CornerRadius = new CornerRadius(16),
            Background = new SolidColorBrush(Color.FromArgb(230, 18, 16, 26)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 213, 74)),
            BorderThickness = new Thickness(1),
            Child = pila,
        };

        microfono.SelectionChanged += (_, _) => Ricorda();
        uscita.SelectionChanged += (_, _) => Ricorda();
        Loaded += async (_, _) =>
        {
            Sistema(accanto);
            DispositiviAudio d;
            try { d = await Task.Run(Registrazione.Elenca); }
            catch (Exception e) { nota.Text = e.Message; return; }
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
        if (accanto is not null)
        {
            Left = accanto.Left + accanto.Width + 12;
            Top = accanto.Top;
            if (Left + ActualWidth > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth)
                Left = Math.Max(SystemParameters.VirtualScreenLeft, accanto.Left - ActualWidth - 12);
            return;
        }
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 18;
        Top = area.Bottom - ActualHeight - 18;
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
