
using Application.Abstractions;
using Application.Models;
using Domain.Entities;
using Grpc.Core;

namespace GrpcService.Services;

public sealed class ArticleServiceImpl(
    IRequestStore store,
    IEnumerable<IArticleAnalyzer> analyzers,
    CompanyDirectory directory)
    : ArticleService.ArticleServiceBase
{
    public override Task<SubmitReply> Submit(SubmitRequest request, ServerCallContext context)
    {
        var req = new ImmersiveRequest { UserId = request.User ?? "anonymous", Text = request.Text, Engine = request.Engine };
        store.Save(req);

        _ = Task.Run(async () =>
        {
            try
            {
                var analyzer = analyzers.FirstOrDefault(a => string.Equals(a.EngineName, request.Engine, StringComparison.OrdinalIgnoreCase));
                analyzer ??= analyzers.First(a => a.EngineName == "local");
                var result = await analyzer.AnalyzeAsync(request.Text, directory, request.Translate, context.CancellationToken);
                req.Result = new Domain.ValueObjects.ArticleAnalysisResult(
                    result.Company, result.Ticker, result.Direction, result.Magnitude, result.SentimentScore, result.EvidenceSnippet, result.Translation);
                req.Status = AnalysisStatus.Completed;
            }
            catch (Exception ex)
            {
                req.Status = AnalysisStatus.Failed;
                req.Error = ex.Message;
            }
            finally { store.Update(req); }
        });

        return Task.FromResult(new SubmitReply { RequestId = req.Id });
    }

    public override Task<StatusReply> Status(StatusRequest request, ServerCallContext context)
    {
        var req = store.Get(request.RequestId);
        if (req is null) throw new RpcException(new Status(StatusCode.NotFound, "Not found"));
        return Task.FromResult(new StatusReply { Status = req.Status.ToString(), Engine = req.Engine ?? "", Error = req.Error ?? "" });
    }

    public override Task<ResultReply> Result(ResultRequest request, ServerCallContext context)
    {
        var req = store.Get(request.RequestId);
        if (req is null) throw new RpcException(new Status(StatusCode.NotFound, "Not found"));
        if (req.Status != AnalysisStatus.Completed) throw new RpcException(new Status(StatusCode.FailedPrecondition, $"Status: {req.Status}"));
        var r = req.Result!;
        return Task.FromResult(new ResultReply {
            Company = r.Company, Ticker = r.Ticker ?? "", Direction = r.Direction,
            Magnitude = r.Magnitude, SentimentScore = r.SentimentScore,
            EvidenceSnippet = r.EvidenceSnippet ?? "", Translation = r.Translation ?? ""
        });
    }
}
