using QS3D.Platform.Application;

namespace QS3D.Cad.Host;

public sealed class StandaloneCommandCatalog
{
    private static readonly HashSet<string> ReservedApplicationCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "UNDO",
        "REDO",
        "U",
        "RE"
    };

    private readonly CommandRegistry _registry;

    internal StandaloneCommandCatalog(CommandRegistry registry)
        => _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public IReadOnlyCollection<string> Names => _registry.Names;

    public void Register(ICadCommand command)
    {
        if (command is null) throw new ArgumentNullException(nameof(command));
        var name = command.Name;
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Command name must not be blank.", nameof(command));
        if (ReservedApplicationCommands.Contains(name.Trim()))
            throw new InvalidOperationException($"Command '{name}' is reserved by the standalone application journal.");
        _registry.Register(command);
    }

    public bool Contains(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return _registry.TryResolve(name, out _);
    }
}
