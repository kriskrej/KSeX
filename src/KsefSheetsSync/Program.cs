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

    var invoiceByNumber = new Dictionary<string, KsefInvoiceMetadata>(StringComparer.Ordinal);
    var subjectErrors = new List<Exception>();
    var anySuccess = false;

    foreach (var subjectType in company.SubjectTypes.Distinct(StringComparer.OrdinalIgnoreCase))
    {
        try
        {
            var rows = await ksef.QueryInvoicesMetadataAsync(
                auth.AccessToken,
                subjectType,
                from,
                now);

            anySuccess = true;
            Console.WriteLine($"Fetched {rows.Count} invoice metadata rows (subjectType={subjectType})");

            foreach (var invoice in rows)
                invoiceByNumber.TryAdd(invoice.KsefNumber, invoice);
        }
        catch (Exception ex)
        {
            subjectErrors.Add(ex);
            Console.WriteLine($"WARNING: Query failed for subjectType={subjectType}: {ex.Message}");
        }
    }

    if (!anySuccess && subjectErrors.Count > 0)
        throw subjectErrors[0];

    var invoices = invoiceByNumber.Values.ToList();
    Console.WriteLine($"Fetched {invoices.Count} unique invoice metadata rows");

    var sales = invoices.Where(i => string.Equals(i.Seller?.Nip, company.Nip, StringComparison.Ordinal)).ToList();
    var purchases = invoices.Where(i => string.Equals(i.Buyer?.Identifier?.Value, company.Nip, StringComparison.Ordinal)).ToList();

    await sheets.EnsureSheetsExistAndHeadersAsync(
        cfg.SpreadsheetId,
        (company.SalesTabName, true),
        (company.PurchasesTabName, false));

    var existingSales = await sheets.GetExistingKeysAsync(cfg.SpreadsheetId, company.SalesTabName);
    var existingPurchases = await sheets.GetExistingKeysAsync(cfg.SpreadsheetId, company.PurchasesTabName);

    var newSales = sales.Where(i => !existingSales.Contains(i.KsefNumber)).ToList();
    var newPurchases = purchases.Where(i => !existingPurchases.Contains(i.KsefNumber)).ToList();

    await sheets.AppendInvoicesAsync(cfg.SpreadsheetId, company.SalesTabName, newSales, true);
    await sheets.AppendInvoicesAsync(cfg.SpreadsheetId, company.PurchasesTabName, newPurchases, false);

    Console.WriteLine($"Append sales={newSales.Count}, purchases={newPurchases.Count}");
}

Console.WriteLine("\nDone.");
