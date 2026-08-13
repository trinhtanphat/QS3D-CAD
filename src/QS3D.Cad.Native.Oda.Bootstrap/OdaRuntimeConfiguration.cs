namespace QS3D.Cad.Native.Oda.Bootstrap;

public sealed record OdaRuntimeConfiguration(bool Configured, string? SdkDirectory, string? Error)
{
    public const string EnvironmentVariable = "QS3D_ODA_SDK_DIR";

    public static OdaRuntimeConfiguration Discover()
    {
        var configured = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configured))
            return new(false, null, $"{EnvironmentVariable} is not set. Native ODA integration is disabled.");
        string fullPath;
        try { fullPath = Path.GetFullPath(configured); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new(false, null, $"{EnvironmentVariable} is invalid: {ex.Message}");
        }
        if (!Directory.Exists(fullPath))
            return new(false, fullPath, $"Configured SDK directory does not exist: {fullPath}");
        return new(true, fullPath, null);
    }
}
