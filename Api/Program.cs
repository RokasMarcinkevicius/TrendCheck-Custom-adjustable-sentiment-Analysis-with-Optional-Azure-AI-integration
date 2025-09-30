
using Application.Abstractions;
using Application.Models;
using Application.UseCases;
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

// Use the extension method to configure the article analyzer
builder.Services.AddArticleAnalyzer(cfg);

builder.Services.AddScoped<SubmitArticleCommand>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();
