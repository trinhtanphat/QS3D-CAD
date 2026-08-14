using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Diagnostics;
using QS3D.Platform.Domain;

namespace QS3D.Cad.Host;

public static class StandaloneModelReadinessAnalyzer
{
    public static ModelHealthReport Analyze(ICadDocument document, SemanticProject project)
    {
        if (document is null) throw new ArgumentNullException(nameof(document));
        if (project is null) throw new ArgumentNullException(nameof(project));

        var findings = new List<DiagnosticFinding>(ModelReadinessAnalyzer.Analyze(project).Findings);
        HashSet<CadHandle> liveHandles;
        using (var transaction = document.Database.BeginTransaction(CadTransactionMode.ReadOnly))
            liveHandles = new HashSet<CadHandle>(transaction.Query().Select(static entity => entity.Handle));

        foreach (var element in project.Elements.OrderBy(static candidate => candidate.Id.Value))
        {
            if (element.SourceReference.HasValue)
                CheckReference(document.Id, element, element.SourceReference.Value, "source", liveHandles, findings);
            foreach (var generated in element.GeneratedReferences.OrderBy(static reference => reference.DrawingId.Value).ThenBy(static reference => reference.Handle))
                CheckReference(document.Id, element, generated, "generated", liveHandles, findings);
        }

        return new ModelHealthReport(findings);
    }

    private static void CheckReference(
        DrawingId currentDrawingId,
        SemanticElement element,
        CadReference reference,
        string role,
        HashSet<CadHandle> liveHandles,
        ICollection<DiagnosticFinding> findings)
    {
        if (reference.DrawingId != currentDrawingId) return;
        if (liveHandles.Contains(reference.Handle)) return;
        findings.Add(new DiagnosticFinding(
            "ORPHAN_HANDLE",
            DiagnosticSeverity.Error,
            $"Element '{element.Name}' {role} CAD handle {reference.Handle} does not exist in drawing {currentDrawingId.Value:D}.",
            element.Id));
    }
}
