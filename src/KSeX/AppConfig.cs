using System.Text;

namespace KSeX;

public sealed record GoogleConfig
(
    string ServiceAccountJsonBase64,
    string ApplicationName = "KSeX"
);

public sealed record CompanyConfig
(
    string Name,
    string Nip,
    string SpreadsheetId,
    string? KsefToken = null,
    string? RefreshToken = null
);

public sealed class AppConfig
{
    public required string KsefBaseUrl { get; init; }
    public required int LookbackDays { get; init; }
    public required GoogleConfig Google { get; init; }
    public required List<CompanyConfig> Companies { get; init; }

    public static AppConfig LoadFromEnvironment()
    {
        var dotEnv = DotEnv.Load(FindDotEnvPath());

        static string? Opt(string n, IReadOnlyDictionary<string, string>? env)
        {
            var value = env != null && env.TryGetValue(n, out var v)
                ? v
                : Environment.GetEnvironmentVariable(n);
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        static string Req(string n, IReadOnlyDictionary<string, string>? env)
            => Opt(n, env) ?? throw new InvalidOperationException($"Missing env var: {n}");

        const string baseUrl = "https://api.ksef.mf.gov.pl/v2";
        var lookback = int.TryParse(Opt("LOOKBACK_DAYS", dotEnv), out var d) ? d : 7;

        var google = new GoogleConfig(ServiceAccountJsonBase64: Req("GOOGLE_SERVICE_ACCOUNT_JSON_BASE64", dotEnv));

        var companies = new List<CompanyConfig>
        {
            LoadCompany(1),
            LoadCompany(2)
        };

        return new AppConfig
        {
            KsefBaseUrl = baseUrl,
            LookbackDays = lookback,
            Google = google,
            Companies = companies
        };

        CompanyConfig LoadCompany(int idx)
        {
            var p = $"COMPANY{idx}_";
            var name = Req(p + "NAME", dotEnv);
            var nip = Req(p + "NIP", dotEnv);
            var companySpreadsheetId = Req(p + "SPREADSHEET_ID", dotEnv);
            var ksefToken = Opt(p + "KSEF_TOKEN", dotEnv);
            var refreshToken = Opt(p + "REFRESH_TOKEN", dotEnv);

            if (string.IsNullOrWhiteSpace(ksefToken) && string.IsNullOrWhiteSpace(refreshToken))
                throw new InvalidOperationException($"Set either {p}KSEF_TOKEN or {p}REFRESH_TOKEN");

            return new CompanyConfig(
                Name: name,
                Nip: nip,
                SpreadsheetId: companySpreadsheetId,
                KsefToken: string.IsNullOrWhiteSpace(ksefToken) ? null : ksefToken,
                RefreshToken: string.IsNullOrWhiteSpace(refreshToken) ? null : refreshToken
            );
        }
    }

    private static string? FindDotEnvPath()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate))
                return candidate;

            if (Directory.Exists(Path.Combine(dir.FullName, ".git")) ||
                File.Exists(Path.Combine(dir.FullName, "KSeX.sln")))
                break;

            dir = dir.Parent;
        }

        return null;
    }

    private static class DotEnv
    {
        public static Dictionary<string, string>? Load(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                if (line.StartsWith("export ", StringComparison.Ordinal))
                    line = line[7..].TrimStart();

                var idx = line.IndexOf('=');
                if (idx <= 0)
                    continue;

                var key = line[..idx].Trim();
                var value = line[(idx + 1)..].Trim();

                if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                    value = UnescapeDoubleQuoted(value[1..^1]);
                else if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
                    value = value[1..^1];

                if (key.Length > 0)
                    result[key] = value;
            }

            return result.Count == 0 ? null : result;
        }

        private static string UnescapeDoubleQuoted(string value)
        {
            var sb = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (ch == '\\' && i + 1 < value.Length)
                {
                    var next = value[++i];
                    sb.Append(next switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        '\\' => '\\',
                        '"' => '"',
                        _ => next
                    });
                }
                else
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }
    }
}
