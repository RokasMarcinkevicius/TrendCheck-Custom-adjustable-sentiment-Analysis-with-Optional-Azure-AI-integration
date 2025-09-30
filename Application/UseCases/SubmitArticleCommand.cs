
using Application.Abstractions;
using Application.Models;
using Domain.Entities;

namespace Application.UseCases;

public sealed class SubmitArticleCommand
{
    private readonly IRequestStore _store;
    private readonly IEnumerable<IArticleAnalyzer> _analyzers;
    private readonly CompanyDirectory _directory;

    public SubmitArticleCommand(IRequestStore store, IEnumerable<IArticleAnalyzer> analyzers, CompanyDirectory directory)
    {
        _store = store;
        _analyzers = analyzers;
        _directory = directory;
    }

    public ImmersiveRequest Execute(string userId, string text, string engine, bool translate)
    {
        var req = new ImmersiveRequest { UserId = userId, Text = text, Engine = engine };
        _store.Save(req);

        _ = Task.Run(async () =>
        {
            try
            {
                var analyzer = _analyzers.FirstOrDefault(a => string.Equals(a.EngineName, engine, StringComparison.OrdinalIgnoreCase));
                if (analyzer is null) throw new InvalidOperationException($"Unknown engine '{engine}'");
                var result = await analyzer.AnalyzeAsync(text, _directory, translate);
                req.Result = result;
                req.Status = AnalysisStatus.Completed;
            }
            catch (Exception ex)
            {
                req.Status = AnalysisStatus.Failed;
                req.Error = ex.Message;
            }
            finally { _store.Update(req); }
        });

        return req;
    }
}
