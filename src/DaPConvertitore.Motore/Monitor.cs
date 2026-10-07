using System.Diagnostics;

namespace DaP.Convertitore;

/// <summary>Quanto lavorano CPU e scheda video, in percento. Encoder e decoder sono i blocchi NVENC/NVDEC (o AMD/Intel).</summary>
public sealed record Carico(double Cpu, double Encoder, double Decoder, double Grafica);

/// <summary>
/// Legge gli stessi contatori di Gestione attività ("GPU Engine"): funziona con NVIDIA, AMD e Intel senza
/// programmi loro. Due letture consecutive fanno un valore.
/// </summary>
public sealed class Monitor : IDisposable
{
    PerformanceCounter? cpu;
    PerformanceCounterCategory? motori;
    Dictionary<string, CounterSample> prima = [];

    public Monitor()
    {
        try { cpu = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total", true); cpu.NextValue(); }
        catch
        {
            try { cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total", true); cpu.NextValue(); }
            catch { cpu = null; }
        }
        try { motori = new PerformanceCounterCategory("GPU Engine"); }
        catch { motori = null; }
    }

    public Carico Leggi()
    {
        double c = 0;
        try { c = Math.Clamp(cpu?.NextValue() ?? 0, 0, 100); } catch { }
        double enc = 0, dec = 0, g3d = 0;
        if (motori is not null)
        {
            try
            {
                var dati = motori.ReadCategory()["utilization percentage"];
                var adesso = new Dictionary<string, CounterSample>();
                // per motore fisico (luid + engine) si sommano tutti i processi, poi si prende il motore più carico per tipo
                var somme = new Dictionary<string, double>();
                if (dati is not null)
                {
                    foreach (InstanceData d in dati.Values)
                    {
                        adesso[d.InstanceName] = d.Sample;
                        if (!prima.TryGetValue(d.InstanceName, out var vecchio)) continue;
                        var v = CounterSample.Calculate(vecchio, d.Sample);
                        var i = d.InstanceName.IndexOf("_luid_", StringComparison.Ordinal);
                        if (i < 0) continue;
                        var chiave = d.InstanceName[i..];
                        somme[chiave] = somme.GetValueOrDefault(chiave) + v;
                    }
                }
                prima = adesso;
                foreach (var (k, v) in somme)
                {
                    if (k.EndsWith("engtype_VideoEncode", StringComparison.OrdinalIgnoreCase)) enc = Math.Max(enc, v);
                    else if (k.EndsWith("engtype_VideoDecode", StringComparison.OrdinalIgnoreCase)) dec = Math.Max(dec, v);
                    else if (k.EndsWith("engtype_3D", StringComparison.OrdinalIgnoreCase)) g3d = Math.Max(g3d, v);
                }
            }
            catch { }
        }
        return new Carico(Math.Round(c, 1), Math.Round(Math.Min(enc, 100), 1), Math.Round(Math.Min(dec, 100), 1), Math.Round(Math.Min(g3d, 100), 1));
    }

    public void Dispose() => cpu?.Dispose();
}
