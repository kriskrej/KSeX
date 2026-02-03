using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace KSeX;

public static class InvoiceXmlParser
{
    private static readonly Regex MultiWhitespace = new(@"\s+", RegexOptions.Compiled);

    public static string BuildLineItems(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return "";

        var doc = XDocument.Parse(xml);
        var rows = doc.Descendants().Where(e => e.Name.LocalName == "FaWiersz");

        var items = new List<string>();
        foreach (var row in rows)
        {
            var name = NormalizeWhitespace(GetValue(row, "P_7"));
            var quantity = NormalizeQuantity(GetValue(row, "P_8B"));

            var item = FormatItem(quantity, name);
            if (item.Length > 0)
                items.Add(item);
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

        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
            return value.ToString("0.####", CultureInfo.InvariantCulture);

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
}
