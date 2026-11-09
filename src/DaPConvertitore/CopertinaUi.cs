using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DaP.Convertitore.Lettore;
using Microsoft.Win32;

namespace DaP.Convertitore.App;

/// <summary>
/// La scelta della copertina, dal player sull'avatar: salvarla nella cartella, caricarne una, o prenderne una disegnata.
/// Sul lettore intero la stessa scelta sta nella pagina.
/// </summary>
public sealed class FinestraCopertina : Window
{
    readonly string audio;
    readonly double? fotogramma;
    readonly TaskCompletionSource<EsitoCopertina?> fatto = new();
    readonly WrapPanel varianti = new() { Margin = new Thickness(0, 8, 0, 0) };
    bool chiuso;

    FinestraCopertina(string audio, double? fotogramma)
    {
        this.audio = audio;
        this.fotogramma = fotogramma;
        Title = "Copertina";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        Width = 440;

        var titolo = new TextBlock { Text = "Copertina", Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeights.Bold };
        var video = Copertine.VideoNelFile(audio);
        var nota = new TextBlock
        {
            Text = video
                ? "MP4, MOV e MKV tengono la copertina dentro (è la miniatura). Gli altri: la salvo accanto. Se qualcosa va storto, il file resta com'è."
                : "MP3, M4A e FLAC la tengono dentro il file. Gli altri: la salvo accanto, e l'originale resta com'è se qualcosa va storto.",
            Foreground = Col("#a19db0"), FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 12),
        };
        var salva = Tasto("Salva nella cartella", true, () => _ = Salva());
        var carica = Tasto("Carica un'immagine", false, () => _ = Carica());
        var fila = new StackPanel { Orientation = Orientation.Horizontal };
        fila.Children.Add(salva);
        fila.Children.Add(carica);
        if (video && fotogramma is not null)
            fila.Children.Add(Tasto("Questo fotogramma", false, () => _ = DalFotogramma(fotogramma.Value)));
        var disegnate = new TextBlock { Text = "OPPURE UNA DISEGNATA", Foreground = Col("#ffd54a"), FontSize = 11, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 14, 0, 0) };
        var chiudi = Tasto("Chiudi", false, () => Close());
        chiudi.HorizontalAlignment = HorizontalAlignment.Right;
        chiudi.Margin = new Thickness(0, 12, 0, 0);

        var pila = new StackPanel { Margin = new Thickness(18, 16, 18, 16) };
        pila.Children.Add(titolo);
        pila.Children.Add(nota);
        pila.Children.Add(fila);
        pila.Children.Add(disegnate);
        pila.Children.Add(varianti);
        pila.Children.Add(chiudi);
        Content = new Border
        {
            CornerRadius = new CornerRadius(16), Margin = new Thickness(12), Child = pila, Width = 416,
            Background = new SolidColorBrush(Color.FromArgb(242, 18, 16, 26)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), BorderThickness = new Thickness(1),
        };
        Closed += (_, _) => { chiuso = true; fatto.TrySetResult(null); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        Loaded += (_, _) => _ = Disegna();
    }

    public static Task<EsitoCopertina?> Mostra(Window sopra, string audio, double? fotogramma = null)
    {
        var w = new FinestraCopertina(audio, fotogramma) { Owner = sopra };
        w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        w.Show();
        return w.fatto.Task;
    }

    async Task Disegna()
    {
        var nome = Path.GetFileNameWithoutExtension(audio);
        for (var n = 0; n < Copertine.Quante; n++)
        {
            var variante = n;
            byte[] png;
            try { png = await Task.Run(() => Copertine.Genera(nome, variante, null)); }
            catch { return; }
            if (chiuso) return;
            var img = Immagine(png);
            var b = new Button { Width = 112, Height = 112, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(0), Cursor = Cursors.Hand, ToolTip = "Usa questa" };
            var bordo = new Border { CornerRadius = new CornerRadius(10), Child = new Image { Source = img, Stretch = Stretch.UniformToFill } };
            b.Content = bordo;
            b.Background = Brushes.Transparent;
            b.BorderThickness = new Thickness(0);
            b.Click += async (_, _) =>
            {
                try { await Finisci(await Copertine.ApplicaGenerata(Strumenti.Trova(), audio, nome, variante)); }
                catch (Exception e) { notaErrore(e.Message); }
            };
            varianti.Children.Add(b);
        }
    }

    async Task Salva()
    {
        try
        {
            var dove = await Copertine.SalvaAccanto(Strumenti.Trova(), audio);
            await Finisci(new EsitoCopertina(null, dove, false, "Salvata: " + Path.GetFileName(dove)));
        }
        catch (Exception e) { notaErrore(e.Message); }
    }

    async Task Carica()
    {
        var d = new OpenFileDialog { Title = "Scegli la copertina", Filter = "Immagini|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.tif;*.tiff" };
        if (d.ShowDialog(this) != true) return;
        try { await Finisci(await Copertine.Applica(Strumenti.Trova(), audio, d.FileName)); }
        catch (Exception e) { notaErrore(e.Message); }
    }

    async Task DalFotogramma(double secondo)
    {
        var tmp = Path.Combine(Strumenti.CartellaTemporanea, $"frame-{Guid.NewGuid():N}.png");
        try
        {
            var n = secondo.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            var r = await Processi.Esegui(Strumenti.Trova().Ffmpeg, ["-hide_banner", "-loglevel", "error", "-y", "-ss", n, "-i", audio, "-frames:v", "1", tmp], CancellationToken.None, tieniUscita: false);
            if (r.Codice != 0 || !File.Exists(tmp)) throw new ErroreConversione("Non ho preso il fotogramma.", r.Errori);
            await Finisci(await Copertine.Applica(Strumenti.Trova(), audio, tmp));
        }
        catch (Exception e) { notaErrore(e.Message); }
        finally { try { File.Delete(tmp); } catch { } }
    }

    Task Finisci(EsitoCopertina esito)
    {
        chiuso = true;
        fatto.TrySetResult(esito);
        Close();
        return Task.CompletedTask;
    }

    void notaErrore(string testo)
    {
        if (Content is Border { Child: StackPanel p } && p.Children.Count > 1 && p.Children[1] is TextBlock t)
        {
            t.Text = testo;
            t.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xb3, 0xc1));
        }
    }

    static BitmapImage Immagine(byte[] b)
    {
        var img = new BitmapImage();
        using var ms = new MemoryStream(b);
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = ms;
        img.EndInit();
        img.Freeze();
        return img;
    }

    static SolidColorBrush Col(string c) => (SolidColorBrush)new BrushConverter().ConvertFromString(c)!;

    static Button Tasto(string testo, bool oro, Action fai)
    {
        var b = new Button
        {
            Content = new TextBlock
            {
                Text = testo, FontWeight = FontWeights.Bold, FontSize = 13,
                Foreground = oro ? new SolidColorBrush(Color.FromRgb(0x1a, 0x14, 0x05)) : Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            },
            Height = 32, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 0, 12, 0),
            Cursor = Cursors.Hand, Focusable = false, BorderThickness = new Thickness(0),
            Background = oro ? new SolidColorBrush(Color.FromRgb(0xff, 0xd5, 0x4a)) : new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)),
        };
        var t = new ControlTemplate(typeof(Button));
        var bordo = new FrameworkElementFactory(typeof(Border));
        bordo.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
        bordo.SetValue(Border.PaddingProperty, new Thickness(12, 0, 12, 0));
        bordo.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        bordo.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
        t.VisualTree = bordo;
        b.Template = t;
        b.Click += (_, _) => fai();
        return b;
    }
}
