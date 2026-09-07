namespace QS3D.ProductBootstrapper.SmokeTests;

internal static class Program
{
    public static int Main()
    {
        ManifestContractSmoke.Run();
        Console.WriteLine("QS3D product bootstrapper smoke tests passed.");
        return 0;
    }
}

internal static class Smoke
{
    public static void True(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Equal<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message} Expected={expected} Actual={actual}");
    }

    public static void Throws<TException>(Action action, string message) where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException(message);
    }
}
