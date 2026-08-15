using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;

namespace QS3D.Cad.Host;

public sealed record StandaloneDocumentReliabilitySnapshot(
    DrawingId DrawingId,
    string Name,
    string? PrimaryPath,
    bool IsDirty,
    bool HasExternalMutation,
    long CurrentStateId,
    long SavedStateId,
    string? AutosavePath,
    DateTimeOffset? LastAutosavedUtc);

public sealed class StandaloneDocumentReliability
{
    private readonly Dictionary<DrawingId, State> _states = new();

    internal void OnOpened(ICadDocument document, long semanticRevision)
    {
        ArgumentNullException.ThrowIfNull(document);
        _states[document.Id] = new State
        {
            Name = document.Name,
            CurrentStateId = 0,
            SavedStateId = 0,
            NextStateId = 0,
            KnownDatabaseRevision = document.Database.Revision,
            KnownSemanticRevision = semanticRevision
        };
    }

    internal void OnClosed(DrawingId drawingId) => _states.Remove(drawingId);

    internal bool ObserveExternalMutation(ICadDocument document, long semanticRevision)
    {
        var state = Require(document);
        if (state.KnownDatabaseRevision == document.Database.Revision
            && state.KnownSemanticRevision == semanticRevision)
            return false;

        state.CurrentStateId = checked(++state.NextStateId);
        state.KnownDatabaseRevision = document.Database.Revision;
        state.KnownSemanticRevision = semanticRevision;
        return true;
    }

    internal (long BeforeStateId, long AfterStateId) RecordMutation(ICadDocument document, long semanticRevision)
    {
        var state = Require(document);
        var before = state.CurrentStateId;
        var after = checked(++state.NextStateId);
        state.CurrentStateId = after;
        state.KnownDatabaseRevision = document.Database.Revision;
        state.KnownSemanticRevision = semanticRevision;
        return (before, after);
    }

    internal void RestoreState(ICadDocument document, long semanticRevision, long stateId)
    {
        var state = Require(document);
        if (stateId < 0 || stateId > state.NextStateId)
            throw new InvalidOperationException($"Document state {stateId} is outside the known journal range.");
        state.CurrentStateId = stateId;
        state.KnownDatabaseRevision = document.Database.Revision;
        state.KnownSemanticRevision = semanticRevision;
    }

    public StandaloneDocumentReliabilitySnapshot GetSnapshot(ICadDocument document, long semanticRevision)
    {
        ArgumentNullException.ThrowIfNull(document);
        var state = Require(document);
        var external = state.KnownDatabaseRevision != document.Database.Revision
            || state.KnownSemanticRevision != semanticRevision;
        return new StandaloneDocumentReliabilitySnapshot(
            document.Id,
            document.Name,
            state.PrimaryPath,
            state.CurrentStateId != state.SavedStateId || external,
            external,
            state.CurrentStateId,
            state.SavedStateId,
            state.AutosavePath,
            state.LastAutosavedUtc);
    }

    public void MarkOpened(ICadDocument document, long semanticRevision, string primaryPath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryPath);
        var state = Require(document);
        state.PrimaryPath = Path.GetFullPath(primaryPath);
        state.KnownDatabaseRevision = document.Database.Revision;
        state.KnownSemanticRevision = semanticRevision;
        state.SavedStateId = state.CurrentStateId;
    }

    public void MarkSaved(ICadDocument document, long semanticRevision, string primaryPath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryPath);
        ObserveExternalMutation(document, semanticRevision);
        var state = Require(document);
        state.PrimaryPath = Path.GetFullPath(primaryPath);
        state.SavedStateId = state.CurrentStateId;
        state.LastAutosavedUtc = null;
    }

    internal void MarkRecovered(ICadDocument document, long semanticRevision, string autosavePath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(autosavePath);
        var state = Require(document);
        state.CurrentStateId = checked(++state.NextStateId);
        state.SavedStateId = 0;
        state.PrimaryPath = null;
        state.AutosavePath = Path.GetFullPath(autosavePath);
        state.KnownDatabaseRevision = document.Database.Revision;
        state.KnownSemanticRevision = semanticRevision;
        state.LastAutosavedUtc = File.Exists(state.AutosavePath)
            ? new DateTimeOffset(File.GetLastWriteTimeUtc(state.AutosavePath), TimeSpan.Zero)
            : null;
    }

    internal void MarkAutosaved(ICadDocument document, string autosavePath, DateTimeOffset writtenUtc)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(autosavePath);
        var state = Require(document);
        state.AutosavePath = Path.GetFullPath(autosavePath);
        state.LastAutosavedUtc = writtenUtc;
    }

    public bool TryDiscardAutosave(DrawingId drawingId)
    {
        if (!_states.TryGetValue(drawingId, out var state) || string.IsNullOrWhiteSpace(state.AutosavePath))
            return false;
        var path = state.AutosavePath;
        state.AutosavePath = null;
        state.LastAutosavedUtc = null;
        try
        {
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private State Require(ICadDocument document)
    {
        if (!_states.TryGetValue(document.Id, out var state))
            throw new InvalidOperationException($"Document {document.Id} is not registered with the reliability tracker.");
        return state;
    }

    private sealed class State
    {
        public required string Name { get; init; }
        public string? PrimaryPath { get; set; }
        public long CurrentStateId { get; set; }
        public long SavedStateId { get; set; }
        public long NextStateId { get; set; }
        public long KnownDatabaseRevision { get; set; }
        public long KnownSemanticRevision { get; set; }
        public string? AutosavePath { get; set; }
        public DateTimeOffset? LastAutosavedUtc { get; set; }
    }
}
