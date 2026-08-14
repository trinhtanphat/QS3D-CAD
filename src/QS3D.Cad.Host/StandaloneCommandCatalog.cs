using QS3D.Platform.Application;

namespace QS3D.Cad.Host;

public sealed class StandaloneCommandCatalog
{
    private readonly CommandRegistry _registry;

    internal StandaloneCommandCatalog(CommandRegistry registry)
        => _registry = registry ?? throw new ArgumentNullException(nameof(registry));

    public IReadOnlyCollection<string> Names => _registry.Names;

    public void Register(ICadCommand command) => _registry.Register(command);

    public bool TryResolve(string name, out ICadCommand? command) => _registry.TryResolve(name, out command);
}
