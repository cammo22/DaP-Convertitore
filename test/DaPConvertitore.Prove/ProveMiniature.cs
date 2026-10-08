using System.Text;
using DaP.Convertitore.Lettore;

namespace DaP.Convertitore.Prove;

/// <summary>Le miniature: i modelli 3D si leggono e si disegnano, la forma d'onda segue la musica, i gruppi di «Apri con».</summary>
[Collection("banco")]
public class ProveMiniature(Banco b)
{
    /// <summary>Un tetraedro: 4 facce, il modello più piccolo che si può disegnare.</summary>
    static readonly float[][] vertici = [[0, 0, 0], [1, 0, 0], [0, 1, 0], [0, 0, 1]];
    static readonly int[][] facce = [[0, 2, 1], [0, 1, 3], [0, 3, 2], [1, 2, 3]];

    string Stl(string nome, bool testo)
    {
        var p = Path.Combine(b.Cartella, nome);
        if (testo)
        {
            var sb = new StringBuilder("solid t\n");
            foreach (var f in facce)
            {
                sb.Append("facet normal 0 0 0\nouter loop\n");
                foreach (var i in f) sb.Append(FormattableString.Invariant($"vertex {vertici[i][0]} {vertici[i][1]} {vertici[i][2]}\n"));
                sb.Append("endloop\nendfacet\n");
            }
            File.WriteAllText(p, sb + "endsolid t\n");
        }
        else
        {
            using var w = new BinaryWriter(File.Create(p));
            w.Write(new byte[80]);
            w.Write((uint)facce.Length);
            foreach (var f in facce)
            {
                w.Write(0f); w.Write(0f); w.Write(0f);
                foreach (var i in f) { w.Write(vertici[i][0]); w.Write(vertici[i][1]); w.Write(vertici[i][2]); }
                w.Write((ushort)0);
            }
        }
        return p;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoStlSiLegge(bool testo)
    {
        var m = Modelli.Leggi(Stl(testo ? "t.stl" : "b.stl", testo));
        Assert.NotNull(m);
        Assert.Equal(4, m!.Triangoli);
    }

    [Fact]
    public void LObjSiLeggeConPoligoniEColori()
    {
        var mtl = Path.Combine(b.Cartella, "q.mtl");
        File.WriteAllText(mtl, "newmtl rosso\nKd 1 0 0\n");
        var p = Path.Combine(b.Cartella, "q.obj");
        File.WriteAllText(p, "mtllib q.mtl\nv 0 0 0\nv 1 0 0\nv 1 1 0\nv 0 1 0\nusemtl rosso\nf 1 2 3 4\nf -4 -3 -2\n");
        var m = Modelli.Leggi(p);
        Assert.NotNull(m);
        Assert.Equal(3, m!.Triangoli); // il quadrato in due triangoli, più il triangolo con gli indici negativi
        Assert.All(m.Colori, c => Assert.Equal(0xFFFF0000u, c));
    }

    [Fact]
    public void LoPlyAsciiSiLegge()
    {
        var p = Path.Combine(b.Cartella, "t.ply");
        File.WriteAllText(p, "ply\nformat ascii 1.0\nelement vertex 3\nproperty float x\nproperty float y\nproperty float z\nelement face 1\nproperty list uchar int vertex_indices\nend_header\n0 0 0\n1 0 0\n0 1 0\n3 0 1 2\n");
        Assert.Equal(1, Modelli.Leggi(p)?.Triangoli);
    }

    [Fact]
    public void UnModelloRovinatoDaNullEBasta()
    {
        var p = Path.Combine(b.Cartella, "rotto.glb");
        File.WriteAllBytes(p, [1, 2, 3, 4, 5]);
        Assert.Null(Modelli.Leggi(p));
    }

    [Fact]
    public void LoStudioDisegnaIlModelloInMezzoSuSfondoTrasparente()
    {
        var m = Modelli.Leggi(Stl("studio.stl", false))!;
        var d = Studio.Disegna(m, 64, 64);
        Assert.Equal(64 * 64 * 4, d.Pixel.Length);
        var coperti = Enumerable.Range(0, 64 * 64).Count(i => d.Pixel[i * 4 + 3] > 200);
        Assert.InRange(coperti, 300, 64 * 64 * 0.8); // c'è qualcosa, e non riempie tutto il quadro
        Assert.True(d.Pixel[3] == 0, "l'angolo del quadro è vuoto");
        Assert.InRange(d.Sinistra, 0, 0.5);
        Assert.InRange(d.Destra, 0.5, 1);
    }

    [Fact]
    public void LaFormaDOndaRaggruppataPortaIlPiuForteAUno()
    {
        var volumi = Enumerable.Range(0, 1000).Select(i => i < 500 ? 0.05f : 0.5f).ToList();
        var barre = Onde.Raggruppa(volumi, 10);
        Assert.Equal(10, barre.Length);
        Assert.True(barre[0] < 0.3f && barre[9] > 0.9f, string.Join(" ", barre));
        Assert.Empty(Onde.Raggruppa([], 10));
    }

    [Fact]
    public async Task LaFormaDOndaDellaMusicaVieneDaFfmpeg()
    {
        // un tono che sale e scende: le barre in mezzo sono più alte di quelle ai bordi
        var wav = b.Ffmpeg("onda-prova.wav", "-f", "lavfi", "-i", "aevalsrc='sin(2*PI*330*t)*sin(PI*t/4)':d=4:s=16000", "-ac", "1");
        var barre = await Onde.Barre(b.Strumenti, wav, 20, TimeSpan.FromSeconds(20));
        Assert.Equal(20, barre.Length);
        Assert.True(barre[10] > barre[0] + 0.3f, string.Join(" ", barre.Select(x => x.ToString("0.00"))));
        Assert.True(barre[10] > barre[19] + 0.3f);
    }

    [Theory]
    [InlineData(".pptx", "Presentazione")]
    [InlineData(".cs", "Codice")]
    [InlineData(".md", "Codice")]
    [InlineData(".srt", "Sottotitoli")]
    [InlineData(".txt", "Testo")]
    [InlineData(".docx", "Documento")]
    [InlineData(".glb", "Modello")]
    public void IGruppiDiApriConHannoIlLoroNome(string est, string gruppo)
    {
        Assert.Equal(gruppo, Vista.Gruppo(est));
        Assert.Contains(gruppo, ApriCon.Gruppi);
    }

    [Fact]
    public void VideoMusicaFotoEModelliHannoSempreLaNostraMiniatura()
    {
        Assert.True(Vista.MiniaturaSempre(".mp4"));
        Assert.True(Vista.MiniaturaSempre(".flac"));
        Assert.True(Vista.MiniaturaSempre(".jpg"));
        Assert.True(Vista.MiniaturaSempre(".stl"));
        Assert.False(Vista.MiniaturaSempre(".docx")); // Word con Office ha la sua, la nostra solo dove manca
    }
}
