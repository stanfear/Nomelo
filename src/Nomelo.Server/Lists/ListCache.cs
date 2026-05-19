using System.Collections.Concurrent;

namespace Nomelo.Server.Lists;

public class ListCache
{
    private readonly ConcurrentDictionary<string, ListFile> _byId = new();

    public void Set(ListFile list) => _byId[list.Id] = list;

    public bool TryGet(string id, out ListFile? list)
    {
        if (_byId.TryGetValue(id, out var found)) { list = found; return true; }
        list = null;
        return false;
    }

    public void Remove(string id) => _byId.TryRemove(id, out _);
}
