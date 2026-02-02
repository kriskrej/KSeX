using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace KsefSheetsSync;

public sealed class GoogleSheetsWriter
{
    private readonly SheetsService _service;

    private static readonly string[] Headers =
    {
        "ksefNumber", "invoiceNumber", "issueDate", "invoicingDate", "acquisitionDate", "permanentStorageDate",
        "sellerNip", "sellerName", "buyerIdType", "buyerIdValue", "buyerName",
        "netAmount", "vatAmount", "grossAmount", "currency", "invoicingMode", "invoiceType",
        "hasAttachment", "isSelfInvoicing", "invoiceHash"
    };

    public GoogleSheetsWriter(GoogleConfig cfg)
    {
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(cfg.ServiceAccountJsonBase64));
        var cred = GoogleCredential.FromJson(json).CreateScoped(SheetsService.Scope.Spreadsheets);
        _service = new SheetsService(new BaseClientService.Initializer { HttpClientInitializer = cred, ApplicationName = cfg.ApplicationName });
    }

    public async Task EnsureSheetsExistAndHeadersAsync(string spreadsheetId, params string[] sheetNames)
    {
        var ssReq = _service.Spreadsheets.Get(spreadsheetId);
        ssReq.Fields = "sheets(properties(title,sheetId))";
        var ss = await ssReq.ExecuteAsync();
        var existing = ss.Sheets?.Select(s => s.Properties?.Title).Where(t => !string.IsNullOrWhiteSpace(t)).ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);

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

        foreach (var name in sheetNames.Distinct(StringComparer.Ordinal))
        {
            await EnsureHeaderAsync(spreadsheetId, name);
        }
    }

    private async Task EnsureHeaderAsync(string spreadsheetId, string sheetName)
    {
        var range = $"{sheetName}!A1:T1";
        var get = await _service.Spreadsheets.Values.Get(spreadsheetId, range).ExecuteAsync();
        var hasHeader = get.Values is { Count: > 0 } && get.Values[0].Count >= 1 && !string.IsNullOrWhiteSpace(get.Values[0][0]?.ToString());
        if (hasHeader) return;

        var vr = new ValueRange { Values = new List<IList<object>> { Headers.Cast<object>().ToList() } };
        var upd = _service.Spreadsheets.Values.Update(vr, spreadsheetId, range);
        upd.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
        await upd.ExecuteAsync();
    }

    public async Task<HashSet<string>> GetExistingKeysAsync(string spreadsheetId, string sheetName)
    {
        var range = $"{sheetName}!A2:A";
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

    public async Task AppendInvoicesAsync(string spreadsheetId, string sheetName, List<KsefInvoiceMetadata> invoices)
    {
        if (invoices.Count == 0) return;

        var values = new List<IList<object>>();
        foreach (var i in invoices)
        {
            values.Add(new List<object>
            {
                i.KsefNumber,
                i.InvoiceNumber ?? "",
                i.IssueDate ?? "",
                i.InvoicingDate?.ToString("O") ?? "",
                i.AcquisitionDate?.ToString("O") ?? "",
                i.PermanentStorageDate?.ToString("O") ?? "",
                i.Seller?.Nip ?? "",
                i.Seller?.Name ?? "",
                i.Buyer?.Identifier?.Type ?? "",
                i.Buyer?.Identifier?.Value ?? "",
                i.Buyer?.Name ?? "",
                i.NetAmount ?? 0,
                i.VatAmount ?? 0,
                i.GrossAmount ?? 0,
                i.Currency ?? "",
                i.InvoicingMode ?? "",
                i.InvoiceType ?? "",
                i.HasAttachment ?? false,
                i.IsSelfInvoicing ?? false,
                i.InvoiceHash ?? ""
            });
        }

        var vr = new ValueRange { Values = values };
        var append = _service.Spreadsheets.Values.Append(vr, spreadsheetId, $"{sheetName}!A:T");
        append.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
        append.InsertDataOption = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
        await append.ExecuteAsync();
    }
}
