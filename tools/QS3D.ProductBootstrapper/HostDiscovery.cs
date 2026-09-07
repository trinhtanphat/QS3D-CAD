using Microsoft.Win32;

namespace QS3D.ProductBootstrapper;

public sealed record HostInstallation(string Product, string HostGeneration, string InstallRoot, string Source);

public interface IHostDiscovery
{
    IReadOnlyList<HostInstallation> Discover();
}

public static class HostGenerationMapper
{
    private static readonly IReadOnlyDictionary<string, string> AutoCad = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["R24.0"] = "2021",
        ["R24.1"] = "2022",
        ["R24.2"] = "2023",
        ["R24.3"] = "2024",
        ["R25.0"] = "2025",
        ["R25.1"] = "2026",
        ["R26.0"] = "2027"
    };

    public static bool TryMap(string product, string vendorVersion, out string hostGeneration)
    {
        hostGeneration = string.Empty;
        if (string.Equals(product, "autocad", StringComparison.OrdinalIgnoreCase))
        {
            if (!AutoCad.TryGetValue(vendorVersion, out var mapped)) return false;
            hostGeneration = mapped;
            return true;
        }
        if (string.Equals(product, "bricscad", StringComparison.OrdinalIgnoreCase))
        {
            if (vendorVersion.StartsWith("V25", StringComparison.OrdinalIgnoreCase)) { hostGeneration = "V25"; return true; }
            if (vendorVersion.StartsWith("V26", StringComparison.OrdinalIgnoreCase)) { hostGeneration = "V26"; return true; }
        }
        return false;
    }
}

public sealed class WindowsRegistryHostDiscovery : IHostDiscovery
{
    public IReadOnlyList<HostInstallation> Discover()
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<HostInstallation>();
        var result = new List<HostInstallation>();
#pragma warning disable CA1416
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            DiscoverAutoCad(baseKey, hive, view, result);
            DiscoverBricsCad(baseKey, hive, view, result);
        }
#pragma warning restore CA1416
        return result
            .DistinctBy(x => $"{x.Product}\u001f{x.HostGeneration}\u001f{x.InstallRoot}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Product, StringComparer.Ordinal)
            .ThenBy(x => x.HostGeneration, StringComparer.Ordinal)
            .ThenBy(x => x.InstallRoot, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

#pragma warning disable CA1416
    private static void DiscoverAutoCad(RegistryKey baseKey, RegistryHive hive, RegistryView view, List<HostInstallation> output)
    {
        using var root = baseKey.OpenSubKey(@"SOFTWARE\Autodesk\AutoCAD");
        if (root is null) return;
        foreach (var version in root.GetSubKeyNames())
        {
            if (!HostGenerationMapper.TryMap("autocad", version, out var generation)) continue;
            using var versionKey = root.OpenSubKey(version);
            if (versionKey is null) continue;
            var locales = versionKey.GetSubKeyNames();
            if (locales.Length == 0)
            {
                AddAutoCadLocation(versionKey, generation, hive, view, version, output);
                continue;
            }
            foreach (var locale in locales)
            {
                using var localeKey = versionKey.OpenSubKey(locale);
                if (localeKey is not null) AddAutoCadLocation(localeKey, generation, hive, view, version, output);
            }
        }
    }

    private static void AddAutoCadLocation(RegistryKey key, string generation, RegistryHive hive, RegistryView view, string version, List<HostInstallation> output)
    {
        var rootFolder = key.GetValue("AcadLocation") as string ?? key.GetValue("InstallLocation") as string;
        if (!string.IsNullOrWhiteSpace(rootFolder))
            output.Add(new("autocad", generation, rootFolder, $"registry:{hive}:{view}:{version}"));
    }

    private static void DiscoverBricsCad(RegistryKey baseKey, RegistryHive hive, RegistryView view, List<HostInstallation> output)
    {
        using var root = baseKey.OpenSubKey(@"SOFTWARE\Bricsys\BricsCAD");
        if (root is null) return;
        foreach (var version in root.GetSubKeyNames())
        {
            if (!HostGenerationMapper.TryMap("bricscad", version, out var generation)) continue;
            using var versionKey = root.OpenSubKey(version);
            var rootFolder = versionKey?.GetValue("InstallPath") as string ?? versionKey?.GetValue("InstallLocation") as string;
            if (!string.IsNullOrWhiteSpace(rootFolder))
                output.Add(new("bricscad", generation, rootFolder, $"registry:{hive}:{view}:{version}"));
        }
    }
#pragma warning restore CA1416
}
