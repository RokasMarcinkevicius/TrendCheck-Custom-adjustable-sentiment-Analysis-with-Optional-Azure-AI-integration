
using System.Text.RegularExpressions;
using Application.Abstractions;
using Application.Models;
using Domain.ValueObjects;

namespace Infrastructure.Nlp;

public sealed class LocalArticleAnalyzer : IArticleAnalyzer
{
    public string EngineName => "local";

    private static readonly Regex TickerRegex = new(@"\$?[A-Z]{1,5}\b", RegexOptions.Compiled);
    private static readonly Regex ParenTicker = new(@"\(([A-Z]{1,5})\)", RegexOptions.Compiled);
    private static readonly Regex MarketPrefix = new(@"(?:NASDAQ|NYSE|LSE|FWB|TSX)[:\s]-?\s*([A-Z]{1,5})", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SentenceSplit = new(@"(?<=[\.\!\?])\s+", RegexOptions.Compiled);

    private static readonly (string term, double weight)[] PosLex =
    {
        ("beat", 0.35), ("beats", 0.35), ("outperform", 0.40), ("upgrade", 0.35),
        ("bullish", 0.40), ("soar", 0.55), ("soars", 0.55), ("surge", 0.55), ("surges", 0.55),
        ("record", 0.25), ("profit", 0.25), ("growth", 0.20), ("raised guidance", 0.35),
        ("exceed", 0.25), ("exceeds", 0.25), ("strong", 0.20), ("robust", 0.20)
    };
    private static readonly (string term, double weight)[] NegLex =
    {
        ("miss", -0.35), ("misses", -0.35), ("underperform", -0.40), ("downgrade", -0.35),
        ("bearish", -0.40), ("plunge", -0.60), ("plunges", -0.60), ("tumble", -0.55), ("falls", -0.45),
        ("profit warning", -0.50), ("cut guidance", -0.40), ("loss", -0.30), ("lawsuit", -0.25),
        ("regulator", -0.15), ("probe", -0.20), ("recall", -0.30), ("delay", -0.20)
    };
    private static readonly string[] Negations = { "no", "not", "never", "without", "hardly", "barely", "scarcely" };

    public async Task<ArticleAnalysisResult> AnalyzeAsync(string text, CompanyDirectory dir, bool translate, CancellationToken ct = default)
    {
        await Task.Yield();

        var (company, ticker, evidence) = ExtractCompany(text, dir);
        var (score, bestSentence) = ScoreText(text);

        var direction = "Neutral";
        var magnitude = 0.0;
        var abs = Math.Abs(score);
        if (abs < 0.10) { direction = "Neutral"; magnitude = abs * 0.5; }
        else { direction = score > 0 ? "Up" : "Down"; magnitude = abs < 0.35 ? abs : Math.Min(1.0, abs * 1.2); }

        string? translation = null;
        if (translate) translation = "[lt] " + (bestSentence ?? evidence);

        return new ArticleAnalysisResult(
            company ?? "Unknown",
            ticker,
            direction,
            Math.Round(magnitude, 3),
            Math.Round(score, 3),
            bestSentence ?? evidence,
            translation
        );
    }

    private static (string? company, string? ticker, string evidence) ExtractCompany(string text, CompanyDirectory dir)
    {
        foreach (Match m in ParenTicker.Matches(text))
        {
            var tk = m.Groups[1].Value;
            return (FindCompanyByTicker(tk, dir) ?? tk, tk, Snip(text, m.Index));
        }
        foreach (Match m in MarketPrefix.Matches(text))
        {
            var tk = m.Groups[1].Value;
            return (FindCompanyByTicker(tk, dir) ?? tk, tk, Snip(text, m.Index));
        }
        foreach (Match m in TickerRegex.Matches(text))
        {
            var raw = m.Value.TrimStart('$');
            if (raw.Length <= 5 && raw.All(char.IsUpper))
                return (FindCompanyByTicker(raw, dir) ?? raw, raw, Snip(text, m.Index));
        }

        var tokens = Tokenize(text);
        for (int i = 0; i < tokens.Length; i++)
        {
            for (int n = 3; n >= 1; n--)
            {
                if (i + n > tokens.Length) continue;
                var span = string.Join(' ', tokens[i..(i + n)]);
                var (name, tk) = dir.Resolve(span);
                if (name is not null) return (name, tk, Snip(text, IndexOfSpan(text, span)));
            }
        }

        var best = tokens.Where(t => t.Length > 1 && char.IsUpper(t[0])).Take(3).ToArray();
        var guess = best.Length > 0 ? string.Join(' ', best) : null;
        return (guess, null, Snip(text, 0));
    }

    private static string? FindCompanyByTicker(string ticker, CompanyDirectory dir)
        => dir.NameToTicker.FirstOrDefault(kv => kv.Value.Equals(ticker, StringComparison.OrdinalIgnoreCase)).Key;

    private static string[] Tokenize(string text)
        => Regex.Matches(text, @"[A-Za-z][A-Za-z\.\-&']+").Select(m => m.Value).ToArray();

    private static (double score, string bestSentence) ScoreText(string text)
    {
        var sentences = SentenceSplit.Split(text).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
        double total = 0;
        double bestAbs = 0;
        string best = sentences.FirstOrDefault() ?? text;

        foreach (var s in sentences)
        {
            var sLower = s.ToLowerInvariant();
            double sScore = 0;

            foreach (var (term, w) in PosLex.Concat(NegLex))
            {
                if (sLower.Contains(term))
                {
                    var neg = ContainsNegationWindow(sLower, term, 3);
                    var val = neg ? -w : w;
                    sScore += val;
                }
            }

            if (sLower.Contains("beats expectations") || sLower.Contains("top estimates")) sScore += 0.15;
            if (sLower.Contains("misses expectations") || sLower.Contains("below estimates")) sScore -= 0.15;

            sScore = Math.Max(-1.0, Math.Min(1.0, sScore));

            total += sScore;
            if (Math.Abs(sScore) > bestAbs) { bestAbs = Math.Abs(sScore); best = s.Trim(); }
        }

        var score = sentences.Length > 0 ? total / Math.Max(3, sentences.Length) : total;
        score = Math.Max(-1.0, Math.Min(1.0, score));
        return (score, best);
    }

    private static bool ContainsNegationWindow(string sentenceLower, string term, int windowWords)
    {
        var idx = sentenceLower.IndexOf(term, StringComparison.Ordinal);
        if (idx <= 0) return false;
        var left = sentenceLower[..idx];
        var leftTokens = left.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var tail = leftTokens.Reverse().Take(windowWords).ToArray();
        return tail.Any(t => Negations.Contains(t.Trim().Trim('.', ',', ';', ':'), StringComparer.OrdinalIgnoreCase));
    }

    private static string Snip(string text, int idx)
    {
        if (idx < 0) idx = 0;
        var start = Math.Max(0, idx - 80);
        var len = Math.Min(text.Length - start, 160);
        return text.Substring(start, len).Replace("\n", " ").Replace("\r", " ").Trim();  // Fixed: replace -> Replace, strip -> Trim
    }

    private static int IndexOfSpan(string text, string span)
        => text.IndexOf(span, StringComparison.OrdinalIgnoreCase);
}
