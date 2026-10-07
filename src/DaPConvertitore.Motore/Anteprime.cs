using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;

namespace DaP.Convertitore;

public sealed record Anteprima(byte[] Dati, string Tipo)
{
    public string DataUrl => $"data:{Tipo};base64,{Convert.ToBase64String(Dati)}";
}

/// <summary>
/// Le figurine dei file nella coda. Video: un fotogramma vero (al 10%); foto e PDF: l'immagine o la prima pagina;
/// il resto: la stessa anteprima o icona di Esplora file.
/// </summary>
public static class Anteprime
{
    public static async Task<Anteprima?> Fai(string percorso, Categoria c, Strumenti s, double durata = 0, uint lato = 320)
    {
        try
        {
            switch (c)
            {
                case Categoria.Video:
                {
                    var jpg = Path.Combine(Strumenti.CartellaTemporanea, $"anteprima-{Guid.NewGuid():N}.jpg");
                    try
                    {
                        var t = durata > 4 ? durata * 0.1 : 0;
                        var r = await Processi.Esegui(s.Ffmpeg, ["-hide_banner", "-loglevel", "error", "-y", "-ss", t.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                            "-i", percorso, "-frames:v", "1", "-vf", $"scale='min({lato},iw)':-2", "-q:v", "4", "-update", "1", jpg], CancellationToken.None, tieniUscita: false);
                        if (r.Codice == 0 && File.Exists(jpg)) return new Anteprima(await File.ReadAllBytesAsync(jpg), "image/jpeg");
                    }
                    finally { try { File.Delete(jpg); } catch { } }
                    break;
                }
                case Categoria.Immagine:
                {
                    var ctx = new Contesto(s, new InfoHardware("", 1, [], []), CancellationToken.None, _ => { });
                    try
                    {
                        var foto = await Immagini.Apri(percorso, ctx, lato);
                        return new Anteprima(await Immagini.Codifica(Diritta(foto), BitmapEncoder.PngEncoderId), "image/png");
                    }
                    finally { ctx.Pulisci(); }
                }
                case Categoria.Pdf:
                {
                    var p = await Pdf.Copertina(percorso, lato);
                    if (p is not null) return new Anteprima(await Immagini.Codifica(p, BitmapEncoder.JpegEncoderId, 0.85f), "image/jpeg");
                    break;
                }
            }
        }
        catch (Exception e) { Registro.Scrivi($"Anteprima di {Path.GetFileName(percorso)}: {e.Message}"); }
        return await DiWindows(percorso, (int)Math.Min(lato, 256));
    }

    static SoftwareBitmap Diritta(SoftwareBitmap f) =>
        f.BitmapAlphaMode == BitmapAlphaMode.Straight ? f : SoftwareBitmap.Convert(f, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);

    /// <summary>L'anteprima di Esplora file (IShellItemImageFactory), su un thread STA come piace alla shell.</summary>
    public static Task<Anteprima?> DiWindows(string percorso, int lato)
    {
        var tcs = new TaskCompletionSource<Anteprima?>();
        var t = new Thread(() =>
        {
            try { tcs.SetResult(Shell(percorso, lato)); }
            catch (Exception e) { Registro.Scrivi($"Anteprima di Windows: {e.Message}"); tcs.SetResult(null); }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
        return tcs.Task;
    }

    static Anteprima? Shell(string percorso, int lato)
    {
        SHCreateItemFromParsingName(Path.GetFullPath(percorso), IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out var fabbrica);
        var hr = fabbrica.GetImage(new Misura { cx = lato, cy = lato }, 0x1, out var hbm);
        Marshal.ReleaseComObject(fabbrica);
        if (hr != 0 || hbm == IntPtr.Zero) return null;
        try
        {
            GetObject(hbm, Marshal.SizeOf<BITMAP>(), out var bm);
            int w = bm.bmWidth, h = bm.bmHeight;
            var px = new byte[w * h * 4];
            var bi = new BITMAPINFOHEADER { biSize = Marshal.SizeOf<BITMAPINFOHEADER>(), biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32 };
            var dc = GetDC(IntPtr.Zero);
            try { GetDIBits(dc, hbm, 0, (uint)h, px, ref bi, 0); }
            finally { ReleaseDC(IntPtr.Zero, dc); }
            // le icone hanno l'alfa; le miniature a volte no (tutto 0): allora sono opache
            if (px.Where((_, i) => i % 4 == 3).All(a => a == 0))
                for (var i = 3; i < px.Length; i += 4) px[i] = 255;
            var sb = SoftwareBitmap.CreateCopyFromBuffer(px.AsBuffer(), BitmapPixelFormat.Bgra8, w, h, BitmapAlphaMode.Premultiplied);
            var png = Immagini.Codifica(Diritta(sb), BitmapEncoder.PngEncoderId).GetAwaiter().GetResult();
            return new Anteprima(png, "image/png");
        }
        finally { DeleteObject(hbm); }
    }

    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellItemImageFactory
    {
        [PreserveSig] int GetImage(Misura size, int flags, out IntPtr phbm);
    }

    [StructLayout(LayoutKind.Sequential)] struct Misura { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAP { public int bmType, bmWidth, bmHeight, bmWidthBytes; public ushort bmPlanes, bmBitsPixel; public IntPtr bmBits; }

    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight; public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItemImageFactory ppv);
    [DllImport("gdi32.dll")] static extern int GetObject(IntPtr h, int c, out BITMAP bm);
    [DllImport("gdi32.dll")] static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint lines, [Out] byte[] bits, ref BITMAPINFOHEADER bi, uint usage);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
}
