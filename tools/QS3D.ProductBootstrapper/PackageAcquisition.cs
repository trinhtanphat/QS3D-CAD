using System.Net;
using System.Security.Cryptography;

namespace QS3D.ProductBootstrapper;

public interface IPackageDownloader
{
    Task DownloadAsync(ProductComponent component, string destination, CancellationToken cancellationToken);
}

public static class PackageVerifier
{
    public static void Verify(string path, ProductComponent component)
    {
        if (component.Bytes is null || string.IsNullOrWhiteSpace(component.Sha256))
            throw new InvalidDataException("Package integrity metadata is incomplete.");
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != component.Bytes.Value)
            throw new InvalidDataException("Downloaded package length does not match manifest.");
        using var stream = File.OpenRead(path);
        var actual = SHA256.HashData(stream);
        var expected = Convert.FromHexString(component.Sha256);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            throw new InvalidDataException("Downloaded package SHA-256 does not match manifest.");
    }
}

public sealed class HttpPackageDownloader : IPackageDownloader, IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    public HttpPackageDownloader(HttpClient? client = null)
    {
        if (client is not null) { _client = client; return; }
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        _ownsClient = true;
    }

    public async Task DownloadAsync(ProductComponent component, string destination, CancellationToken cancellationToken)
    {
        if (File.Exists(destination)) throw new IOException("Download destination already exists.");
        if (component.Bytes is null or <= 0 || component.Bytes > ProductFamilyManifestValidator.MaxComponentBytes)
            throw new InvalidDataException("Manifest package size is outside the bounded download policy.");
        var directory = Path.GetDirectoryName(destination) ?? throw new InvalidDataException("Download destination has no parent directory.");
        Directory.CreateDirectory(directory);
        try
        {
            using var response = await _client.GetAsync(component.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest)
                throw new InvalidDataException("Package download redirects are not accepted; manifest must identify final GitHub bytes.");
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long declared && declared != component.Bytes.Value)
                throw new InvalidDataException("HTTP content length does not match manifest.");
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            var buffer = new byte[81920];
            long total = 0;
            while (true)
            {
                var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                total += read;
                if (total > component.Bytes.Value || total > ProductFamilyManifestValidator.MaxComponentBytes)
                    throw new InvalidDataException("Downloaded package exceeded the declared/bounded size.");
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            PackageVerifier.Verify(destination, component);
        }
        catch
        {
            try { if (File.Exists(destination)) File.Delete(destination); } catch { }
            throw;
        }
    }

    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
    }
}
