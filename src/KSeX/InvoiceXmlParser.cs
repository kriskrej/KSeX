using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace KSeX;

public static class InvoiceXmlParser
{
    private static readonly Regex MultiWhitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly CultureInfo PolishCulture = new("pl-PL");

    public static string BuildLineItems(string xml, string? currencyCode = null)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return "";

        var currencySuffix = ResolveCurrencySuffix(currencyCode);

        var doc = XDocument.Parse(xml);
        var rows = doc.Descendants().Where(e => e.Name.LocalName == "FaWiersz");

        var parsedItems = new List<LineItem>();
        foreach (var row in rows)
        {
            var name = NormalizeWhitespace(GetValue(row, "P_7"));
            var quantityRaw = GetValue(row, "P_8B");
            var quantityText = NormalizeQuantity(quantityRaw);
            var quantityValue = ParseDecimal(quantityRaw);

            var unitNet = ParseDecimal(GetValue(row, "P_9A"));
            var totalNet = ParseDecimal(GetValue(row, "P_9B"));
            var totalGross = ParseDecimal(GetValue(row, "P_11A"));

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(quantityText) &&
                unitNet == null && totalNet == null && totalGross == null)
            {
                continue;
            }

            parsedItems.Add(new LineItem(name, quantityText, quantityValue, unitNet, totalNet, totalGross));
        }

        var rendered = parsedItems
            .Select(item => (Item: item, Text: FormatItem(item.QuantityText, item.Name)))
            .Where(item => item.Text.Length > 0)
            .ToList();

        var includeCosts = rendered.Count > 1;
        var items = new List<string>();
        foreach (var item in rendered)
        {
            var text = item.Text;
            if (includeCosts)
            {
                var cost = FormatCost(item.Item, currencySuffix);
                if (cost.Length > 0)
                    text = $"{text} ({cost})";
            }

            items.Add(text);
        }

        return string.Join("\n", items);
    }

    private static string GetValue(XElement parent, string localName)
    {
        var element = parent.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);
        return element?.Value.Trim() ?? "";
    }

    private static string NormalizeQuantity(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var parsed = ParseDecimal(raw);
        if (parsed.HasValue)
            return parsed.Value.ToString("0.####", CultureInfo.InvariantCulture);

        return raw.Trim();
    }

    private static string NormalizeWhitespace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return MultiWhitespace.Replace(value.Trim(), " ");
    }

    private static string FormatItem(string quantity, string name)
    {
        if (string.IsNullOrWhiteSpace(quantity) && string.IsNullOrWhiteSpace(name))
            return "";

        if (!string.IsNullOrWhiteSpace(name) &&
            decimal.TryParse(quantity, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) &&
            value == 1m)
        {
            quantity = "";
        }

        if (string.IsNullOrWhiteSpace(quantity))
            return name;

        if (string.IsNullOrWhiteSpace(name))
            return $"{quantity}x";

        return $"{quantity}x{name}";
    }

    private static string FormatCost(LineItem item, string currencySuffix)
    {
        var qty = item.Quantity;
        var useNet = item.UnitNet != null || item.TotalNet != null;
        var unit = useNet ? item.UnitNet : null;
        var total = useNet ? item.TotalNet : item.TotalGross;

        if (!qty.HasValue || qty.Value <= 0)
        {
            var amount = total ?? unit;
            return amount.HasValue ? $"{FormatAmount(amount.Value)}{currencySuffix}" : "";
        }

        if (qty.Value == 1m)
        {
            var amount = total ?? unit;
            return amount.HasValue ? $"{FormatAmount(amount.Value)}{currencySuffix}" : "";
        }

        if (!unit.HasValue && total.HasValue)
            unit = total.Value / qty.Value;

        if (!total.HasValue && unit.HasValue)
            total = unit.Value * qty.Value;

        if (!unit.HasValue || !total.HasValue)
        {
            var amount = total ?? unit;
            return amount.HasValue ? $"{FormatAmount(amount.Value)}{currencySuffix}" : "";
        }

        return $"{FormatAmount(qty.Value)}*{FormatAmount(unit.Value)}={FormatAmount(total.Value)}{currencySuffix}";
    }

    private static decimal? ParseDecimal(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            return value;

        if (decimal.TryParse(raw, NumberStyles.Any, PolishCulture, out value))
            return value;

        return null;
    }

    private static string FormatAmount(decimal value)
        => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string ResolveCurrencySuffix(string? currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
            return "zł";

        var code = currencyCode.Trim().ToUpperInvariant();
        return code == "PLN" ? "zł" : code;
    }

    private sealed record LineItem(
        string Name,
        string QuantityText,
        decimal? Quantity,
        decimal? UnitNet,
        decimal? TotalNet,
        decimal? TotalGross);
}
