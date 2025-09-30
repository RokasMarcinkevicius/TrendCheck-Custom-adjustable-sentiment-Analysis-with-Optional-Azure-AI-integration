using Application.Abstractions;
using Application.UseCases;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/analyze")]
public sealed class AnalyzeController(IRequestStore store, SubmitArticleCommand submit) : ControllerBase
{
    public sealed record SubmitDto(string user, string text, string engine = "local", bool translate = false);

    [HttpPost("submit")]
    public ActionResult<object> Submit([FromBody] SubmitDto body)
    {
        if (string.IsNullOrWhiteSpace(body.text)) return BadRequest("text is required");
        var req = submit.Execute(body.user ?? "anonymous", body.text, body.engine, body.translate);
        return Ok(new { requestId = req.Id });
    }

    [HttpGet("status/{id}")]
    public ActionResult<object> Status([FromRoute] string id)
    {
        var req = store.Get(id);
        if (req is null) return NotFound();
        return Ok(new { status = req.Status.ToString(), engine = req.Engine, error = req.Error });
    }

    [HttpGet("result/{id}")]
    public ActionResult<object> Result([FromRoute] string id)
    {
        var req = store.Get(id);
        if (req is null) return NotFound();
        if (req.Status != AnalysisStatus.Completed) return BadRequest(new { status = req.Status.ToString() });
        return Ok(req.Result);
    }

    [HttpGet("all")]
    public ActionResult<IEnumerable<object>> GetAll()
    {
        var allRequests = store.GetAll();
        return Ok(allRequests.Select(req => new
        {
            requestId = req.Id,
            user = req.User,
            status = req.Status.ToString(),
            engine = req.Engine,
            createdAt = req.CreatedAt,
            result = req.Status == AnalysisStatus.Completed ? req.Result : null,
            error = req.Error
        }));
    }
}
