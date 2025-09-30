
using System.Collections.Concurrent;
using Application.Abstractions;
using Domain.Entities;

namespace Infrastructure.State;

public sealed class InMemoryRequestStore : IRequestStore
{
    private static readonly ConcurrentDictionary<string, ImmersiveRequest> _db = new();

    public ImmersiveRequest Save(ImmersiveRequest req) { _db[req.Id] = req; return req; }
    public ImmersiveRequest? Get(string id) => _db.TryGetValue(id, out var r) ? r : null;
    public void Update(ImmersiveRequest req) => _db[req.Id] = req;
}
