using System.Runtime.InteropServices;

namespace DaP.Convertitore.App;

/// <summary>
/// Quanto è alto il microfono e l'audio del PC, adesso. È il misuratore di Windows (non un secondo FFmpeg,
/// che terrebbe il dispositivo occupato). Se il nome scelto non si trova, si usa quello di sistema.
/// Se la chiamata non riesce, il livello resta a zero e la registrazione va avanti lo stesso.
/// </summary>
public static class LivelloAudio
{
    static string? nomeMic, nomePc;
    static IAudioMeterInformation? metroMic, metroPc;
    static bool detto;

    public static float Microfono(string? nome) => Picco(ref nomeMic, ref metroMic, nome, cattura: true);
    public static float Uscita(string? nome) => Picco(ref nomePc, ref metroPc, nome, cattura: false);

    static float Picco(ref string? tenuto, ref IAudioMeterInformation? metro, string? nome, bool cattura)
    {
        if (string.IsNullOrWhiteSpace(nome)) return 0;
        try
        {
            if (tenuto != nome || metro is null)
            {
                if (metro is not null) try { Marshal.ReleaseComObject(metro); } catch { /* già lasciato */ }
                metro = Apri(nome, cattura);
                tenuto = nome;
            }
            if (metro is null) return 0;
            metro.GetPeakValue(out var v);
            return float.IsFinite(v) ? Math.Clamp(v, 0f, 1f) : 0;
        }
        catch (Exception e)
        {
            if (!detto) { detto = true; Registro.Scrivi($"registrazione: livello audio non letto ({e.Message})"); }
            return 0;
        }
    }

    static IAudioMeterInformation? Apri(string nome, bool cattura)
    {
        var e = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        try
        {
            var flusso = cattura ? 1 : 0;
            e.EnumAudioEndpoints(flusso, 1, out var col);
            try
            {
                col.GetCount(out var n);
                for (var i = 0; i < n; i++)
                {
                    col.Item(i, out var dev);
                    var friendly = Nome(dev);
                    var va = friendly is not null && (string.Equals(friendly, nome, StringComparison.OrdinalIgnoreCase)
                        || friendly.Contains(nome, StringComparison.OrdinalIgnoreCase)
                        || nome.Contains(friendly, StringComparison.OrdinalIgnoreCase));
                    if (va && Attiva(dev) is { } m) return m;
                    try { Marshal.ReleaseComObject(dev); } catch { /* il metro lo tiene */ }
                }
            }
            finally { try { Marshal.ReleaseComObject(col); } catch { /* già lasciato */ } }
            e.GetDefaultAudioEndpoint(flusso, 0, out var def);
            return Attiva(def);
        }
        finally { try { Marshal.ReleaseComObject(e); } catch { /* già lasciato */ } }
    }

    static IAudioMeterInformation? Attiva(IMMDevice dev)
    {
        var iid = typeof(IAudioMeterInformation).GUID;
        dev.Activate(ref iid, 23, IntPtr.Zero, out var metro);
        return metro;
    }

    static string? Nome(IMMDevice dev)
    {
        dev.OpenPropertyStore(0, out var store);
        try
        {
            var chiave = new PROPERTYKEY { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };
            store.GetValue(ref chiave, out var v);
            try
            {
                if (v.vt != 31 || v.p == IntPtr.Zero) return null;
                return Marshal.PtrToStringUni(v.p);
            }
            finally { PropVariantClear(ref v); }
        }
        finally { try { Marshal.ReleaseComObject(store); } catch { /* già lasciato */ } }
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        void GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        void RegisterEndpointNotificationCallback(IntPtr client);
        void UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection
    {
        void GetCount(out int n);
        void Item(int i, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        void Activate(ref Guid iid, int clsCtx, IntPtr activation, out IAudioMeterInformation meter);
        void OpenPropertyStore(int access, out IPropertyStore store);
        void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        void GetState(out int state);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        void GetCount(out int n);
        void GetAt(int i, out PROPERTYKEY key);
        void GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
        void SetValue(ref PROPERTYKEY key, ref PROPVARIANT value);
        void Commit();
    }

    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioMeterInformation
    {
        void GetPeakValue(out float peak);
        void GetMeterChannelCount(out int n);
        void GetChannelsPeakValues(int n, IntPtr peaks);
        void QueryHardwareSupport(out int mask);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PROPERTYKEY
    {
        public Guid fmtid;
        public int pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PROPVARIANT
    {
        public ushort vt;
        public ushort w1, w2, w3;
        public IntPtr p;
        public int extra;
    }

    [DllImport("ole32.dll")] static extern int PropVariantClear(ref PROPVARIANT p);
}
