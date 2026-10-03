using System.Globalization;
using System.Text.Json;
namespace F2C;

/// Image -> Order. The LLM does perception; code does normalisation + validation.
public static class Extraction
{
    const string Prompt = """
Extract every value from this sales order image. Return ONLY JSON:
{
 "external_reference": str, "order_date": "YYYY-MM-DD", "customer_id": str, "currency": str,
 "company": str, "contact_first_name": str, "contact_last_name": str, "alias": str,
 "email": str, "phone": str,
 "billing": {"name": str, "street": str, "zip": str, "city": str, "country": str},
 "delivery": {"name": str, "street": str, "zip": str, "city": str, "country": str},
 "payment_method": str, "paid_status": "PAID" or other text, "payment_date": "YYYY-MM-DD" or null,
 "items": [{"sku": str, "description": str, "qty": number, "unit": str,
            "unit_net": number, "discount_pct": number, "vat_pct": number, "line_net": number}],
 "totals": {"net": number, "vat": number, "gross": number}
}
Copy text exactly as printed. Numbers are plain decimals (no currency symbols, no % signs).
Do not compute anything; transcribe.
""";

    static string S(JsonElement e, string k) =>
        e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "").Trim() : "";

    static decimal D(JsonElement e, string k, decimal def = 0)
    {
        if (!e.TryGetProperty(k, out var v) || v.ValueKind == JsonValueKind.Null) return def;
        return v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : decimal.Parse(v.GetString()!, CultureInfo.InvariantCulture);
    }

    static DateOnly? Dt(JsonElement e, string k)
    {
        var s = S(e, k);
        return s == "" ? null : DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    static Address A(JsonElement a) => new(S(a, "name"), S(a, "street"), S(a, "zip"), S(a, "city"), S(a, "country"));

    public static Order FromJson(JsonElement j)
    {
        try
        {
            if (!j.TryGetProperty("items", out var itemsEl))
                throw new ValidationException("LLM response missing 'items' array");
            if (!j.TryGetProperty("totals", out var t))
                throw new ValidationException("LLM response missing 'totals' object");

            var orderDate = Dt(j, "order_date")
                ?? throw new ValidationException("LLM response missing or invalid 'order_date'");

            var items = itemsEl.EnumerateArray().Select(i => new Item(
                S(i, "sku"), S(i, "description"), D(i, "qty"), S(i, "unit"), D(i, "unit_net"),
                D(i, "discount_pct"), D(i, "vat_pct"), D(i, "line_net"))).ToList();

            return new Order(
                S(j, "external_reference"), orderDate, S(j, "customer_id"), S(j, "currency"),
                S(j, "company"), S(j, "contact_first_name"), S(j, "contact_last_name"), S(j, "alias"),
                S(j, "email"), S(j, "phone"),
                j.TryGetProperty("billing", out var b) ? A(b) : new Address("", "", "", "", ""),
                j.TryGetProperty("delivery", out var d) ? A(d) : new Address("", "", "", "", ""),
                S(j, "payment_method"), S(j, "paid_status").ToUpperInvariant() == "PAID", Dt(j, "payment_date"),
                items, D(t, "net"), D(t, "vat"), D(t, "gross"));
        }
        catch (ValidationException) { throw; }
        catch (Exception ex)
        {
            throw new ValidationException($"Failed to parse LLM response: {ex.Message}");
        }
    }

    /// Accepts an image (needs GEMINI_API_KEY) OR a .json file with the already-extracted fields.
    public static Order Extract(string path)
    {
        Order order;
        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            order = FromJson(JsonDocument.Parse(File.ReadAllText(path)).RootElement);
        }
        else
        {
            var media = path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : "image/png";
            order = FromJson(Llm.ParseJson(Llm.AskVision(File.ReadAllBytes(path), media, Prompt)));
        }
        Validation.Validate(order); // throws before any UI action
        return order;
    }
}
