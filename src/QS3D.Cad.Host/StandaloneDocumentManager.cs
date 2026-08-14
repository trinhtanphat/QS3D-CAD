using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.Host;

public sealed class StandaloneDocumentManager : IDocumentManager
{
    private readonly InMemoryDocumentManager _inner = new();
    private readonly Action<ICadDocument> _opened;
    private readonly Action<DrawingId> _closed;

    internal StandaloneDocumentManager(Action<ICadDocument> opened, Action<DrawingId> closed)
    {
        _opened = opened ?? throw new ArgumentNullException(nameof(opened));
        _closed = closed ?? throw new ArgumentNullException(nameof(closed));
    }

    public IReadOnlyList<ICadDocument> Documents => _inner.Documents;
    public ICadDocument? ActiveDocument => _inner.ActiveDocument;

    public ICadDocument CreateNew(string name)
    {
        var document = _inner.CreateNew(name);
        _opened(document);
        return document;
    }

    public void Open(InMemoryCadDocument document)
    {
        if (document is null) throw new ArgumentNullException(nameof(document));
        _inner.Open(document);
        _opened(document);
    }

    public void Activate(DrawingId id) => _inner.Activate(id);

    public bool Close(DrawingId id)
    {
        if (!_inner.Close(id)) return false;
        _closed(id);
        return true;
    }
}
