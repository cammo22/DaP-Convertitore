using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DaP.Convertitore.App;

/// <summary>
/// A registrazione finita: il file, un fotogramma, e Apri, la cartella, Modifica, Elimina.
/// </summary>
public static class RiepilogoRegistrazione
{
    public static void Mostra(string percorso, TimeSpan durata)
    {
        var d = Application.Current?.Dispatcher;
        if (d is null) return;
        d.BeginInvoke(() =>
        {
            try
            {
                var w = new FinestraRiepilogo(percorso, durata);
                w.Show();
                w.Activate();
            }
            catch (Exception e)
            {
                Registro.Errore("registrazione: riepilogo", e);
                Vassoio.Dire("Registrazione salvata", Path.GetFileName(percorso));
            }
        });
    }

    sealed class FinestraRiepilogo : Window
    {
        readonly string percorso;
        readonly Image foto = new() { Stretch = Stretch.Uniform, MaxHeight = 220 };

        public FinestraRiepilogo(string percorso, TimeSpan durata)
        {
            this.percorso = percorso;
            Title = "Registrazione";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;
            ShowInTaskbar = true;
            Topmost = true;
            Background = Pennello(0x12, 0x10, 0x1A);
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Width = 420;

            var nome = Path.GetFileName(percorso);
            long peso = 0;
            try { if (File.Exists(percorso)) peso = new FileInfo(percorso).Length; } catch { /* si vede solo il nome */ }
            var info = new TextBlock
            {
                Text = $"{nome}\n{TestoDurata(durata)}  ·  {TestoPeso(peso)}",
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Margin = new Thickness(0, 8, 0, 12),
            };
            var apri = Tasto("Apri", 0xFF, 0x3D, 0xF2, () => { Regia.ApriNelLettore(percorso); Close(); });
            var cartella = Tasto("Mostra nella cartella", 0x3A, 0x35, 0x48, () => Programma.Mostra(percorso));
            var modifica = Tasto("Modifica", 0xFF, 0xD5, 0x4A, () => { var p = percorso; Close(); Regia.Converti(p); });
            modifica.Foreground = Pennello(0x1A, 0x14, 0x08);
            var elimina = Tasto("Elimina", 0x8A, 0x2A, 0x3C, Elimina);

            var tasti = new WrapPanel();
            foreach (var b in new[] { apri, cartella, modifica, elimina }) { b.Margin = new Thickness(0, 0, 8, 8); tasti.Children.Add(b); }

            var titolo = new TextBlock { Text = "REGISTRAZIONE", Foreground = Pennello(0xFF, 0xD5, 0x4A), FontWeight = FontWeights.Bold, FontSize = 13 };
            var pila = new StackPanel { Margin = new Thickness(16) };
            pila.Children.Add(titolo);
            pila.Children.Add(new Border
            {
                Background = Brushes.Black, Margin = new Thickness(0, 10, 0, 0), MinHeight = 80,
                Child = foto,
            });
            pila.Children.Add(info);
            pila.Children.Add(tasti);
            Content = new Border
            {
                BorderBrush = Pennello(0xFF, 0xD5, 0x4A), BorderThickness = new Thickness(1),
                Background = Pennello(0x12, 0x10, 0x1A), Child = pila, Width = 420,
            };
            MouseLeftButtonDown += (_, e) =>
            {
                if (e.OriginalSource is Button) return;
                try { DragMove(); } catch { /* il mouse è già su */ }
            };
            PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
            Loaded += async (_, _) =>
            {
                try
                {
                    var png = await Task.Run(() => Fotogramma(percorso));
                    if (png is null || !IsLoaded) return;
                    var img = new BitmapImage();
                    using var ms = new MemoryStream(png);
                    img.BeginInit();
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.StreamSource = ms;
                    img.EndInit();
                    img.Freeze();
                    foto.Source = img;
                }
                catch (Exception e) { Registro.Scrivi($"registrazione: anteprima del riepilogo ({e.Message})"); }
            };
        }

        void Elimina()
        {
            try
            {
                var perche = Cestino.Sposta(percorso);
                if (File.Exists(percorso))
                {
                    if (perche is not null) Registro.Scrivi($"registrazione: cestino ({perche}), lo cancello");
                    File.Delete(percorso);
                }
                Storico.Togli(percorso);
            }
            catch (Exception e) { Registro.Errore("registrazione: elimina", e); Vassoio.Dire("Non l'ho eliminata", e.Message); return; }
            Close();
        }

        static byte[]? Fotogramma(string file)
        {
            if (!File.Exists(file) || Path.GetExtension(file).Equals(".m4a", StringComparison.OrdinalIgnoreCase)) return null;
            var psi = new ProcessStartInfo(Strumenti.Trova().Ffmpeg)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
            };
            foreach (var a in ComandiRegistrazione.ArgomentiFotogramma(file)) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p is null) return null;
            using var ms = new MemoryStream();
            p.StandardOutput.BaseStream.CopyTo(ms);
            if (!p.WaitForExit(8000)) { try { p.Kill(entireProcessTree: true); } catch { } }
            return ms.Length > 32 ? ms.ToArray() : null;
        }

        static string TestoDurata(TimeSpan t) => t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";

        static string TestoPeso(long byte_)
        {
            if (byte_ < 1024) return byte_ + " B";
            if (byte_ < 1024 * 1024) return $"{byte_ / 1024.0:0.#} KB";
            return $"{byte_ / (1024.0 * 1024):0.#} MB";
        }

        static Button Tasto(string testo, byte r, byte g, byte b, Action fai)
        {
            var btn = new Button
            {
                Content = testo, Padding = new Thickness(12, 7, 12, 7),
                Background = Pennello(r, g, b), Foreground = Brushes.White,
                BorderThickness = new Thickness(0), FontWeight = FontWeights.Bold, Cursor = Cursors.Hand,
            };
            btn.Click += (_, _) => fai();
            return btn;
        }

        static SolidColorBrush Pennello(byte r, byte g, byte b)
        {
            var p = new SolidColorBrush(Color.FromRgb(r, g, b));
            p.Freeze();
            return p;
        }
    }
}
