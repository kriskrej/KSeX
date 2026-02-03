using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace KSeX;

public sealed class GoogleSheetsWriter
{
    private readonly SheetsService _service;

    private static readonly string[] PurchaseHeaders =
    {
        "typ", "id", "nr faktury", "zaKSEFowane", "sprzedawca", "NIP", "netto", "brutto", "waluta", "pozycje"
    };

    private static readonly string[] SalesHeaders =
    {
        "typ", "id", "nr faktury", "zaKSEFowane", "nabywca", "NIP", "netto", "brutto", "waluta", "pozycje"
    };

    private const int IdColumnIndex = 2;

    public GoogleSheetsWriter(GoogleConfig cfg)
    {
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(cfg.ServiceAccountJsonBase64));
        var cred = CredentialFactory.FromJson<ServiceAccountCredential>(json)
            .ToGoogleCredential()
            .CreateScoped(SheetsService.Scope.Spreadsheets);
        _service = new SheetsService(new BaseClientService.Initializer { HttpClientInitializer = cred, ApplicationName = cfg.ApplicationName });
    }

    public async Task EnsureSheetsExistAndHeadersAsync(string spreadsheetId, params (string SheetName, bool IsSales)[] sheets)
    {
        var distinctSheets = sheets
            .Where(s => !string.IsNullOrWhiteSpace(s.SheetName))
            .GroupBy(s => s.SheetName, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        var sheetNames = distinctSheets.Select(s => s.SheetName).ToList();

        var ssReq = _service.Spreadsheets.Get(spreadsheetId);
        ssReq.Fields = "sheets(properties(title,sheetId))";
        var ss = await ssReq.ExecuteAsync();
        var existing = ss.Sheets?
            .Select(s => s.Properties?.Title)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)
            .ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);

        var missing = sheetNames.Where(n => !existing.Contains(n)).Distinct(StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
        {
            var batch = new BatchUpdateSpreadsheetRequest { Requests = new List<Request>() };
            foreach (var name in missing)
            {
                batch.Requests.Add(new Request { AddSheet = new AddSheetRequest { Properties = new SheetProperties { Title = name } } });
            }
            await _service.Spreadsheets.BatchUpdate(batch, spreadsheetId).ExecuteAsync();
        }

        foreach (var sheet in distinctSheets)
        {
            await EnsureHeaderAsync(spreadsheetId, sheet.SheetName, sheet.IsSales);
        }
    }

    private async Task EnsureHeaderAsync(string spreadsheetId, string sheetName, bool isSales)
    {
        var headers = GetHeaders(isSales);
        var range = $"{sheetName}!A1:{GetColumnLetter(headers.Length)}1";
        var get = await _service.Spreadsheets.Values.Get(spreadsheetId, range).ExecuteAsync();
        if (HeaderMatches(get.Values, headers)) return;

        var vr = new ValueRange { Values = new List<IList<object>> { headers.Cast<object>().ToList() } };
        var upd = _service.Spreadsheets.Values.Update(vr, spreadsheetId, range);
        upd.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
        await upd.ExecuteAsync();
    }

    public async Task<HashSet<string>> GetExistingKeysAsync(string spreadsheetId, string sheetName)
    {
        var column = GetColumnLetter(IdColumnIndex);
        var range = $"{sheetName}!{column}2:{column}";
        var resp = await _service.Spreadsheets.Values.Get(spreadsheetId, range).ExecuteAsync();
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (resp.Values == null) return set;
        foreach (var row in resp.Values)
        {
            if (row.Count == 0) continue;
            var v = row[0]?.ToString();
            if (!string.IsNullOrWhiteSpace(v)) set.Add(v);
        }
        return set;
    }

    public async Task AppendInvoicesAsync(
        string spreadsheetId,
        string sheetName,
        List<KSeXInvoiceMetadata> invoices,
        bool isSales,
        IReadOnlyDictionary<string, string>? lineItemsByKsefNumber = null)
    {
        if (invoices.Count == 0) return;

        var headers = GetHeaders(isSales);
        var values = new List<IList<object>>();
        foreach (var i in invoices)
        {
            var partnerName = NormalizeCompanyName(isSales ? i.Buyer?.Name : i.Seller?.Name);
            var partnerNip = isSales ? i.Buyer?.Identifier?.Value : i.Seller?.Nip;
            var lineItems = lineItemsByKsefNumber != null && lineItemsByKsefNumber.TryGetValue(i.KsefNumber, out var items)
                ? items
                : "";

            values.Add(new List<object>
            {
                i.InvoiceType ?? "",
                i.KsefNumber,
                i.InvoiceNumber ?? "",
                i.AcquisitionDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "",
                partnerName ?? "",
                partnerNip ?? "",
                i.NetAmount ?? 0,
                i.GrossAmount ?? 0,
                i.Currency ?? "",
                lineItems
            });
        }

        var vr = new ValueRange { Values = values };
        var append = _service.Spreadsheets.Values.Append(vr, spreadsheetId, $"{sheetName}!A:{GetColumnLetter(headers.Length)}");
        append.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
        append.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
        await append.ExecuteAsync();
    }

    private static string[] GetHeaders(bool isSales)
        => isSales ? SalesHeaders : PurchaseHeaders;

    private static bool HeaderMatches(IList<IList<object>>? values, string[] headers)
    {
        if (values == null || values.Count == 0) return false;
        var row = values[0];
        if (row.Count < headers.Length) return false;
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = row[i]?.ToString() ?? "";
            if (!string.Equals(cell, headers[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static string GetColumnLetter(int columnIndex)
    {
        if (columnIndex <= 0)
            throw new ArgumentOutOfRangeException(nameof(columnIndex), "Column index must be positive.");

        var letters = new StringBuilder();
        var index = columnIndex;
        while (index > 0)
        {
            index--;
            letters.Insert(0, (char)('A' + (index % 26)));
            index /= 26;
        }

        return letters.ToString();
    }

    private static string NormalizeCompanyName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        var normalized = name.Replace(
            "spółka z ograniczoną odpowiedzialnością",
            "sp. z o.o.",
            StringComparison.OrdinalIgnoreCase);

        normalized = normalized.Replace(
            "spolka z ograniczona odpowiedzialnoscia",
            "sp. z o.o.",
            StringComparison.OrdinalIgnoreCase);

        normalized = normalized.Replace(
            "spółka akcyjna",
            "SA",
            StringComparison.OrdinalIgnoreCase);

        normalized = normalized.Replace(
            "spolka akcyjna",
            "SA",
            StringComparison.OrdinalIgnoreCase);

        return normalized;
    }
}
