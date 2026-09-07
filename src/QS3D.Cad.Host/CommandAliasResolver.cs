namespace QS3D.Cad.Host;

internal static class CommandAliasResolver
{
    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["L"] = "LINE",
        ["C"] = "CIRCLE",
        ["A"] = "ARC",
        ["PO"] = "POINT",
        ["POL"] = "POLYGON",
        ["REC"] = "RECTANG",
        ["M"] = "MOVE",
        ["CO"] = "COPY",
        ["CP"] = "COPY",
        ["SC"] = "SCALE",
        ["RO"] = "ROTATE",
        ["MI"] = "MIRROR",
        ["E"] = "ERASE",
        ["LA"] = "LAYER",
        ["B"] = "BLOCK",
        ["I"] = "INSERT",
        ["ZE"] = "ZOOMEXTENTS",
        ["ZW"] = "ZOOMWINDOW",
        ["U"] = "UNDO",
        ["RE"] = "REDO",
        ["DI"] = "DIST",
        ["ME"] = "MEASURE"
    };

    public static string Resolve(string commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName))
            throw new ArgumentException("Command name must not be blank.", nameof(commandName));
        var trimmed = commandName.Trim();
        return Aliases.TryGetValue(trimmed, out var canonical) ? canonical : trimmed;
    }
}
