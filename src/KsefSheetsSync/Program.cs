using KsefSheetsSync;

var cfg = AppConfig.LoadFromEnvironment();
Console.WriteLine($"KSeX → Google Sheets sync | baseUrl={cfg.KsefBaseUrl} | lookbackDays={cfg.LookbackDays}");

using var http = new HttpClient { BaseAddress = new Uri(cfg.KsefBaseUrl.TrimEnd('/') + "/") };
var ksef = new KsefApiClient(http);
var sheets = new GoogleSheetsWriter(cfg.Google);

const string SalesSheetName = "Sprzedaż";
const string PurchasesSheetName = "Kupno";
const string SalesSubjectType = "Subject1";
const string PurchasesSubjectType = "Subject2";

var now = DateTimeOffset.UtcNow;
var from = now.AddDays(-cfg.LookbackDays);

foreach (var company in cfg.Companies)
{
    Console.WriteLine($"\n== {company.Name} ({company.Nip}) ==");
    var spreadsheetId = company.SpreadsheetId;

    var auth = await ksef.GetAccessTokenAsync(new KsefAuthConfig
    {
        ContextNip = company.Nip,
        KsefToken = company.KsefToken,
        RefreshToken = company.RefreshToken
    });

    Console.WriteLine($"Access token validUntil={auth.ValidUntil:O}");

    var subjectErrors = new List<Exception>();
    var anySuccess = false;

    async Task<List<KsefInvoiceMetadata>> QuerySubjectAsync(string subjectType)
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
            return Deduplicate(rows);
        }
        catch (Exception ex)
        {
            subjectErrors.Add(ex);
            Console.WriteLine($"WARNING: Query failed for subjectType={subjectType}: {ex.Message}");
            return new List<KsefInvoiceMetadata>();
        }
    }

    var salesRows = await QuerySubjectAsync(SalesSubjectType);
    var purchasesRows = await QuerySubjectAsync(PurchasesSubjectType);

    if (!anySuccess && subjectErrors.Count > 0)
        throw subjectErrors[0];

    var sales = salesRows.Where(i => string.Equals(i.Seller?.Nip, company.Nip, StringComparison.Ordinal)).ToList();
    var purchases = purchasesRows.Where(i => string.Equals(i.Buyer?.Identifier?.Value, company.Nip, StringComparison.Ordinal)).ToList();

    await sheets.EnsureSheetsExistAndHeadersAsync(
        spreadsheetId,
        (SalesSheetName, true),
        (PurchasesSheetName, false));

    var existingSales = await sheets.GetExistingKeysAsync(spreadsheetId, SalesSheetName);
    var existingPurchases = await sheets.GetExistingKeysAsync(spreadsheetId, PurchasesSheetName);

    var newSales = sales.Where(i => !existingSales.Contains(i.KsefNumber)).ToList();
    var newPurchases = purchases.Where(i => !existingPurchases.Contains(i.KsefNumber)).ToList();

    var newInvoices = newSales.Concat(newPurchases).ToList();
    var lineItemsByKsef = newInvoices.Count == 0
        ? new Dictionary<string, string>(StringComparer.Ordinal)
        : await FetchLineItemsAsync(newInvoices);

    await sheets.AppendInvoicesAsync(spreadsheetId, SalesSheetName, newSales, true, lineItemsByKsef);
    await sheets.AppendInvoicesAsync(spreadsheetId, PurchasesSheetName, newPurchases, false, lineItemsByKsef);

    Console.WriteLine($"Append sales={newSales.Count}, purchases={newPurchases.Count}");

    async Task<Dictionary<string, string>> FetchLineItemsAsync(List<KsefInvoiceMetadata> invoices)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var invoice in invoices)
        {
            try
            {
                var xml = await ksef.GetInvoiceXmlAsync(auth.AccessToken, invoice.KsefNumber);
                var items = InvoiceXmlParser.BuildLineItems(xml);
                map[invoice.KsefNumber] = items;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARNING: Failed to fetch XML for {invoice.KsefNumber}: {ex.Message}");
            }
        }

        return map;
    }
}

Console.WriteLine("\nDone.");

static List<KsefInvoiceMetadata> Deduplicate(List<KsefInvoiceMetadata> rows)
{
    var dict = new Dictionary<string, KsefInvoiceMetadata>(StringComparer.Ordinal);
    foreach (var row in rows)
        dict.TryAdd(row.KsefNumber, row);
    return dict.Values.ToList();
}
