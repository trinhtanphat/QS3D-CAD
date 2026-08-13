using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;

namespace QS3D.Cad.Host;

public sealed class StandaloneSemanticWorkspace
{
    private readonly Dictionary<DrawingId, SemanticProject> _projects = new();

    public SemanticProject Ensure(ICadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (_projects.TryGetValue(document.Id, out var project)) return project;
        project = new SemanticProject(ProjectId.New(), document.Name);
        _projects.Add(document.Id, project);
        return project;
    }

    public SemanticProject Get(ICadDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return _projects.TryGetValue(document.Id, out var project)
            ? project
            : throw new InvalidOperationException($"No semantic project is registered for drawing {document.Id.Value:D}.");
    }

    public SemanticElement TagSource(ICadDocument document, CadHandle handle, SemanticElementKind kind, string? name = null)
    {
        if (kind == SemanticElementKind.Unknown) throw new ArgumentOutOfRangeException(nameof(kind));
        var project = Ensure(document);
        var source = new CadReference(document.Id, handle);
        if (project.Elements.Any(element => element.SourceReference.HasValue && element.SourceReference.Value.Equals(source)))
            throw new InvalidOperationException($"CAD entity {handle} is already the source of a semantic element.");

        var family = project.Families.FirstOrDefault(candidate => candidate.Kind == kind);
        if (family is null)
        {
            family = new Family(FamilyId.New(), kind, $"Default {kind}");
            project.AddFamily(family);
        }

        var normalizedName = string.IsNullOrWhiteSpace(name) ? $"{kind} {project.Elements.Count + 1}" : name.Trim();
        var element = new SemanticElement(ElementId.New(), kind, normalizedName, family.Id);
        element.SetSource(source);
        project.AddElement(element);
        return element;
    }
}
