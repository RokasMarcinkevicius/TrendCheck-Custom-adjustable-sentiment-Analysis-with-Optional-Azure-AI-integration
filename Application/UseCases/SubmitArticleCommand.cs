
using Application.Abstractions;
using Application.Models;
using Domain.Entities;

namespace Application.UseCases;

public sealed class SubmitArticleCommand(
    IRequestStore store,
    IEnumerable<IArticleAnalyzer> analyzers,
    CompanyDirectory directory)
{
    public ImmersiveRequest Execute(string userId, string text, string engine, bool translate)
    {
        var req = new ImmersiveRequest { UserId = userId, Text = text, Engine = engine };
        store.Save(req);

        _ = Task.Run(async () =>
        {
            try
            {
                var analyzer = analyzers.FirstOrDefault(a => string.Equals(a.EngineName, engine, StringComparison.OrdinalIgnoreCase));
                if (analyzer is null) throw new InvalidOperationException($"Unknown engine '{engine}'");
                var result = await analyzer.AnalyzeAsync(text, directory, translate);
                req.Result = result;
                req.Status = AnalysisStatus.Completed;
            }
            catch (Exception ex)
            {
                req.Status = AnalysisStatus.Failed;
                req.Error = ex.Message;
            }
            finally { store.Update(req); }
        });

        return req;
    }
}
