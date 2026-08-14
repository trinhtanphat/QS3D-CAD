using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.Host;

public enum CadBackendKind
{
    Reference = 0,
    Native = 1
}

public sealed class CadBackendDescriptor
{
    public CadBackendDescriptor(
        string id,
        string displayName,
        CadBackendKind kind,
        CadCapabilities capabilities,
        bool isAvailable,
        string? unavailableReason = null,
        int priority = 0,
        string? version = null)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Backend ID must not be blank.", nameof(id));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Backend display name must not be blank.", nameof(displayName));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        CadCapabilityValidation.RequireKnown(capabilities, nameof(capabilities), allowNone: true);
        if (priority < 0) throw new ArgumentOutOfRangeException(nameof(priority));
        if (!isAvailable && string.IsNullOrWhiteSpace(unavailableReason))
            throw new ArgumentException("Unavailable backend must provide a reason.", nameof(unavailableReason));
        Id = NormalizeId(id);
        DisplayName = displayName.Trim();
        Kind = kind;
        Capabilities = capabilities;
        IsAvailable = isAvailable;
        UnavailableReason = string.IsNullOrWhiteSpace(unavailableReason) ? null : unavailableReason.Trim();
        Priority = priority;
        Version = string.IsNullOrWhiteSpace(version) ? null : version.Trim();
    }

    public string Id { get; }
    public string DisplayName { get; }
    public CadBackendKind Kind { get; }
    public CadCapabilities Capabilities { get; }
    public bool IsAvailable { get; }
    public string? UnavailableReason { get; }
    public int Priority { get; }
    public string? Version { get; }

    private static string NormalizeId(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        foreach (var character in normalized)
        {
            var valid = (character >= 'a' && character <= 'z')
                || (character >= '0' && character <= '9')
                || character == '.' || character == '-' || character == '_';
            if (!valid) throw new ArgumentException("Backend ID contains an unsupported character.", nameof(value));
        }
        return normalized;
    }
}

public sealed class CadBackendSelectionPolicy
{
    public CadBackendSelectionPolicy(CadCapabilities requiredCapabilities, bool requireNative)
    {
        CadCapabilityValidation.RequireKnown(requiredCapabilities, nameof(requiredCapabilities), allowNone: !requireNative);
        RequiredCapabilities = requiredCapabilities;
        RequireNative = requireNative;
    }

    public CadCapabilities RequiredCapabilities { get; }
    public bool RequireNative { get; }

    public static CadBackendSelectionPolicy Development(CadCapabilities requiredCapabilities = CadCapabilities.TwoDimensional)
        => new(requiredCapabilities, false);

    public static CadBackendSelectionPolicy Production(CadCapabilities requiredCapabilities)
        => new(requiredCapabilities, true);
}

public static class CadBackendSelector
{
    public static CadBackendDescriptor Select(IEnumerable<CadBackendDescriptor> candidates, CadBackendSelectionPolicy policy)
    {
        if (candidates is null) throw new ArgumentNullException(nameof(candidates));
        if (policy is null) throw new ArgumentNullException(nameof(policy));
        var copied = candidates.ToArray();
        if (copied.Any(static candidate => candidate is null)) throw new ArgumentException("Backend candidates must not contain null entries.", nameof(candidates));

        var duplicate = copied.GroupBy(static candidate => candidate.Id, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null) throw new InvalidOperationException($"Duplicate CAD backend ID '{duplicate.Key}'.");

        var eligible = copied
            .Where(candidate => candidate.IsAvailable)
            .Where(candidate => !policy.RequireNative || candidate.Kind == CadBackendKind.Native)
            .Where(candidate => (candidate.Capabilities & policy.RequiredCapabilities) == policy.RequiredCapabilities)
            .OrderByDescending(static candidate => candidate.Kind)
            .ThenByDescending(static candidate => candidate.Priority)
            .ThenBy(static candidate => candidate.Id, StringComparer.Ordinal)
            .ToArray();

        if (eligible.Length != 0) return eligible[0];

        var required = policy.RequiredCapabilities == CadCapabilities.None ? "none" : policy.RequiredCapabilities.ToString();
        var native = policy.RequireNative ? "native " : string.Empty;
        var reasons = copied.Where(static candidate => !candidate.IsAvailable)
            .OrderBy(static candidate => candidate.Id, StringComparer.Ordinal)
            .Select(candidate => $"{candidate.Id}: {candidate.UnavailableReason}")
            .ToArray();
        var detail = reasons.Length == 0 ? string.Empty : " Unavailable backends: " + string.Join("; ", reasons) + ".";
        throw new InvalidOperationException($"No available {native}CAD backend satisfies required capabilities: {required}.{detail}");
    }
}
