namespace minio_csharpClient.Models;

public class MinioOptions
{
    public const string SectionName = "Minio";

    public string Endpoint { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public bool WithSSL { get; set; } = true;
    public string? Region { get; set; }

    public string CleanEndpoint
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Endpoint))
                return string.Empty;

            var ep = Endpoint.Trim();
            if (ep.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return ep.Substring("https://".Length).TrimEnd('/');
            if (ep.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                return ep.Substring("http://".Length).TrimEnd('/');
            return ep.TrimEnd('/');
        }
    }
}
