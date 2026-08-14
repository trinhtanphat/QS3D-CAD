using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.Host;

internal static class CadCapabilityValidation
{
    internal const CadCapabilities Known = CadCapabilities.TwoDimensional
        | CadCapabilities.ThreeDimensional
        | CadCapabilities.Blocks
        | CadCapabilities.Xrefs
        | CadCapabilities.Layouts
        | CadCapabilities.Plot
        | CadCapabilities.NativeSolids
        | CadCapabilities.BooleanSolids
        | CadCapabilities.ObjectSnaps
        | CadCapabilities.Grips
        | CadCapabilities.Layers;

    public static void RequireKnown(CadCapabilities capabilities, string parameterName, bool allowNone)
    {
        var unknownBits = (long)capabilities & ~(long)Known;
        if (unknownBits != 0)
            throw new ArgumentOutOfRangeException(parameterName, capabilities, "CAD capabilities contain unknown flag bits.");
        if (!allowNone && capabilities == CadCapabilities.None)
            throw new ArgumentOutOfRangeException(parameterName, capabilities, "At least one CAD capability is required.");
    }
}
