using System.Windows;
using System.Windows.Controls;
using DaP.Convertitore.Lettore;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DaP.Convertitore.App;

/// <summary>
/// Il video sull'avatar, come la musica: parte subito, senza aprire l'app. Se Windows non lo sa suonare, lo apre il lettore.
/// </summary>
public sealed class MiniVideo : Window
{
    readonly MediaElement film = new() { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Manual, Stretch = Stretch.Uniform };
    readonly IReadOnlyList<string> file;
    readonly FinestraAvatar avatar;
    readonly TextBlock titolo = new() { Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly Border riempimento = new()
    {
        Background = new LinearGradientBrush(Color.FromRgb(0xff, 0xd5, 0x4a), Color.FromRgb(0xff, 0x3d, 0xf2), 0),
        Height = 6, CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left, Width = 0, IsHitTestVisible = false,
    };
    readonly Grid barra = new() { Height = 18, Cursor = Cursors.Hand, Margin = new Thickness(0, 8, 0, 0) };
    readonly Button play;
    readonly DispatcherTimer orologio;
    int i;
    bool suona = true;

    public event Action<bool>? StatoCambiato;

    public MiniVideo(IReadOnlyList<string> file, FinestraAvatar avatar)
    {
        this.file = file;
        this.avatar = avatar;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Width = 420;
        SizeToContent = SizeToContent.Height;

        film.Width = 384;
        film.Height = 216;
        film.MediaEnded += (_, _) => { if (i + 1 < file.Count) Vai(i + 1); else Close(); };
        film.MediaFailed += (_, _) =>
        {
            Regia.Gestisci(new Richiesta(null, [file[i]], false, true));
            Close();
        };

        barra.Children.Add(new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(Color.FromArgb(46, 255, 255, 255)), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false });
        barra.Children.Add(riempimento);
        void Cerca(MouseEventArgs e)
        {
            if (!film.NaturalDuration.HasTimeSpan) return;
            var f = Math.Clamp(e.GetPosition(barra).X / Math.Max(1, barra.ActualWidth), 0, 1);
            film.Position = TimeSpan.FromSeconds(film.NaturalDuration.TimeSpan.TotalSeconds * f);
            Aggiorna();
        }
        barra.MouseLeftButtonDown += (_, e) => { barra.CaptureMouse(); Cerca(e); };
        barra.MouseMove += (_, e) => { if (barra.IsMouseCaptured) Cerca(e); };
        barra.MouseLeftButtonUp += (_, _) => barra.ReleaseMouseCapture();

        var riquadro = new Grid { Width = 384, Height = 216 };
        riquadro.Children.Add(new Border { Background = Brushes.Black, CornerRadius = new CornerRadius(12), Child = film, ClipToBounds = true });
        var sopra = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        sopra.Children.Add(Tasto("Modifica", () => _ = Modifica()));
        sopra.Children.Add(Tasto("Download", () => _ = Scarica()));
        var velo = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(170, 8, 6, 14)),
            VerticalAlignment = VerticalAlignment.Bottom, Height = 42, Child = sopra, Visibility = Visibility.Collapsed,
        };
        riquadro.Children.Add(velo);
        riquadro.MouseEnter += (_, _) => velo.Visibility = Visibility.Visible;
        riquadro.MouseLeave += (_, _) => velo.Visibility = Visibility.Collapsed;

        play = Tasto("Pausa", Gira, oro: true);
        var chiudi = Tasto("Chiudi", Close);
        var tasti = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
        tasti.Children.Add(Tasto("Apri nel lettore", () => { film.Pause(); Regia.Gestisci(new Richiesta(null, [file[i]], false, true)); Close(); }));
        if (file.Count > 1) tasti.Children.Add(Tasto("Prima", () => Vai(i - 1)));
        tasti.Children.Add(play);
        if (file.Count > 1) tasti.Children.Add(Tasto("Dopo", () => Vai(i + 1)));
        tasti.Children.Add(chiudi);

        var pila = new StackPanel { Margin = new Thickness(16, 14, 16, 12) };
        pila.Children.Add(titolo);
        pila.Children.Add(riquadro);
        pila.Children.Add(barra);
        pila.Children.Add(tasti);
        Content = new Border
        {
            CornerRadius = new CornerRadius(16), Margin = new Thickness(12), Child = pila,
            Background = new SolidColorBrush(Color.FromArgb(230, 18, 16, 26)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), BorderThickness = new Thickness(1),
        };

        orologio = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Normal, (_, _) => Aggiorna(), Dispatcher);
        Loaded += (_, _) => { Posiziona(); Vai(0); orologio.Start(); };
        avatar.Spostato += Posiziona;
        Closed += (_, _) => { avatar.Spostato -= Posiziona; orologio.Stop(); film.Stop(); film.Close(); StatoCambiato?.Invoke(false); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); else if (e.Key == Key.Space) Gira(); };
    }

    void Vai(int n)
    {
        i = (n + file.Count) % file.Count;
        titolo.Text = Path.GetFileName(file[i]);
        film.Source = new Uri(file[i]);
        film.Play();
        suona = true;
        play.Content = "Pausa";
        StatoCambiato?.Invoke(true);
    }

    void Gira()
    {
        if (suona) { film.Pause(); suona = false; play.Content = "Play"; }
        else { film.Play(); suona = true; play.Content = "Pausa"; }
        StatoCambiato?.Invoke(suona);
    }

    void Aggiorna()
    {
        if (!film.NaturalDuration.HasTimeSpan || barra.ActualWidth <= 0) return;
        var tot = film.NaturalDuration.TimeSpan.TotalSeconds;
        if (tot <= 0) return;
        riempimento.Width = barra.ActualWidth * Math.Clamp(film.Position.TotalSeconds / tot, 0, 1);
    }

    async Task Modifica()
    {
        film.Pause();
        var era = suona;
        suona = false;
        try
        {
            var esito = await FinestraCopertina.Mostra(this, file[i], film.Position.TotalSeconds);
            if (esito?.Nota is { Length: > 0 }) titolo.ToolTip = esito.Nota;
        }
        catch { /* la finestra lo dice */ }
        if (era) { film.Play(); suona = true; play.Content = "Pausa"; }
    }

    async Task Scarica()
    {
        try
        {
            var dove = await Copertine.SalvaAccanto(Strumenti.Trova(), file[i]);
            titolo.ToolTip = "Salvata: " + Path.GetFileName(dove);
        }
        catch (Exception e) { titolo.ToolTip = e.Message; }
    }

    void Posiziona()
    {
        var a = avatar.Area;
        var area = SystemParameters.WorkArea;
        Left = Math.Clamp(a.Left + a.Width / 2 - Width / 2, area.Left, Math.Max(area.Left, area.Right - Width));
        var sotto = a.Bottom - 10;
        Top = sotto + 280 <= area.Bottom ? sotto : Math.Max(area.Top, a.Top - 280);
    }

    static Button Tasto(string testo, Action fai, bool oro = false)
    {
        var b = new Button
        {
            Content = testo, Height = 30, Margin = new Thickness(4, 0, 4, 0), Padding = new Thickness(10, 0, 10, 0),
            Foreground = oro ? new SolidColorBrush(Color.FromRgb(0x1a, 0x14, 0x05)) : Brushes.White,
            Background = oro ? new SolidColorBrush(Color.FromRgb(0xff, 0xd5, 0x4a)) : new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand, FontWeight = FontWeights.Bold,
        };
        b.Click += (_, _) => fai();
        return b;
    }
}
