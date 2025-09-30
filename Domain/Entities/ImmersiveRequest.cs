
namespace Domain.Entities;

public enum AnalysisStatus { Pending, Completed, Failed }

public sealed class ImmersiveRequest
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string UserId { get; init; } = "anonymous";
    public string Text { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public AnalysisStatus Status { get; set; } = AnalysisStatus.Pending;
    public string? Engine { get; set; } // local | azure
    public string? Error { get; set; }
    public Domain.ValueObjects.ArticleAnalysisResult? Result { get; set; }
}
