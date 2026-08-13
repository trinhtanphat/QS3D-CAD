using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;
using QS3D.Platform.Persistence;

namespace QS3D.Cad.Host;

public sealed class StandaloneSemanticWorkspace
{
    private readonly Dictionary<DrawingId, ProjectState> _states = new();

    public SemanticProject Ensure(ICadDocument document) => EnsureState(document).Project;

    public void Attach(ICadDocument document, SemanticProject project)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(project);
        _states[document.Id] = new ProjectState(CloneProject(project));
    }

    public SemanticProject Get(ICadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return _states.TryGetValue(document.Id, out var state)
            ? state.Project
            : throw new InvalidOperationException($"No semantic project is registered for drawing {document.Id.Value:D}.");
    }

    public long Revision(ICadDocument document) => EnsureState(document).Revision;
    public bool CanUndo(ICadDocument document) => EnsureState(document).Undo.Count != 0;
    public bool CanRedo(ICadDocument document) => EnsureState(document).Redo.Count != 0;

    public SemanticElement TagSource(ICadDocument document, CadHandle handle, SemanticElementKind kind, string? name = null)
    {
        if (kind == SemanticElementKind.Unknown) throw new ArgumentOutOfRangeException(nameof(kind));
        var state = EnsureState(document);
        var before = CloneProject(state.Project);
        var source = new CadReference(document.Id, handle);
        if (state.Project.Elements.Any(element => element.SourceReference.HasValue && element.SourceReference.Value.Equals(source)))
            throw new InvalidOperationException($"CAD entity {handle} is already the source of a semantic element.");

        var family = state.Project.Families.FirstOrDefault(candidate => candidate.Kind == kind);
        if (family is null)
        {
            family = new Family(FamilyId.New(), kind, $"Default {kind}");
            state.Project.AddFamily(family);
        }

        var normalizedName = string.IsNullOrWhiteSpace(name) ? $"{kind} {state.Project.Elements.Count + 1}" : name.Trim();
        var element = new SemanticElement(ElementId.New(), kind, normalizedName, family.Id);
        element.SetSource(source);
        state.Project.AddElement(element);
        state.Undo.Push(new SemanticChange(before, CloneProject(state.Project)));
        state.Redo.Clear();
        state.Revision++;
        return element;
    }

    public void Undo(ICadDocument document)
    {
        var state = EnsureState(document);
        if (state.Undo.Count == 0) throw new InvalidOperationException("No semantic change is available to undo.");
        var change = state.Undo.Pop();
        state.Project = CloneProject(change.Before);
        state.Redo.Push(change);
        state.Revision++;
    }

    public void Redo(ICadDocument document)
    {
        var state = EnsureState(document);
        if (state.Redo.Count == 0) throw new InvalidOperationException("No semantic change is available to redo.");
        var change = state.Redo.Pop();
        state.Project = CloneProject(change.After);
        state.Undo.Push(change);
        state.Revision++;
    }

    private ProjectState EnsureState(ICadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (_states.TryGetValue(document.Id, out var state)) return state;
        state = new ProjectState(new SemanticProject(ProjectId.New(), document.Name));
        _states.Add(document.Id, state);
        return state;
    }

    private static SemanticProject CloneProject(SemanticProject source)
        => SemanticSnapshotService.Restore(SemanticSnapshotService.Capture(source));

    private sealed class ProjectState
    {
        public ProjectState(SemanticProject project) => Project = project;
        public SemanticProject Project { get; set; }
        public long Revision { get; set; }
        public Stack<SemanticChange> Undo { get; } = new();
        public Stack<SemanticChange> Redo { get; } = new();
    }

    private sealed record SemanticChange(SemanticProject Before, SemanticProject After);
}
