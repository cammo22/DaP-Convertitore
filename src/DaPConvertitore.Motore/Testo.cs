using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;

namespace DaP.Convertitore;

/// <summary>
/// Testi: TXT, Markdown e HTML. Diventano una pagina impaginata pulita, e da lì PDF (lo stampa WebView2, il motore
/// di Edge), Word (LibreOffice) o testo semplice.
/// </summary>
public static partial class Testo
{
    static readonly MarkdownPipeline md = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    public static async Task Converti(Lavoro l, Contesto ctx)
    {
        var est = l.Formato.Estensione;
        var sorg = Path.GetExtension(l.Sorgente).ToLowerInvariant();
        ctx.Fase("Leggo il testo");
        var testo = Leggi(l.Sorgente);

        if (est == ".txt")
        {
            var semplice = sorg switch
            {
                ".md" or ".markdown" => Markdown.ToPlainText(testo, md),
                ".html" or ".htm" when ctx.Stampante is not null => await ctx.Stampante.Testo(l.Sorgente, ctx.Ct),
                ".html" or ".htm" => SenzaTag(testo),
                _ => testo,
            };
            await File.WriteAllTextAsync(l.Bozza, semplice.Trim() + Environment.NewLine, new UTF8Encoding(true), ctx.Ct);
            return;
        }

        // la pagina: l'HTML com'è, o quella fatta da noi per Markdown e TXT
        string pagina;
        if (sorg is ".html" or ".htm") pagina = l.Sorgente;
        else
        {
            var corpo = sorg is ".md" or ".markdown" ? Markdown.ToHtml(testo, md) : DaTesto(testo);
            pagina = Path.Combine(ctx.Temporanea(), "pagina.html");
            await File.WriteAllTextAsync(pagina, Impagina(corpo, Nomi.Radice(l.Sorgente), Path.GetDirectoryName(Path.GetFullPath(l.Sorgente))!), new UTF8Encoding(false), ctx.Ct);
        }

        switch (est)
        {
            case ".html":
                File.Copy(pagina, l.Bozza, true);
                break;
            case ".pdf":
                if (ctx.Stampante is null) throw new ErroreConversione("Per il PDF serve la finestra del convertitore.");
                ctx.Fase("Stampo in PDF", 0.5);
                await ctx.Stampante.Stampa(pagina, l.Bozza, ctx.Ct);
                break;
            case ".docx":
                await Office.ConLibreOffice(ctx, pagina, ".docx", l.Bozza, "HTML (StarWriter)");
                break;
            default:
                throw new ErroreConversione($"Non so fare {l.Formato.Etichetta} da un testo.");
        }
    }

    /// <summary>UTF-8 se lo è davvero, se no Latin-1 (i vecchi TXT di Windows con le accentate).</summary>
    public static string Leggi(string percorso)
    {
        var byte_ = File.ReadAllBytes(percorso);
        try
        {
            return new UTF8Encoding(false, true).GetString(byte_).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(byte_);
        }
    }

    static string DaTesto(string testo)
    {
        var paragrafi = Regex.Split(testo.Replace("\r\n", "\n"), @"\n\s*\n");
        return string.Join("\n", paragrafi.Where(p => p.Trim().Length > 0)
            .Select(p => $"<p>{WebUtility.HtmlEncode(p.TrimEnd()).Replace("\n", "<br>")}</p>"));
    }

    static string SenzaTag(string html)
    {
        html = Regex.Replace(html, @"<(script|style)[\s\S]*?</\1>", "", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<(br|/p|/div|/h\d|/li|/tr)\s*/?>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, "<[^>]+>", "");
        return Regex.Replace(WebUtility.HtmlDecode(html), @"\n{3,}", "\n\n");
    }

    /// <summary>La pagina da stampare: carattere leggibile, margini da libro, tabelle e codice in ordine.</summary>
    public static string Impagina(string corpo, string titolo, string cartellaBase) => $$"""
        <!doctype html>
        <html lang="it">
        <head>
        <meta charset="utf-8">
        <base href="{{new Uri(cartellaBase.TrimEnd('\\') + "\\").AbsoluteUri}}">
        <title>{{WebUtility.HtmlEncode(titolo)}}</title>
        <style>
          @page { size: A4; }
          html { background: #fff; }
          body { font: 11.5pt/1.6 "Segoe UI", system-ui, sans-serif; color: #1d1b24; max-width: 46em; margin: 0 auto; }
          h1, h2, h3, h4 { font-family: "Segoe UI Semibold", "Segoe UI", sans-serif; line-height: 1.25; margin: 1.4em 0 .5em; color: #120e1c; }
          h1 { font-size: 22pt; border-bottom: 3px solid #ffd54a; padding-bottom: .2em; }
          h2 { font-size: 16pt; } h3 { font-size: 13pt; }
          p { margin: .6em 0; }
          a { color: #7a2cbf; }
          img { max-width: 100%; }
          code { font: 10pt Consolas, "Cascadia Mono", monospace; background: #f3f0f8; padding: .1em .3em; border-radius: 3px; }
          pre { background: #f3f0f8; padding: .8em 1em; border-radius: 6px; overflow-wrap: anywhere; white-space: pre-wrap; }
          pre code { background: none; padding: 0; }
          blockquote { margin: 1em 0; padding: .2em 1em; border-left: 4px solid #ff3df2; color: #4a4558; }
          table { border-collapse: collapse; margin: 1em 0; width: 100%; }
          th, td { border: 1px solid #d8d3e2; padding: .35em .6em; text-align: left; vertical-align: top; }
          th { background: #f3f0f8; }
          hr { border: 0; border-top: 1px solid #d8d3e2; margin: 2em 0; }
        </style>
        </head>
        <body>
        {{corpo}}
        </body>
        </html>
        """;
}
