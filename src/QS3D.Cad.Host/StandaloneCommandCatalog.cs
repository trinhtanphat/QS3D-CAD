using QS3D.Platform.Application;

namespace QS3D.Cad.Host;

public sealed class StandaloneCommandCatalog
{
    private static readonly HashSet<string> ReservedApplicationCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "UNDO",
        "REDO"
    };

    private readonly CommandRegistry _registry;

    internal StandaloneCommandCatalog(CommandRegistry registry)
        => _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public IReadOnlyCollection<string> Names => _registry.Names;

    public void Register(ICadCommand command)
    {
        if (command is null) throw new ArgumentNullException(nameof(command));
        if (ReservedApplicationCommands.Contains(command.Name?.Trim() ?? string.Empty))
            throw new InvalidOperationException($"Command '{command.Name}' is reserved by the standalone application journal.");
        _registry.Register(command);
    }

    public bool TryResolve(string name, out ICadCommand? command) => _registry.TryResolve(name, out command);
}
