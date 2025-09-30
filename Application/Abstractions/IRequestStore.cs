
using Domain.Entities;

namespace Application.Abstractions;

public interface IRequestStore
{
    ImmersiveRequest Save(ImmersiveRequest req);
    ImmersiveRequest? Get(string id);
    List<ImmersiveRequest?> GetAll();
    void Update(ImmersiveRequest req);
}
