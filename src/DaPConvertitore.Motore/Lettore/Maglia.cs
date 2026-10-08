using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace DaP.Convertitore.Lettore;

/// <summary>
/// Un modello 3D ridotto all'osso: solo triangoli (9 numeri l'uno: x y z dei tre vertici) e, se il modello ce l'ha,
/// un colore per triangolo (ARGB, 0 = nessuno). Basta per disegnarne la miniatura. Le coordinate sono già "Y in su".
/// </summary>
public sealed class Maglia
{
    public float[] Punti { get; }
    public uint[] Colori { get; }
    public int Triangoli => Colori.Length;

    public Maglia(float[] punti, uint[] colori) { Punti = punti; Colori = colori; }
}

/// <summary>
/// Legge GLB/GLTF, STL (binario e testo), OBJ (con i colori del .mtl), PLY (testo e binario) e 3MF.
/// Niente texture, niente animazioni: un modello è una nuvola di triangoli, e per la miniatura basta.
/// </summary>
public static class Modelli
{
    public const long MassimoByte = 400L * 1024 * 1024;

    public static bool Conosciuto(string estensione) =>
        estensione.ToLowerInvariant() is ".glb" or ".gltf" or ".stl" or ".obj" or ".ply" or ".3mf";

    /// <summary>Il modello, o null se non si legge (formato strano, file rovinato, troppo grosso).</summary>
    public static Maglia? Leggi(string percorso)
    {
        try
        {
            if (new FileInfo(percorso).Length > MassimoByte) return null;
            var m = Path.GetExtension(percorso).ToLowerInvariant() switch
            {
                ".stl" => Stl(File.ReadAllBytes(percorso)),
                ".obj" => Obj(percorso),
                ".ply" => Ply(File.ReadAllBytes(percorso)),
                ".glb" or ".gltf" => Gltf(percorso),
                ".3mf" => Tremf(percorso),
                _ => null,
            };
            return m is { Triangoli: > 0 } ? m : null;
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Registro.Scrivi($"Modello 3D non letto ({Path.GetFileName(percorso)}): {e.Message}");
            return null;
        }
    }

    /// <summary>Raccoglie i triangoli mentre si leggono.</summary>
    sealed class Raccolta
    {
        public readonly List<float> Punti = new(3000);
        public readonly List<uint> Colori = new(1000);

        public void Tri(Vector3 a, Vector3 b, Vector3 c, uint colore = 0)
        {
            Punti.Add(a.X); Punti.Add(a.Y); Punti.Add(a.Z);
            Punti.Add(b.X); Punti.Add(b.Y); Punti.Add(b.Z);
            Punti.Add(c.X); Punti.Add(c.Y); Punti.Add(c.Z);
            Colori.Add(colore);
        }

        /// <summary>Con Z in su (STL, PLY, 3MF) si rimette Y in su: (x, y, z) → (x, z, -y).</summary>
        public Maglia Fine(bool zSu)
        {
            var p = Punti.ToArray();
            if (zSu)
                for (var i = 0; i < p.Length; i += 3) { var y = p[i + 1]; p[i + 1] = p[i + 2]; p[i + 2] = -y; }
            return new Maglia(p, Colori.ToArray());
        }
    }

    static float F(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

    // ——— STL ———

    static Maglia Stl(byte[] b)
    {
        var r = new Raccolta();
        var n = b.Length >= 84 ? BitConverter.ToUInt32(b, 80) : 0;
        if (b.Length >= 84 && 84L + 50L * n == b.Length)
        {
            for (var i = 0; i < n; i++)
            {
                var o = 84 + i * 50 + 12;
                r.Tri(V(b, o), V(b, o + 12), V(b, o + 24));
            }
        }
        else
        {
            var testo = Encoding.ASCII.GetString(b);
            var v = new List<Vector3>(3);
            foreach (var riga in testo.Split('\n'))
            {
                var t = riga.Trim();
                if (!t.StartsWith("vertex", StringComparison.OrdinalIgnoreCase)) continue;
                var c = t.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (c.Length < 4) continue;
                v.Add(new Vector3(F(c[1]), F(c[2]), F(c[3])));
                if (v.Count == 3) { r.Tri(v[0], v[1], v[2]); v.Clear(); }
            }
        }
        return r.Fine(zSu: true);
    }

    static Vector3 V(byte[] b, int o) => new(BitConverter.ToSingle(b, o), BitConverter.ToSingle(b, o + 4), BitConverter.ToSingle(b, o + 8));

    // ——— OBJ ———

    static Maglia Obj(string percorso)
    {
        var r = new Raccolta();
        var v = new List<Vector3>();
        var coloriVertice = new List<uint>();
        var materiali = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        uint corrente = 0;
        foreach (var riga in File.ReadLines(percorso))
        {
            if (riga.Length < 2) continue;
            var c = riga.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (c.Length == 0) continue;
            switch (c[0])
            {
                case "v" when c.Length >= 4:
                    v.Add(new Vector3(F(c[1]), F(c[2]), F(c[3])));
                    coloriVertice.Add(c.Length >= 7 ? Colore(F(c[4]), F(c[5]), F(c[6])) : 0);
                    break;
                case "mtllib" when c.Length >= 2:
                    LeggiMtl(Path.Combine(Path.GetDirectoryName(percorso) ?? "", string.Join(' ', c.Skip(1))), materiali);
                    break;
                case "usemtl" when c.Length >= 2:
                    corrente = materiali.TryGetValue(string.Join(' ', c.Skip(1)), out var col) ? col : 0;
                    break;
                case "f" when c.Length >= 4:
                {
                    var idx = new int[c.Length - 1];
                    for (var i = 1; i < c.Length; i++)
                    {
                        var s = c[i];
                        var barra = s.IndexOf('/');
                        var k = int.Parse(barra < 0 ? s : s[..barra], CultureInfo.InvariantCulture);
                        idx[i - 1] = k < 0 ? v.Count + k : k - 1;
                    }
                    if (idx.Any(i => i < 0 || i >= v.Count)) break;
                    for (var i = 1; i + 1 < idx.Length; i++)
                    {
                        var colore = corrente != 0 ? corrente : coloriVertice[idx[0]];
                        r.Tri(v[idx[0]], v[idx[i]], v[idx[i + 1]], colore);
                    }
                    break;
                }
            }
        }
        return r.Fine(zSu: false);
    }

    static void LeggiMtl(string percorso, Dictionary<string, uint> dove)
    {
        try
        {
            if (!File.Exists(percorso)) return;
            string? nome = null;
            foreach (var riga in File.ReadLines(percorso))
            {
                var c = riga.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (c.Length >= 2 && c[0] == "newmtl") nome = string.Join(' ', c.Skip(1));
                else if (c.Length >= 4 && c[0] == "Kd" && nome is not null) dove[nome] = Colore(F(c[1]), F(c[2]), F(c[3]));
            }
        }
        catch { /* senza i colori si disegna in grigio */ }
    }

    static uint Colore(float r, float g, float b)
    {
        static uint C(float x) => (uint)Math.Clamp((int)MathF.Round(x * 255f), 0, 255);
        return 0xFF000000u | (C(r) << 16) | (C(g) << 8) | C(b);
    }

    // ——— PLY ———

    static Maglia Ply(byte[] b)
    {
        // l'intestazione è testo fino a «end_header»
        var fine = IndexDi(b, "end_header");
        if (fine < 0) throw new InvalidDataException("PLY senza intestazione");
        var dopo = fine + "end_header".Length;
        while (dopo < b.Length && b[dopo] is (byte)'\r') dopo++;
        if (dopo < b.Length && b[dopo] == (byte)'\n') dopo++;
        var intestazione = Encoding.ASCII.GetString(b, 0, fine).Split('\n').Select(s => s.Trim()).ToList();
        var formato = "ascii";
        var elementi = new List<(string Nome, int N, List<(string Nome, string Tipo, string? TipoConto)> Prop)>();
        foreach (var riga in intestazione)
        {
            var c = riga.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (c.Length >= 2 && c[0] == "format") formato = c[1];
            else if (c.Length >= 3 && c[0] == "element") elementi.Add((c[1], int.Parse(c[2]), []));
            else if (c.Length >= 3 && c[0] == "property" && elementi.Count > 0)
            {
                if (c[1] == "list" && c.Length >= 5) elementi[^1].Prop.Add((c[4], c[3], c[2]));
                else elementi[^1].Prop.Add((c[2], c[1], null));
            }
        }

        var r = new Raccolta();
        var vertici = new List<Vector3>();
        var colori = new List<uint>();
        var ascii = formato == "ascii";
        var grande = formato == "binary_big_endian";
        var pos = dopo;
        string[]? token = null;
        var t = 0;
        if (ascii) token = Encoding.ASCII.GetString(b, dopo, b.Length - dopo).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        double Leggi(string tipo)
        {
            if (ascii) return double.Parse(token![t++], NumberStyles.Float, CultureInfo.InvariantCulture);
            int n = Dimensione(tipo);
            var tmp = new byte[8];
            Array.Copy(b, pos, tmp, 0, n);
            pos += n;
            if (grande) Array.Reverse(tmp, 0, n);
            return tipo switch
            {
                "char" or "int8" => (sbyte)tmp[0],
                "uchar" or "uint8" => tmp[0],
                "short" or "int16" => BitConverter.ToInt16(tmp, 0),
                "ushort" or "uint16" => BitConverter.ToUInt16(tmp, 0),
                "int" or "int32" => BitConverter.ToInt32(tmp, 0),
                "uint" or "uint32" => BitConverter.ToUInt32(tmp, 0),
                "float" or "float32" => BitConverter.ToSingle(tmp, 0),
                "double" or "float64" => BitConverter.ToDouble(tmp, 0),
                _ => throw new InvalidDataException($"tipo PLY {tipo}"),
            };
        }

        foreach (var (nome, n, prop) in elementi)
        {
            for (var i = 0; i < n; i++)
            {
                double x = 0, y = 0, z = 0, rr = -1, gg = -1, bb = -1;
                List<int>? indici = null;
                foreach (var (pn, tipo, conto) in prop)
                {
                    if (conto is not null)
                    {
                        var k = (int)Leggi(conto);
                        var lista = new List<int>(k);
                        for (var j = 0; j < k; j++) lista.Add((int)Leggi(tipo));
                        if (nome == "face" && (pn is "vertex_indices" or "vertex_index")) indici = lista;
                    }
                    else
                    {
                        var val = Leggi(tipo);
                        switch (pn) { case "x": x = val; break; case "y": y = val; break; case "z": z = val; break; case "red": rr = val; break; case "green": gg = val; break; case "blue": bb = val; break; }
                    }
                }
                if (nome == "vertex")
                {
                    vertici.Add(new Vector3((float)x, (float)y, (float)z));
                    colori.Add(rr >= 0 ? Colore((float)rr / 255f, (float)gg / 255f, (float)bb / 255f) : 0);
                }
                else if (nome == "face" && indici is { Count: >= 3 })
                {
                    if (indici.Any(k => k < 0 || k >= vertici.Count)) continue;
                    for (var k = 1; k + 1 < indici.Count; k++)
                        r.Tri(vertici[indici[0]], vertici[indici[k]], vertici[indici[k + 1]], colori[indici[0]]);
                }
            }
        }
        return r.Fine(zSu: true);
    }

    static int Dimensione(string tipo) => tipo switch
    {
        "char" or "uchar" or "int8" or "uint8" => 1,
        "short" or "ushort" or "int16" or "uint16" => 2,
        "int" or "uint" or "float" or "int32" or "uint32" or "float32" => 4,
        "double" or "float64" => 8,
        _ => throw new InvalidDataException($"tipo PLY {tipo}"),
    };

    static int IndexDi(byte[] b, string testo)
    {
        var cerca = Encoding.ASCII.GetBytes(testo);
        var limite = Math.Min(b.Length, 64 * 1024) - cerca.Length;
        for (var i = 0; i <= limite; i++)
        {
            var uguale = true;
            for (var j = 0; j < cerca.Length && uguale; j++) uguale = b[i + j] == cerca[j];
            if (uguale) return i;
        }
        return -1;
    }

    // ——— glTF / GLB ———

    static Maglia Gltf(string percorso)
    {
        var b = File.ReadAllBytes(percorso);
        byte[]? binario = null;
        string json;
        if (b.Length > 20 && BitConverter.ToUInt32(b, 0) == 0x46546C67) // "glTF"
        {
            json = "";
            var o = 12;
            while (o + 8 <= b.Length)
            {
                var len = (int)BitConverter.ToUInt32(b, o);
                var tipo = BitConverter.ToUInt32(b, o + 4);
                if (tipo == 0x4E4F534A) json = Encoding.UTF8.GetString(b, o + 8, len);
                else if (tipo == 0x004E4942 && binario is null) binario = b.AsSpan(o + 8, len).ToArray();
                o += 8 + ((len + 3) & ~3);
            }
        }
        else json = Encoding.UTF8.GetString(b);

        using var doc = JsonDocument.Parse(json);
        var radice = doc.RootElement;
        var cartella = Path.GetDirectoryName(percorso) ?? "";

        var buffer = new List<byte[]?>();
        if (radice.TryGetProperty("buffers", out var bufs))
            foreach (var bu in bufs.EnumerateArray())
            {
                if (!bu.TryGetProperty("uri", out var uri)) { buffer.Add(binario); continue; }
                var u = uri.GetString() ?? "";
                if (u.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) buffer.Add(Convert.FromBase64String(u[(u.IndexOf(',') + 1)..]));
                else
                {
                    var f = Path.Combine(cartella, Uri.UnescapeDataString(u));
                    buffer.Add(File.Exists(f) ? File.ReadAllBytes(f) : null);
                }
            }
        var viste = radice.TryGetProperty("bufferViews", out var vv) ? vv.EnumerateArray().ToList() : [];
        var accessori = radice.TryGetProperty("accessors", out var aa) ? aa.EnumerateArray().ToList() : [];
        var mesh = radice.TryGetProperty("meshes", out var mm) ? mm.EnumerateArray().ToList() : [];
        var nodi = radice.TryGetProperty("nodes", out var nn) ? nn.EnumerateArray().ToList() : [];
        var materiali = radice.TryGetProperty("materials", out var mt) ? mt.EnumerateArray().ToList() : [];

        float[] Leggi(int accessore, int comp)
        {
            var a = accessori[accessore];
            var conto = a.GetProperty("count").GetInt32();
            var tipoComp = a.GetProperty("componentType").GetInt32();
            var normalizzato = a.TryGetProperty("normalized", out var nz) && nz.GetBoolean();
            var out_ = new float[conto * comp];
            if (!a.TryGetProperty("bufferView", out var bvp)) return out_;
            var bv = viste[bvp.GetInt32()];
            var dati = buffer[bv.GetProperty("buffer").GetInt32()] ?? throw new InvalidDataException("buffer mancante");
            var baseO = (bv.TryGetProperty("byteOffset", out var bo) ? bo.GetInt32() : 0) + (a.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0);
            var dim = tipoComp switch { 5120 or 5121 => 1, 5122 or 5123 => 2, _ => 4 };
            var passo = bv.TryGetProperty("byteStride", out var bs) && bs.GetInt32() > 0 ? bs.GetInt32() : dim * comp;
            for (var i = 0; i < conto; i++)
                for (var c = 0; c < comp; c++)
                {
                    var o = baseO + i * passo + c * dim;
                    out_[i * comp + c] = tipoComp switch
                    {
                        5120 => normalizzato ? Math.Max((sbyte)dati[o] / 127f, -1f) : (sbyte)dati[o],
                        5121 => normalizzato ? dati[o] / 255f : dati[o],
                        5122 => normalizzato ? Math.Max(BitConverter.ToInt16(dati, o) / 32767f, -1f) : BitConverter.ToInt16(dati, o),
                        5123 => normalizzato ? BitConverter.ToUInt16(dati, o) / 65535f : BitConverter.ToUInt16(dati, o),
                        5125 => BitConverter.ToUInt32(dati, o),
                        _ => BitConverter.ToSingle(dati, o),
                    };
                }
            return out_;
        }

        var r = new Raccolta();
        void Mesh(int indice, Matrix4x4 m)
        {
            foreach (var prim in mesh[indice].GetProperty("primitives").EnumerateArray())
            {
                var modo = prim.TryGetProperty("mode", out var md) ? md.GetInt32() : 4;
                if (modo is not (4 or 5 or 6)) continue;
                if (!prim.TryGetProperty("attributes", out var attr) || !attr.TryGetProperty("POSITION", out var posAcc)) continue;
                float[] pos;
                try { pos = Leggi(posAcc.GetInt32(), 3); } catch { continue; } // Draco & co: niente
                var nv = pos.Length / 3;
                uint colore = 0;
                if (prim.TryGetProperty("material", out var mat) && mat.GetInt32() < materiali.Count
                    && materiali[mat.GetInt32()].TryGetProperty("pbrMetallicRoughness", out var pbr)
                    && pbr.TryGetProperty("baseColorFactor", out var f) && f.GetArrayLength() >= 3)
                    colore = Colore(f[0].GetSingle(), f[1].GetSingle(), f[2].GetSingle());
                int[] idx;
                if (prim.TryGetProperty("indices", out var ia)) idx = Leggi(ia.GetInt32(), 1).Select(x => (int)x).ToArray();
                else idx = Enumerable.Range(0, nv).ToArray();
                Vector3 P(int i) => Vector3.Transform(new Vector3(pos[i * 3], pos[i * 3 + 1], pos[i * 3 + 2]), m);
                if (modo == 4)
                    for (var i = 0; i + 2 < idx.Length; i += 3) { if (idx[i] < nv && idx[i + 1] < nv && idx[i + 2] < nv) r.Tri(P(idx[i]), P(idx[i + 1]), P(idx[i + 2]), colore); }
                else if (modo == 5)
                    for (var i = 0; i + 2 < idx.Length; i++) { if (idx[i] < nv && idx[i + 1] < nv && idx[i + 2] < nv) { if ((i & 1) == 0) r.Tri(P(idx[i]), P(idx[i + 1]), P(idx[i + 2]), colore); else r.Tri(P(idx[i + 1]), P(idx[i]), P(idx[i + 2]), colore); } }
                else
                    for (var i = 1; i + 1 < idx.Length; i++) { if (idx[0] < nv && idx[i] < nv && idx[i + 1] < nv) r.Tri(P(idx[0]), P(idx[i]), P(idx[i + 1]), colore); }
            }
        }

        void Nodo(int i, Matrix4x4 padre, int profondita)
        {
            if (profondita > 64 || i < 0 || i >= nodi.Count) return;
            var n = nodi[i];
            var locale = Matrix4x4.Identity;
            if (n.TryGetProperty("matrix", out var mx) && mx.GetArrayLength() == 16)
            {
                var v = mx.EnumerateArray().Select(x => x.GetSingle()).ToArray();
                locale = new Matrix4x4(v[0], v[1], v[2], v[3], v[4], v[5], v[6], v[7], v[8], v[9], v[10], v[11], v[12], v[13], v[14], v[15]);
            }
            else
            {
                var s = n.TryGetProperty("scale", out var sc) ? new Vector3(sc[0].GetSingle(), sc[1].GetSingle(), sc[2].GetSingle()) : Vector3.One;
                var q = n.TryGetProperty("rotation", out var ro) ? new Quaternion(ro[0].GetSingle(), ro[1].GetSingle(), ro[2].GetSingle(), ro[3].GetSingle()) : Quaternion.Identity;
                var t = n.TryGetProperty("translation", out var tr) ? new Vector3(tr[0].GetSingle(), tr[1].GetSingle(), tr[2].GetSingle()) : Vector3.Zero;
                locale = Matrix4x4.CreateScale(s) * Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(t);
            }
            var mondo = locale * padre;
            if (n.TryGetProperty("mesh", out var me)) Mesh(me.GetInt32(), mondo);
            if (n.TryGetProperty("children", out var ch)) foreach (var c in ch.EnumerateArray()) Nodo(c.GetInt32(), mondo, profondita + 1);
        }

        var scene = radice.TryGetProperty("scenes", out var ss) ? ss.EnumerateArray().ToList() : [];
        if (scene.Count > 0)
        {
            var quale = radice.TryGetProperty("scene", out var sq) ? sq.GetInt32() : 0;
            if (scene[Math.Min(quale, scene.Count - 1)].TryGetProperty("nodes", out var radiciNodi))
                foreach (var nodo in radiciNodi.EnumerateArray()) Nodo(nodo.GetInt32(), Matrix4x4.Identity, 0);
        }
        if (r.Colori.Count == 0)
            for (var i = 0; i < mesh.Count; i++) Mesh(i, Matrix4x4.Identity);
        return r.Fine(zSu: false);
    }

    // ——— 3MF ———

    static Maglia Tremf(string percorso)
    {
        using var zip = ZipFile.OpenRead(percorso);
        const string principale = "/3D/3dmodel.model";
        var voce = zip.Entries.FirstOrDefault(e => ("/" + e.FullName).Equals(principale, StringComparison.OrdinalIgnoreCase))
                   ?? zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".model", StringComparison.OrdinalIgnoreCase))
                   ?? throw new InvalidDataException("3MF senza modello");
        var chiavePrincipale = "/" + voce.FullName;

        // i file .model: quello principale e, nei 3MF di Bambu/Orca/PrusaSlicer, quelli degli oggetti (attributo p:path)
        var file = new Dictionary<string, (XNamespace Ns, Dictionary<string, XElement> Oggetti, XDocument Doc)>(StringComparer.OrdinalIgnoreCase);
        (XNamespace Ns, Dictionary<string, XElement> Oggetti, XDocument Doc)? Carica(string chiave)
        {
            if (file.TryGetValue(chiave, out var gia)) return gia;
            var e = zip.GetEntry(chiave.TrimStart('/'));
            if (e is null) return null;
            XDocument d;
            using (var s = e.Open()) d = XDocument.Load(s);
            var ns = d.Root!.Name.Namespace;
            var ogg = new Dictionary<string, XElement>();
            foreach (var o in d.Descendants(ns + "object")) if (o.Attribute("id")?.Value is { } id) ogg[id] = o;
            return file[chiave] = (ns, ogg, d);
        }
        var radice = Carica(chiavePrincipale) ?? throw new InvalidDataException("3MF illeggibile");

        static Matrix4x4 Trasforma(string? t)
        {
            if (string.IsNullOrWhiteSpace(t)) return Matrix4x4.Identity;
            var v = t.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(F).ToArray();
            return v.Length < 12 ? Matrix4x4.Identity
                : new Matrix4x4(v[0], v[1], v[2], 0, v[3], v[4], v[5], 0, v[6], v[7], v[8], 0, v[9], v[10], v[11], 1);
        }
        static string? Percorso(XElement e) => e.Attributes().FirstOrDefault(a => a.Name.LocalName == "path")?.Value;

        var r = new Raccolta();
        void Oggetto(string chiave, string id, Matrix4x4 m, int profondita)
        {
            if (profondita > 16 || Carica(chiave) is not { } f || !f.Oggetti.TryGetValue(id, out var o)) return;
            var ns = f.Ns;
            var mesh = o.Element(ns + "mesh");
            if (mesh is not null)
            {
                var v = mesh.Element(ns + "vertices")?.Elements(ns + "vertex")
                    .Select(e => Vector3.Transform(new Vector3(F(e.Attribute("x")!.Value), F(e.Attribute("y")!.Value), F(e.Attribute("z")!.Value)), m)).ToList() ?? [];
                foreach (var t in mesh.Element(ns + "triangles")?.Elements(ns + "triangle") ?? [])
                {
                    int a = int.Parse(t.Attribute("v1")!.Value), b = int.Parse(t.Attribute("v2")!.Value), c = int.Parse(t.Attribute("v3")!.Value);
                    if (a < v.Count && b < v.Count && c < v.Count) r.Tri(v[a], v[b], v[c]);
                }
            }
            foreach (var c in o.Element(ns + "components")?.Elements(ns + "component") ?? [])
                if (c.Attribute("objectid")?.Value is { } sid)
                    Oggetto(Percorso(c) ?? chiave, sid, Trasforma(c.Attribute("transform")?.Value) * m, profondita + 1);
        }

        var voci = radice.Doc.Descendants(radice.Ns + "build").Elements(radice.Ns + "item").ToList();
        if (voci.Count > 0)
            foreach (var item in voci) Oggetto(Percorso(item) ?? chiavePrincipale, item.Attribute("objectid")!.Value, Trasforma(item.Attribute("transform")?.Value), 0);
        else
            foreach (var id in radice.Oggetti.Keys) Oggetto(chiavePrincipale, id, Matrix4x4.Identity, 0);
        return r.Fine(zSu: true);
    }
}
