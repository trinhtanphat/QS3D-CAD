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
        var sha256 = component.Sha256;
        if (component.Bytes is null || string.IsNullOrWhiteSpace(sha256))
            throw new InvalidDataException("Package integrity metadata is incomplete.");
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != component.Bytes.Value)
            throw new InvalidDataException("Downloaded package length does not match manifest.");
        using var stream = File.OpenRead(path);
        var actual = SHA256.HashData(stream);
        var expected = Convert.FromHexString(sha256);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
            throw new InvalidDataException("Downloaded package SHA-256 does not match manifest.");
    }
}

public sealed class HttpPackageDownloader : IPackageDownloader, IDisposable
{
    private static readonly HashSet<string> TrustedDownloadHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com",
        "release-assets.githubusercontent.com",
        "objects.githubusercontent.com"
    };

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
        var downloadUrl = component.DownloadUrl;
        if (string.IsNullOrWhiteSpace(downloadUrl) || !Uri.TryCreate(downloadUrl, UriKind.Absolute, out var initialUri))
            throw new InvalidDataException("Manifest package download URL is missing or invalid.");
        var directory = Path.GetDirectoryName(destination) ?? throw new InvalidDataException("Download destination has no parent directory.");
        Directory.CreateDirectory(directory);
        try
        {
            using var response = await GetTrustedResponseAsync(initialUri, cancellationToken).ConfigureAwait(false);
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

    private async Task<HttpResponseMessage> GetTrustedResponseAsync(Uri initialUri, CancellationToken cancellationToken)
    {
        var current = initialUri;
        for (var redirectCount = 0; redirectCount <= 3; redirectCount++)
        {
            ValidateTrustedUri(current, allowSignedQuery: redirectCount > 0);
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            var effectiveUri = response.RequestMessage?.RequestUri ?? current;
            ValidateTrustedUri(effectiveUri, allowSignedQuery: !string.Equals(effectiveUri.Host, "github.com", StringComparison.OrdinalIgnoreCase));
            if (response.StatusCode is >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest)
            {
                var location = response.Headers.Location;
                if (location is null)
                {
                    response.Dispose();
                    throw new InvalidDataException("Package redirect omitted Location.");
                }
                var next = location.IsAbsoluteUri ? location : new Uri(current, location);
                response.Dispose();
                current = next;
                continue;
            }
            return response;
        }
        throw new InvalidDataException("Package download exceeded trusted redirect limit.");
    }

    private static void ValidateTrustedUri(Uri uri, bool allowSignedQuery)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || !TrustedDownloadHosts.Contains(uri.Host) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment) ||
            (!allowSignedQuery && !string.IsNullOrEmpty(uri.Query)))
            throw new InvalidDataException("Package download left the trusted GitHub release origin set.");
    }

    public void Dispose()
    {
        if (_ownsClient) _client.Dispose();
    }
}
