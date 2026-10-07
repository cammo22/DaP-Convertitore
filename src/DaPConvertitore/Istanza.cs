using System.IO.Pipes;
using System.Text.Json;

namespace DaP.Convertitore.App;

/// <summary>
/// Un'istanza sola. Con dieci file selezionati Esplora file lancia dieci processi: il primo apre la finestra,
/// gli altri le mandano i loro file da una named pipe e si chiudono in un lampo.
/// </summary>
public static class Istanza
{
    static string Tubo => $"DaProd.Convertitore.{Environment.UserName}";

    public static bool Manda(Richiesta r)
    {
        // il primo processo può star ancora partendo: si riprova per qualche secondo
        var fine = DateTime.Now.AddSeconds(6);
        while (DateTime.Now < fine)
        {
            try
            {
                using var c = new NamedPipeClientStream(".", Tubo, PipeDirection.Out);
                c.Connect(500);
                using var w = new StreamWriter(c);
                w.Write(JsonSerializer.Serialize(r));
                w.Flush();
                return true;
            }
            catch (TimeoutException) { }
            catch (IOException) { Thread.Sleep(100); }
        }
        return false;
    }

    public static void Ascolta(Action<Richiesta> arriva)
    {
        var t = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var s = new NamedPipeServerStream(Tubo, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte);
                    s.WaitForConnection();
                    using var r = new StreamReader(s);
                    var testo = r.ReadToEnd();
                    var richiesta = JsonSerializer.Deserialize<Richiesta>(testo);
                    if (richiesta is not null) arriva(richiesta);
                }
                catch (Exception e) { Registro.Errore("istanza", e); Thread.Sleep(200); }
            }
        }) { IsBackground = true, Name = "istanza" };
        t.Start();
    }
}
