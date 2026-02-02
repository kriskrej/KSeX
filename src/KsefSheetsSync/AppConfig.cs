namespace KsefSheetsSync;

public sealed record GoogleConfig
(
    string ServiceAccountJsonBase64,
    string ApplicationName = "KsefSheetsSync"
);

public sealed record CompanyConfig
(
    string Name,
    string Nip,
    string SalesTabName,
    string PurchasesTabName,
    string SubjectType,
    string? KsefToken = null,
    string? RefreshToken = null
);

public sealed class AppConfig
{
    public required string KsefBaseUrl { get; init; }
    public required int LookbackDays { get; init; }
    public required string SpreadsheetId { get; init; }
    public required GoogleConfig Google { get; init; }
    public required List<CompanyConfig> Companies { get; init; }

    public static AppConfig LoadFromEnvironment()
    {
        static string? Opt(string n) => Environment.GetEnvironmentVariable(n);
        static string Req(string n) => Opt(n) ?? throw new InvalidOperationException($"Missing env var: {n}");

        var baseUrl = Opt("KSEF_BASE_URL") ?? "https://api.ksef.mf.gov.pl/v2";
        var lookback = int.TryParse(Opt("LOOKBACK_DAYS"), out var d) ? d : 7;

        var google = new GoogleConfig(ServiceAccountJsonBase64: Req("GOOGLE_SERVICE_ACCOUNT_JSON_BASE64"));
        var spreadsheetId = Req("SPREADSHEET_ID");

        var companies = new List<CompanyConfig>
        {
            LoadCompany(1),
            LoadCompany(2)
        };

        return new AppConfig
        {
            KsefBaseUrl = baseUrl,
            LookbackDays = lookback,
            SpreadsheetId = spreadsheetId,
            Google = google,
            Companies = companies
        };

        CompanyConfig LoadCompany(int idx)
        {
            var p = $"COMPANY{idx}_";
            var name = Req(p + "NAME");
            var nip = Req(p + "NIP");
            var subjectType = Opt(p + "SUBJECT_TYPE") ?? "Subject1";
            var salesTab = Opt(p + "SALES_TAB") ?? $"C{idx}_Sales";
            var purchasesTab = Opt(p + "PURCHASES_TAB") ?? $"C{idx}_Purchases";
            var ksefToken = Opt(p + "KSEF_TOKEN");
            var refreshToken = Opt(p + "REFRESH_TOKEN");

            if (string.IsNullOrWhiteSpace(ksefToken) && string.IsNullOrWhiteSpace(refreshToken))
                throw new InvalidOperationException($"Set either {p}KSEF_TOKEN or {p}REFRESH_TOKEN");

            return new CompanyConfig(
                Name: name,
                Nip: nip,
                SalesTabName: salesTab,
                PurchasesTabName: purchasesTab,
                SubjectType: subjectType,
                KsefToken: string.IsNullOrWhiteSpace(ksefToken) ? null : ksefToken,
                RefreshToken: string.IsNullOrWhiteSpace(refreshToken) ? null : refreshToken
            );
        }
    }
}
