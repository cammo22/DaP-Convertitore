using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using MiniExcelLibs;

namespace DaP.Convertitore;

/// <summary>Una tabella: le colonne e le righe. I valori sono testo, numeri, sì/no, date o niente.</summary>
public sealed class Tabella
{
    public List<string> Colonne { get; } = [];
    public List<object?[]> Righe { get; } = [];
}

/// <summary>
/// CSV, TSV, JSON ed Excel, avanti e indietro. Il CSV che esce usa il separatore di Windows (in Italia il punto e
/// virgola) e il BOM, così Excel lo apre giusto con un doppio clic. Quello che entra si annusa da solo.
/// </summary>
public static class Dati
{
    public static async Task Converti(Lavoro l, Contesto ctx)
    {
        ctx.Fase("Leggo i dati");
        var t = await Leggi(l.Sorgente, ctx);
        ctx.Fase($"Scrivo {t.Righe.Count} righe", 0.5);
        switch (l.Formato.Estensione)
        {
            case ".csv": await File.WriteAllTextAsync(l.Bozza, Csv(t, CultureInfo.CurrentCulture.TextInfo.ListSeparator.FirstOrDefault(';'), CultureInfo.CurrentCulture), new UTF8Encoding(true), ctx.Ct); break;
            case ".tsv": await File.WriteAllTextAsync(l.Bozza, Csv(t, '\t', CultureInfo.InvariantCulture), new UTF8Encoding(true), ctx.Ct); break;
            case ".json": await File.WriteAllTextAsync(l.Bozza, Json(t), new UTF8Encoding(false), ctx.Ct); break;
            case ".xlsx":
                var righe = t.Righe.Select(r =>
                {
                    var d = new Dictionary<string, object?>();
                    for (var i = 0; i < t.Colonne.Count; i++) d[t.Colonne[i]] = i < r.Length ? r[i] : null;
                    return d;
                }).ToList();
                await using (var fs = File.Create(l.Bozza)) await fs.SaveAsAsync(righe, cancellationToken: ctx.Ct);
                break;
            default: throw new ErroreConversione($"Non so scrivere {l.Formato.Etichetta}.");
        }
    }

    public static async Task<Tabella> Leggi(string percorso, Contesto ctx)
    {
        var est = Path.GetExtension(percorso).ToLowerInvariant();
        switch (est)
        {
            case ".csv":
            case ".tsv":
                return LeggiCsv(Testo.Leggi(percorso), est == ".tsv" ? '\t' : null);
            case ".json":
                return LeggiJson(Testo.Leggi(percorso));
            case ".xlsx":
            case ".xlsm":
                return LeggiExcel(percorso);
            default:
                // xls, ods, numbers: prima LibreOffice li fa diventare xlsx
                var xlsx = Path.Combine(ctx.Temporanea(), "foglio.xlsx");
                await Office.ConLibreOffice(ctx, percorso, ".xlsx", xlsx);
                return LeggiExcel(xlsx);
        }
    }

    static Tabella LeggiExcel(string percorso)
    {
        var t = new Tabella();
        var righe = MiniExcel.Query(percorso, useHeaderRow: true).Cast<IDictionary<string, object?>>().ToList();
        if (righe.Count == 0) return t;
        t.Colonne.AddRange(righe[0].Keys);
        foreach (var r in righe) t.Righe.Add(t.Colonne.Select(c => r.TryGetValue(c, out var v) ? v : null).ToArray());
        return t;
    }

    /// <summary>Il separatore si indovina dalla prima riga: punto e virgola, virgola, tab o barra.</summary>
    public static char Annusa(string testo)
    {
        var prima = testo.Split('\n').FirstOrDefault() ?? "";
        return new[] { ';', ',', '\t', '|' }.OrderByDescending(c => prima.Count(x => x == c)).First();
    }

    public static Tabella LeggiCsv(string testo, char? separatore = null)
    {
        var sep = separatore ?? Annusa(testo);
        // col punto e virgola i numeri sono quasi sempre all'italiana: 1.234,56
        var cultura = sep == ';' ? CultureInfo.GetCultureInfo("it-IT") : CultureInfo.InvariantCulture;
        var righe = SpezzaCsv(testo, sep);
        var t = new Tabella();
        if (righe.Count == 0) return t;
        var intestazione = righe[0];
        for (var i = 0; i < intestazione.Count; i++)
        {
            var nome = string.IsNullOrWhiteSpace(intestazione[i]) ? $"colonna {i + 1}" : intestazione[i].Trim();
            while (t.Colonne.Contains(nome)) nome += "_";
            t.Colonne.Add(nome);
        }
        foreach (var r in righe.Skip(1))
        {
            if (r.Count == 1 && r[0].Length == 0) continue;
            t.Righe.Add(t.Colonne.Select((_, i) => i < r.Count ? Valore(r[i], cultura) : null).ToArray());
        }
        return t;
    }

    static object? Valore(string s, CultureInfo cultura)
    {
        if (s.Length == 0) return null;
        // gli zeri davanti (CAP, codici) restano testo
        if (s.Length > 1 && s[0] == '0' && char.IsDigit(s[1])) return s;
        if (double.TryParse(s, NumberStyles.Number, cultura, out var d)) return d;
        return s;
    }

    /// <summary>CSV come lo intende Excel: virgolette che racchiudono separatori e a-capo, "" per una virgoletta.</summary>
    public static List<List<string>> SpezzaCsv(string testo, char sep)
    {
        var righe = new List<List<string>>();
        var riga = new List<string>();
        var campo = new StringBuilder();
        var dentro = false;
        for (var i = 0; i < testo.Length; i++)
        {
            var c = testo[i];
            if (dentro)
            {
                if (c == '"')
                {
                    if (i + 1 < testo.Length && testo[i + 1] == '"') { campo.Append('"'); i++; }
                    else dentro = false;
                }
                else campo.Append(c);
            }
            else if (c == '"') dentro = true;
            else if (c == sep) { riga.Add(campo.ToString()); campo.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < testo.Length && testo[i + 1] == '\n') i++;
                riga.Add(campo.ToString()); campo.Clear();
                righe.Add(riga); riga = [];
            }
            else campo.Append(c);
        }
        if (campo.Length > 0 || riga.Count > 0) { riga.Add(campo.ToString()); righe.Add(riga); }
        return righe;
    }

    public static string Csv(Tabella t, char sep, CultureInfo cultura)
    {
        var sb = new StringBuilder();
        string Cella(object? v)
        {
            var s = v switch
            {
                null => "",
                double d => d.ToString(cultura),
                DateTime dt => dt.TimeOfDay == TimeSpan.Zero ? dt.ToString("d", cultura) : dt.ToString("g", cultura),
                bool b => b ? "sì" : "no",
                IFormattable f => f.ToString(null, cultura),
                _ => v.ToString() ?? "",
            };
            return s.IndexOfAny([sep, '"', '\n', '\r']) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        }
        sb.AppendJoin(sep, t.Colonne.Select(Cella)).Append("\r\n");
        foreach (var r in t.Righe) sb.AppendJoin(sep, r.Select(Cella)).Append("\r\n");
        return sb.ToString();
    }

    public static string Json(Tabella t)
    {
        var arr = new JsonArray();
        foreach (var r in t.Righe)
        {
            var o = new JsonObject();
            for (var i = 0; i < t.Colonne.Count; i++)
            {
                var v = i < r.Length ? r[i] : null;
                o[t.Colonne[i]] = v switch
                {
                    null => null,
                    double d when d == Math.Floor(d) && Math.Abs(d) < 1e15 => JsonValue.Create((long)d),
                    double d => JsonValue.Create(d),
                    bool b => JsonValue.Create(b),
                    DateTime dt => JsonValue.Create(dt.ToString("s", CultureInfo.InvariantCulture)),
                    _ => JsonValue.Create(Convert.ToString(v, CultureInfo.InvariantCulture)),
                };
            }
            arr.Add(o);
        }
        return arr.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    /// <summary>
    /// JSON → tabella: un elenco di oggetti (o un oggetto con dentro un elenco). Gli oggetti dentro gli oggetti
    /// diventano colonne "a.b"; gli elenchi dentro restano testo JSON.
    /// </summary>
    public static Tabella LeggiJson(string testo)
    {
        var nodo = JsonNode.Parse(testo, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var elenco = nodo switch
        {
            JsonArray a => a,
            JsonObject o when o.Select(kv => kv.Value).OfType<JsonArray>().FirstOrDefault() is { } dentro && o.Count <= 3 => dentro,
            JsonObject o => new JsonArray(o.DeepClone()),
            _ => throw new ErroreConversione("Questo JSON non è una tabella."),
        };
        var t = new Tabella();
        var righe = new List<Dictionary<string, object?>>();
        foreach (var el in elenco)
        {
            var d = new Dictionary<string, object?>();
            if (el is JsonObject obj) Appiattisci(obj, "", d);
            else d["valore"] = Semplice(el);
            foreach (var k in d.Keys) if (!t.Colonne.Contains(k)) t.Colonne.Add(k);
            righe.Add(d);
        }
        foreach (var d in righe) t.Righe.Add(t.Colonne.Select(c => d.TryGetValue(c, out var v) ? v : null).ToArray());
        return t;
    }

    static void Appiattisci(JsonObject o, string prima, Dictionary<string, object?> d)
    {
        foreach (var (k, v) in o)
        {
            var chiave = prima.Length == 0 ? k : $"{prima}.{k}";
            if (v is JsonObject dentro) Appiattisci(dentro, chiave, d);
            else d[chiave] = Semplice(v);
        }
    }

    static object? Semplice(JsonNode? v) => v switch
    {
        null => null,
        JsonValue jv when jv.TryGetValue<double>(out var d) => d,
        JsonValue jv when jv.TryGetValue<bool>(out var b) => b,
        JsonValue jv when jv.TryGetValue<string>(out var s) => s,
        _ => v.ToJsonString(),
    };
}
