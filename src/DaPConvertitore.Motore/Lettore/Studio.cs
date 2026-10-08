namespace DaP.Convertitore.Lettore;

/// <summary>Il modello disegnato: pixel BGRA premoltiplicati (sfondo trasparente) e dove sta nel quadro (0–1).</summary>
public sealed record Disegno(byte[] Pixel, int Larghezza, int Altezza, double Sinistra, double Alto, double Destra, double Basso);

/// <summary>
/// Il piccolo studio fotografico per le miniature dei modelli 3D: un rasterizzatore a software (z-buffer, luce
/// principale, controluce magenta, un lampo di riflesso) che disegna il modello a tre quarti, dall'alto, ben
/// centrato. Niente scheda video, niente finestre: gira in un processo qualsiasi e si prova.
/// </summary>
public static class Studio
{
    const float Azimut = 36f * MathF.PI / 180f;
    const float Elevazione = 24f * MathF.PI / 180f;

    /// <summary>Oltre questi triangoli le ombre morbide sono troppo care: si resta alle facce piatte.</summary>
    const int MassimoLiscio = 300_000;

    public static Disegno Disegna(Maglia m, int larghezza, int altezza, double margine = 0.1, int sovra = 2)
    {
        int W = larghezza * sovra, H = altezza * sovra;
        int n = m.Triangoli;
        var p = m.Punti;

        // 1. i vertici nello spazio dello schermo: ruotati (giro attorno a Y, poi inclinati verso chi guarda)
        float cy = MathF.Cos(Azimut), sy = MathF.Sin(Azimut), cx = MathF.Cos(Elevazione), sx = MathF.Sin(Elevazione);
        var v = new float[n * 9];
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        for (var i = 0; i < n * 3; i++)
        {
            float x = p[i * 3], y = p[i * 3 + 1], z = p[i * 3 + 2];
            float x1 = x * cy + z * sy, z1 = -x * sy + z * cy;
            float y2 = y * cx - z1 * sx, z2 = y * sx + z1 * cx;
            v[i * 3] = x1; v[i * 3 + 1] = y2; v[i * 3 + 2] = z2;
            if (x1 < minX) minX = x1; if (x1 > maxX) maxX = x1;
            if (y2 < minY) minY = y2; if (y2 > maxY) maxY = y2;
        }
        float bw = Math.Max(maxX - minX, 1e-9f), bh = Math.Max(maxY - minY, 1e-9f);
        float scala = (float)Math.Min(W * (1 - 2 * margine) / bw, H * (1 - 2 * margine) / bh);
        float ox = W / 2f - (minX + maxX) / 2f * scala, oy = H / 2f + (minY + maxY) / 2f * scala;

        // 2. le normali: una per angolo (lisce dove la superficie è dolce, nette sugli spigoli)
        var fn = new float[n * 3];
        var area = new bool[n];
        for (var t = 0; t < n; t++)
        {
            int o = t * 9;
            float ax = v[o + 3] - v[o], ay = v[o + 4] - v[o + 1], az = v[o + 5] - v[o + 2];
            float bx = v[o + 6] - v[o], by = v[o + 7] - v[o + 1], bz = v[o + 8] - v[o + 2];
            float nx = ay * bz - az * by, ny = az * bx - ax * bz, nz = ax * by - ay * bx;
            fn[t * 3] = nx; fn[t * 3 + 1] = ny; fn[t * 3 + 2] = nz;
            area[t] = nx * nx + ny * ny + nz * nz > 1e-24f;
        }
        var nc = n <= MassimoLiscio ? Lisce(v, fn, n) : null;

        // 3. i triangoli
        var zbuf = new float[W * H];
        Array.Fill(zbuf, float.NegativeInfinity);
        var px = new float[W * H * 3]; // r, g, b già ombreggiati
        var piena = new bool[W * H];
        var k1 = Norma(-0.45f, 0.65f, 0.62f);
        var k2 = Norma(0.85f, 0.12f, 0.35f);
        var mezzo = Norma(k1.x, k1.y, k1.z + 1f);
        double sMinX = double.MaxValue, sMaxX = double.MinValue, sMinY = double.MaxValue, sMaxY = double.MinValue;

        for (var t = 0; t < n; t++)
        {
            if (!area[t]) continue;
            int o = t * 9;
            float x0 = v[o] * scala + ox, y0 = oy - v[o + 1] * scala, z0 = v[o + 2];
            float x1 = v[o + 3] * scala + ox, y1 = oy - v[o + 4] * scala, z1 = v[o + 5];
            float x2 = v[o + 6] * scala + ox, y2 = oy - v[o + 7] * scala, z2 = v[o + 8];
            float areaS = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0);
            if (MathF.Abs(areaS) < 1e-9f) continue;
            int da = Math.Max(0, (int)MathF.Floor(Math.Min(x0, Math.Min(x1, x2)))), a = Math.Min(W - 1, (int)MathF.Ceiling(Math.Max(x0, Math.Max(x1, x2))));
            int dal = Math.Max(0, (int)MathF.Floor(Math.Min(y0, Math.Min(y1, y2)))), al = Math.Min(H - 1, (int)MathF.Ceiling(Math.Max(y0, Math.Max(y1, y2))));
            // il colore di base: quello del modello o un lilla caldo
            var c = m.Colori[t];
            float br = 0.86f, bg = 0.84f, bb = 0.95f;
            if (c != 0) { br = ((c >> 16) & 255) / 255f; bg = ((c >> 8) & 255) / 255f; bb = (c & 255) / 255f; }
            float inv = 1f / areaS;
            for (var yy = dal; yy <= al; yy++)
            {
                float pyy = yy + 0.5f;
                for (var xx = da; xx <= a; xx++)
                {
                    float pxx = xx + 0.5f;
                    float w0 = ((x1 - pxx) * (y2 - pyy) - (x2 - pxx) * (y1 - pyy)) * inv;
                    float w1 = ((x2 - pxx) * (y0 - pyy) - (x0 - pxx) * (y2 - pyy)) * inv;
                    float w2 = 1f - w0 - w1;
                    if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                    float z = w0 * z0 + w1 * z1 + w2 * z2;
                    var k = yy * W + xx;
                    if (z <= zbuf[k]) continue;
                    zbuf[k] = z;
                    float nx, ny, nz;
                    if (nc is not null)
                    {
                        nx = w0 * nc[o] + w1 * nc[o + 3] + w2 * nc[o + 6];
                        ny = w0 * nc[o + 1] + w1 * nc[o + 4] + w2 * nc[o + 7];
                        nz = w0 * nc[o + 2] + w1 * nc[o + 5] + w2 * nc[o + 8];
                    }
                    else { nx = fn[t * 3]; ny = fn[t * 3 + 1]; nz = fn[t * 3 + 2]; }
                    float l = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (l < 1e-12f) { nx = 0; ny = 0; nz = 1; } else { nx /= l; ny /= l; nz /= l; }
                    if (nz < 0) { nx = -nx; ny = -ny; nz = -nz; } // due facce: si guarda quella davanti
                    float d1 = Math.Max(0, nx * k1.x + ny * k1.y + nz * k1.z);
                    float d2 = Math.Max(0, nx * k2.x + ny * k2.y + nz * k2.z);
                    float amb = 0.17f + 0.14f * (0.5f + 0.5f * ny);
                    float rim = MathF.Pow(1f - nz, 3f);
                    float sp = MathF.Pow(Math.Max(0, nx * mezzo.x + ny * mezzo.y + nz * mezzo.z), 40f) * 0.38f;
                    float L = amb + 0.80f * d1;
                    float r = br * L + br * d2 * 0.20f + rim * 0.30f + sp;
                    float g = bg * L + bg * d2 * 0.09f + rim * 0.12f + sp;
                    float b = bb * L + bb * d2 * 0.19f + rim * 0.52f + sp;
                    var q = k * 3;
                    px[q] = r; px[q + 1] = g; px[q + 2] = b;
                    piena[k] = true;
                }
            }
            sMinX = Math.Min(sMinX, Math.Min(x0, Math.Min(x1, x2))); sMaxX = Math.Max(sMaxX, Math.Max(x0, Math.Max(x1, x2)));
            sMinY = Math.Min(sMinY, Math.Min(y0, Math.Min(y1, y2))); sMaxY = Math.Max(sMaxY, Math.Max(y0, Math.Max(y1, y2)));
        }

        // 4. si rimpicciolisce (media dei pixel: i bordi diventano lisci) e si impacchetta in BGRA premoltiplicato
        var uscita = new byte[larghezza * altezza * 4];
        float norm = 1f / (sovra * sovra);
        for (var y = 0; y < altezza; y++)
            for (var x = 0; x < larghezza; x++)
            {
                float r = 0, g = 0, b = 0, aa = 0;
                for (var j = 0; j < sovra; j++)
                    for (var i = 0; i < sovra; i++)
                    {
                        var k = (y * sovra + j) * W + x * sovra + i;
                        if (!piena[k]) continue;
                        r += Math.Min(1f, px[k * 3]); g += Math.Min(1f, px[k * 3 + 1]); b += Math.Min(1f, px[k * 3 + 2]); aa += 1;
                    }
                var o = (y * larghezza + x) * 4;
                uscita[o] = (byte)Math.Clamp((int)(b * norm * 255f + 0.5f), 0, 255);
                uscita[o + 1] = (byte)Math.Clamp((int)(g * norm * 255f + 0.5f), 0, 255);
                uscita[o + 2] = (byte)Math.Clamp((int)(r * norm * 255f + 0.5f), 0, 255);
                uscita[o + 3] = (byte)Math.Clamp((int)(aa * norm * 255f + 0.5f), 0, 255);
            }

        return new Disegno(uscita, larghezza, altezza,
            Math.Max(0, sMinX / W), Math.Max(0, sMinY / H), Math.Min(1, sMaxX / W), Math.Min(1, sMaxY / H));
    }

    static (float x, float y, float z) Norma(float x, float y, float z)
    {
        var l = MathF.Sqrt(x * x + y * y + z * z);
        return (x / l, y / l, z / l);
    }

    /// <summary>
    /// Normali per angolo: la media delle facce che si toccano in quel punto, ma solo di quelle che guardano quasi dalla
    /// stessa parte (meno di 45°). Così la sfera è liscia e il cubo resta con gli spigoli.
    /// </summary>
    static float[] Lisce(float[] v, float[] fn, int n)
    {
        var id = new int[n * 3];
        var mappa = new Dictionary<(float, float, float), int>(n);
        for (var i = 0; i < n * 3; i++)
        {
            var chiave = (v[i * 3], v[i * 3 + 1], v[i * 3 + 2]);
            if (!mappa.TryGetValue(chiave, out var k)) mappa[chiave] = k = mappa.Count;
            id[i] = k;
        }
        var conta = new int[mappa.Count + 1];
        for (var i = 0; i < n * 3; i++) conta[id[i] + 1]++;
        for (var i = 1; i < conta.Length; i++) conta[i] += conta[i - 1];
        var elenco = new int[n * 3];
        var riempi = (int[])conta.Clone();
        for (var i = 0; i < n * 3; i++) elenco[riempi[id[i]]++] = i / 3;

        var unitarie = new float[n * 3];
        for (var t = 0; t < n; t++)
        {
            float x = fn[t * 3], y = fn[t * 3 + 1], z = fn[t * 3 + 2], l = MathF.Sqrt(x * x + y * y + z * z);
            if (l > 0) { unitarie[t * 3] = x / l; unitarie[t * 3 + 1] = y / l; unitarie[t * 3 + 2] = z / l; }
        }
        const float soglia = 0.7071f;
        var nc = new float[n * 9];
        for (var t = 0; t < n; t++)
        {
            float ux = unitarie[t * 3], uy = unitarie[t * 3 + 1], uz = unitarie[t * 3 + 2];
            for (var c = 0; c < 3; c++)
            {
                var vid = id[t * 3 + c];
                float sx = 0, sy = 0, sz = 0;
                for (var j = conta[vid]; j < conta[vid + 1]; j++)
                {
                    var u = elenco[j];
                    if (ux * unitarie[u * 3] + uy * unitarie[u * 3 + 1] + uz * unitarie[u * 3 + 2] < soglia) continue;
                    sx += fn[u * 3]; sy += fn[u * 3 + 1]; sz += fn[u * 3 + 2]; // pesata per l'area: fn non è normalizzata
                }
                nc[t * 9 + c * 3] = sx; nc[t * 9 + c * 3 + 1] = sy; nc[t * 9 + c * 3 + 2] = sz;
            }
        }
        return nc;
    }
}
