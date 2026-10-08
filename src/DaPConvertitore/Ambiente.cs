using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;

namespace DaP.Convertitore.App;

/// <summary>
/// Quello che le finestre hanno in comune: un WebView2 solo per tutte (stesso profilo, stesso processo del browser:
/// la seconda finestra si apre al volo), le stesse regole di sicurezza, e il vestito di Windows 11.
/// </summary>
public static class Ambiente
{
    public const string Host = "dap.locale";
    static Task<CoreWebView2Environment>? env;

    public static Task<CoreWebView2Environment> Prendi() => env ??= CoreWebView2Environment.CreateAsync(
        null, Path.Combine(Strumenti.CartellaDati, "webview"), new CoreWebView2EnvironmentOptions
        {
            Language = "it-IT",
            // il lettore parte da solo quando apri un video o una canzone, come ogni lettore
            AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required",
        });

    /// <summary>Niente menu del browser, niente zoom, niente navigazione fuori dall'interfaccia.</summary>
    public static void Prepara(CoreWebView2 w)
    {
        w.Settings.IsNonClientRegionSupportEnabled = true;
        w.Settings.AreDefaultContextMenusEnabled = false;
        w.Settings.IsStatusBarEnabled = false;
        w.Settings.IsZoomControlEnabled = false;
        w.Settings.AreBrowserAcceleratorKeysEnabled = false;
        w.Settings.IsSwipeNavigationEnabled = false;
#if !DEBUG
        w.Settings.AreDevToolsEnabled = false;
#endif
        w.SetVirtualHostNameToFolderMapping(Host, Path.Combine(AppContext.BaseDirectory, "ui"), CoreWebView2HostResourceAccessKind.DenyCors);
        // niente navigazione fuori dall'interfaccia: i link esterni si aprono nel browser, e solo dal ponte
        var sviluppo = Environment.GetEnvironmentVariable("DAP_UI");
        w.NavigationStarting += (_, e) =>
        {
            if (e.Uri.StartsWith($"https://{Host}/") || (!string.IsNullOrEmpty(sviluppo) && e.Uri.StartsWith(sviluppo))) return;
            e.Cancel = true;
        };
        w.NewWindowRequested += (_, e) => e.Handled = true;
    }

    /// <summary>La pagina: quella vera accanto all'exe, o quella del server di Vite mentre si sviluppa (DAP_UI).</summary>
    public static string Pagina(string nome)
    {
        var sviluppo = Environment.GetEnvironmentVariable("DAP_UI");
        return string.IsNullOrEmpty(sviluppo) ? $"https://{Host}/{nome}" : sviluppo.TrimEnd('/') + "/" + nome;
    }

    // ——— Windows 11: tema scuro, angoli tondi, bordo ———
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public static void Windows11(IntPtr hwnd, bool tondo = true)
    {
        int si = 1, angoli = tondo ? 2 : 1, bordo = 0x00322A2A; // COLORREF è BGR: #2A2A32
        DwmSetWindowAttribute(hwnd, 20, ref si, sizeof(int));     // DWMWA_USE_IMMERSIVE_DARK_MODE
        DwmSetWindowAttribute(hwnd, 33, ref angoli, sizeof(int)); // DWMWA_WINDOW_CORNER_PREFERENCE
        DwmSetWindowAttribute(hwnd, 34, ref bordo, sizeof(int));  // DWMWA_BORDER_COLOR
    }

    // ——— il monitor della finestra, per lo schermo intero ———
    [StructLayout(LayoutKind.Sequential)] struct Rett { public int L, T, R, B; }
    [StructLayout(LayoutKind.Sequential)] struct InfoMonitor { public int cbSize; public Rett Monitor, Lavoro; public uint Flags; }
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] static extern bool GetMonitorInfo(IntPtr m, ref InfoMonitor i);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr dopo, int x, int y, int cx, int cy, uint flags);

    /// <summary>La finestra copre tutto il monitor dov'è, barra delle applicazioni compresa (in pixel veri).</summary>
    public static void SuTuttoLoSchermo(IntPtr hwnd)
    {
        var i = new InfoMonitor { cbSize = Marshal.SizeOf<InfoMonitor>() };
        if (!GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref i)) return;
        SetWindowPos(hwnd, IntPtr.Zero, i.Monitor.L, i.Monitor.T, i.Monitor.R - i.Monitor.L, i.Monitor.B - i.Monitor.T, 0x0040 | 0x0004); // SHOWWINDOW | NOZORDER
    }

    // ——— Windows ———
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHOpenWithDialog(IntPtr hwnd, ref OpenAsInfo info);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct OpenAsInfo { public string File; public string? Classe; public int Flags; }

    /// <summary>La finestra «Apri con» di Windows, per scegliere un altro programma (o farlo predefinito).</summary>
    public static void ApriConAltro(IntPtr hwnd, string percorso)
    {
        // OAIF_ALLOW_REGISTRATION | OAIF_REGISTER_EXT | OAIF_EXEC
        var info = new OpenAsInfo { File = percorso, Flags = 0x1 | 0x2 | 0x4 };
        _ = SHOpenWithDialog(hwnd, ref info);
    }
}
