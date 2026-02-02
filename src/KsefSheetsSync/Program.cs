using KsefSheetsSync;

var cfg = AppConfig.LoadFromEnvironment();
Console.WriteLine($"KSeF → Google Sheets sync | baseUrl={cfg.KsefBaseUrl} | lookbackDays={cfg.LookbackDays}");

using var http = new HttpClient { BaseAddress = new Uri(cfg.KsefBaseUrl.TrimEnd('/') + "/") };
var ksef = new KsefApiClient(http);
var sheets = new GoogleSheetsWriter(cfg.Google);

var now = DateTimeOffset.UtcNow;
var from = now.AddDays(-cfg.LookbackDays);

foreach (var company in cfg.Companies)
{
    Console.WriteLine($"\n== {company.Name} ({company.Nip}) ==");

    var auth = await ksef.GetAccessTokenAsync(new KsefAuthConfig
    {
        ContextNip = company.Nip,
        KsefToken = company.KsefToken,
        RefreshToken = company.RefreshToken
    });

    Console.WriteLine($"Access token validUntil={auth.ValidUntil:O}");

    var invoices = await ksef.QueryInvoicesMetadataAsync(
        auth.AccessToken,
        company.SubjectType,
        from,
        now);

    Console.WriteLine($"Fetched {invoices.Count} invoice metadata rows");

    var sales = invoices.Where(i => string.Equals(i.Seller?.Nip, company.Nip, StringComparison.Ordinal)).ToList();
    var purchases = invoices.Where(i => string.Equals(i.Buyer?.Identifier?.Value, company.Nip, StringComparison.Ordinal)).ToList();

    await sheets.EnsureSheetsExistAndHeadersAsync(cfg.SpreadsheetId, company.SalesTabName, company.PurchasesTabName);

    var existingSales = await sheets.GetExistingKeysAsync(cfg.SpreadsheetId, company.SalesTabName);
    var existingPurchases = await sheets.GetExistingKeysAsync(cfg.SpreadsheetId, company.PurchasesTabName);

    var newSales = sales.Where(i => !existingSales.Contains(i.KsefNumber)).ToList();
    var newPurchases = purchases.Where(i => !existingPurchases.Contains(i.KsefNumber)).ToList();

    await sheets.AppendInvoicesAsync(cfg.SpreadsheetId, company.SalesTabName, newSales);
    await sheets.AppendInvoicesAsync(cfg.SpreadsheetId, company.PurchasesTabName, newPurchases);

    Console.WriteLine($"Append sales={newSales.Count}, purchases={newPurchases.Count}");
}

Console.WriteLine("\nDone.");
