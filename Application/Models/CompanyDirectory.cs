
namespace Application.Models;

public sealed class CompanyDirectory
{
    public Dictionary<string, string> NameToTicker { get; } = new(StringComparer.OrdinalIgnoreCase);

    public CompanyDirectory(IEnumerable<(string name, string ticker, string[] aliases)> seed)
    {
        foreach (var (name, ticker, aliases) in seed)
        {
            NameToTicker[name] = ticker;
            foreach (var a in aliases) NameToTicker[a] = ticker;
        }
    }

    public (string? company, string? ticker) Resolve(string candidate)
        => NameToTicker.TryGetValue(candidate, out var t) ? (candidate, t) : (null, null);
}
