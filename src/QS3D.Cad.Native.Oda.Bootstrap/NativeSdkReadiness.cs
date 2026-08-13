namespace QS3D.Cad.Native.Oda.Bootstrap;

public enum NativeSdkReadinessState
{
    NotConfigured = 0,
    DirectoryMissing,
    ConfiguredUnqualified
}

public sealed class NativeSdkReadiness
{
    public NativeSdkReadiness(NativeSdkReadinessState state, string message, string? sdkRoot = null)
    {
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Readiness message must not be blank.", nameof(message));
        State = state;
        Message = message.Trim();
        SdkRoot = sdkRoot;
    }

    public NativeSdkReadinessState State { get; }
    public string Message { get; }
    public string? SdkRoot { get; }

    public bool IsConfigured => State == NativeSdkReadinessState.ConfiguredUnqualified;

    // Deliberately false until a real licensed adapter executes its capability
    // qualification suite. A directory existing on disk is not runtime proof.
    public bool IsProductionQualified => false;
}

public static class NativeSdkReadinessProbe
{
    public const string EnvironmentVariable = "QS3D_ODA_SDK_DIR";

    public static NativeSdkReadiness Inspect()
        => Inspect(Environment.GetEnvironmentVariable(EnvironmentVariable));

    public static NativeSdkReadiness Inspect(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return new NativeSdkReadiness(
                NativeSdkReadinessState.NotConfigured,
                $"{EnvironmentVariable} is not configured. Native DWG/render/3D capabilities are unavailable.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(configuredPath.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new NativeSdkReadiness(
                NativeSdkReadinessState.DirectoryMissing,
                $"{EnvironmentVariable} does not contain a valid filesystem path.");
        }

        if (!Directory.Exists(fullPath))
        {
            return new NativeSdkReadiness(
                NativeSdkReadinessState.DirectoryMissing,
                $"Configured native SDK directory does not exist: {fullPath}",
                fullPath);
        }

        return new NativeSdkReadiness(
            NativeSdkReadinessState.ConfiguredUnqualified,
            "Native SDK directory exists, but no SDK binding, version compatibility, DWG round-trip, viewport or 3D capability is qualified yet.",
            fullPath);
    }
}
