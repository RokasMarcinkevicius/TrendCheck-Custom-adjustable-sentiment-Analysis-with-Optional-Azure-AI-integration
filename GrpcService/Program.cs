
using Application.Abstractions;
using Application.Models;
using GrpcService.Services;
using Infrastructure.Azure;
using Infrastructure.State;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

var seed = new (string name, string ticker, string[] aliases)[]
{
    ("Apple Inc.", "AAPL", new[]{ "Apple", "AAPL" }),
    ("Microsoft Corporation", "MSFT", new[]{ "Microsoft", "MSFT" }),
    ("NVIDIA Corporation", "NVDA", new[]{ "NVIDIA", "NVDA" }),
    ("Tesla, Inc.", "TSLA", new[]{ "Tesla", "TSLA" }),
};
builder.Services.AddSingleton(new CompanyDirectory(seed));
builder.Services.AddSingleton<IRequestStore, InMemoryRequestStore>();

// Use the extension method instead of manual configuration
builder.Services.AddArticleAnalyzer(cfg);

builder.Services.AddGrpc();
var app = builder.Build();

app.MapGrpcService<ArticleServiceImpl>();
app.MapGet("/", () => "gRPC service running. Use a gRPC client.");
app.Run();
