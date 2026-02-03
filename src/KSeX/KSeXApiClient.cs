using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace KSeX;

public sealed class KSeXApiClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public KSeXApiClient(HttpClient http) => _http = http;

    public async Task<KSeXAuthResult> GetAccessTokenAsync(KSeXAuthConfig cfg, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(cfg.RefreshToken))
        {
            return await GetAccessTokenFromRefreshTokenAsync(cfg.RefreshToken!, ct);
        }

        if (string.IsNullOrWhiteSpace(cfg.KsefToken))
            throw new InvalidOperationException("KsefToken is required when RefreshToken is not set");

        return await AuthenticateWithKsefTokenAsync(cfg.ContextNip, cfg.KsefToken!, ct);
    }

    private async Task<KSeXAuthResult> GetAccessTokenFromRefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "auth/token/refresh");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshToken);

        var resp = await SendAsync<AuthenticationTokenRefreshResponse>(req, ct);
        return new KSeXAuthResult
        {
            AccessToken = resp.AccessToken.Token,
            ValidUntil = resp.AccessToken.ValidUntil
        };
    }

    private async Task<KSeXAuthResult> AuthenticateWithKsefTokenAsync(string contextNip, string ksefToken, CancellationToken ct)
    {
        // 1) challenge
        var challenge = await SendAsync<AuthenticationChallengeResponse>(
            new HttpRequestMessage(HttpMethod.Post, "auth/challenge"), ct);

        // 2) get MF public key certificate for KsefTokenEncryption
        var cert = await GetTokenEncryptionCertificateAsync(ct);
        var rsa = LoadRsaFromCertificate(cert.CertificateBase64);

        // 3) encrypt "token|timestampMs" using RSA-OAEP SHA-256
        var plain = $"{ksefToken}|{challenge.TimestampMs}";
        var cipher = rsa.Encrypt(Encoding.UTF8.GetBytes(plain), RSAEncryptionPadding.OaepSHA256);
        var enc = Convert.ToBase64String(cipher);

        // 4) init auth operation
        var initBody = new InitTokenAuthenticationRequest
        {
            Challenge = challenge.Challenge,
            ContextIdentifier = new ContextIdentifier { Type = "Nip", Value = contextNip },
            EncryptedToken = enc
        };

        using var initReq = new HttpRequestMessage(HttpMethod.Post, "auth/ksef-token")
        {
            Content = CreateJsonContent(initBody)
        };

        var init = await SendAsync<AuthenticationInitResponse>(initReq, ct);

        // 5) poll status
        for (var i = 0; i < 30; i++)
        {
            using var statusReq = new HttpRequestMessage(HttpMethod.Get, $"auth/{init.ReferenceNumber}");
            statusReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", init.AuthenticationToken.Token);
            var status = await SendAsync<AuthenticationOperationStatusResponse>(statusReq, ct);

            if (status.Status.Code == 200)
                break;
            if (status.Status.Code != 100)
                throw new InvalidOperationException($"KSeF auth failed: {status.Status.Code} - {status.Status.Description} ({string.Join("; ", status.Status.Details ?? new List<string>())})");

            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }

        // 6) redeem tokens (only once per operation)
        using var redeemReq = new HttpRequestMessage(HttpMethod.Post, "auth/token/redeem");
        redeemReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", init.AuthenticationToken.Token);
        var tokens = await SendAsync<AuthenticationTokensResponse>(redeemReq, ct);

        return new KSeXAuthResult
        {
            AccessToken = tokens.AccessToken.Token,
            ValidUntil = tokens.AccessToken.ValidUntil,
            RefreshTokenToSave = tokens.RefreshToken.Token
        };
    }

    private async Task<PublicKeyCertificate> GetTokenEncryptionCertificateAsync(CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "security/public-key-certificates");
        var certs = await SendAsync<List<PublicKeyCertificate>>(req, ct);
        var now = DateTimeOffset.UtcNow;
        var cert = certs.FirstOrDefault(c => c.Usage.Contains("KsefTokenEncryption") && c.ValidFrom <= now && now <= c.ValidTo);
        return cert ?? throw new InvalidOperationException("No public key certificate found for KsefTokenEncryption");
    }

    private static RSA LoadRsaFromCertificate(string base64Der)
    {
        var raw = Convert.FromBase64String(base64Der);
        var x509 = new X509Certificate2(raw);
        return x509.GetRSAPublicKey() ?? throw new InvalidOperationException("Certificate does not contain RSA public key");
    }

    public async Task<List<KSeXInvoiceMetadata>> QueryInvoicesMetadataAsync(
        string accessToken,
        string subjectType,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        var result = new List<KSeXInvoiceMetadata>();
        var pageOffset = 0;
        while (true)
        {
            var url = $"invoices/query/metadata?pageSize=250&pageOffset={pageOffset}&sortField=permanentStorageDate&sortDirection=Asc";
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            req.Content = CreateJsonContent(new InvoiceQueryFilters
            {
                SubjectType = subjectType,
                DateRange = new DateRange { DateType = "PermanentStorage", From = from, To = to }
            });

            var resp = await SendAsync<QueryInvoicesMetadataResponse>(req, ct);
            result.AddRange(resp.Invoices);
            if (resp.IsTruncated)
                Console.WriteLine("WARNING: isTruncated=true; narrow the dateRange to avoid 10k limit.");

            if (!resp.HasMore)
                break;

            pageOffset++;
        }

        return result;
    }

    public async Task<string> GetInvoiceXmlAsync(
        string accessToken,
        string ksefNumber,
        CancellationToken ct = default)
    {
        var url = $"invoices/ksef/{Uri.EscapeDataString(ksefNumber)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var res = await _http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"KSeF API error {(int)res.StatusCode} {res.ReasonPhrase}: {body}");

        return body;
    }

    private static StringContent CreateJsonContent<T>(T obj)
    {
        var json = JsonSerializer.Serialize(obj, JsonOpts);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage req, CancellationToken ct)
    {
        using var res = await _http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"KSeF API error {(int)res.StatusCode} {res.ReasonPhrase}: {body}");
        }

        var data = JsonSerializer.Deserialize<T>(body, JsonOpts);
        return data ?? throw new InvalidOperationException("Empty JSON response");
    }
}
