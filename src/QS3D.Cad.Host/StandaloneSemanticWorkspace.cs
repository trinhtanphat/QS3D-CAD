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

    public bool Detach(DrawingId drawingId) => _states.Remove(drawingId);

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
        CompleteMutation(state, before);
        return element;
    }

    public Floor AddFloor(ICadDocument document, string name, double elevationM)
    {
        var state = EnsureState(document);
        var before = CloneProject(state.Project);
        var floor = new Floor(FloorId.New(), name, elevationM);
        state.Project.AddFloor(floor);
        CompleteMutation(state, before);
        return floor;
    }

    public Zone AddZone(ICadDocument document, string name)
    {
        var state = EnsureState(document);
        var before = CloneProject(state.Project);
        var zone = new Zone(ZoneId.New(), name);
        state.Project.AddZone(zone);
        CompleteMutation(state, before);
        return zone;
    }

    public SemanticElement GetElementBySource(ICadDocument document, CadHandle handle)
    {
        var state = EnsureState(document);
        return RequireElementBySource(state.Project, document.Id, handle);
    }

    public SemanticElement SetProperty(ICadDocument document, CadHandle handle, string key, string value)
    {
        var state = EnsureState(document);
        var element = RequireElementBySource(state.Project, document.Id, handle);
        var normalizedKey = string.IsNullOrWhiteSpace(key) ? throw new ArgumentException("Property key must not be blank.", nameof(key)) : key.Trim();
        if (value is null) throw new ArgumentNullException(nameof(value));
        if (element.Properties.TryGetValue(normalizedKey, out var existing) && StringComparer.Ordinal.Equals(existing, value)) return element;
        var before = CloneProject(state.Project);
        element.SetProperty(normalizedKey, value);
        CompleteMutation(state, before);
        return element;
    }

    public SemanticElement AssignLocation(ICadDocument document, CadHandle handle, FloorId? floorId, ZoneId? zoneId)
    {
        var state = EnsureState(document);
        if (floorId.HasValue && !state.Project.ContainsFloor(floorId.Value))
            throw new InvalidOperationException($"Floor {floorId.Value.Value:D} does not belong to the project.");
        if (zoneId.HasValue && !state.Project.ContainsZone(zoneId.Value))
            throw new InvalidOperationException($"Zone {zoneId.Value.Value:D} does not belong to the project.");
        var element = RequireElementBySource(state.Project, document.Id, handle);
        if (Nullable.Equals(element.FloorId, floorId) && Nullable.Equals(element.ZoneId, zoneId)) return element;
        var before = CloneProject(state.Project);
        element.AssignLocation(floorId, zoneId);
        CompleteMutation(state, before);
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

    private static SemanticElement RequireElementBySource(SemanticProject project, DrawingId drawingId, CadHandle handle)
    {
        var reference = new CadReference(drawingId, handle);
        return project.Elements.FirstOrDefault(element => element.SourceReference.HasValue && element.SourceReference.Value.Equals(reference))
            ?? throw new KeyNotFoundException($"No semantic element owns source CAD handle {handle}.");
    }

    private static void CompleteMutation(ProjectState state, SemanticProject before)
    {
        state.Undo.Push(new SemanticChange(before, CloneProject(state.Project)));
        state.Redo.Clear();
        state.Revision++;
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
