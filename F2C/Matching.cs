using System.Text.RegularExpressions;
namespace F2C;

public static class Matching
{
    public static string Norm(string? s) =>
        string.Join(" ", (s ?? "").ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string HeaderKey(string? h) => Regex.Replace((h ?? "").ToLowerInvariant(), @"\W+", "_").Trim('_');

    public static string Get(Dictionary<string, string> r, string k) => r.TryGetValue(k, out var v) ? v : "";

    public static List<Dictionary<string, string>> DebtorMatches(
        List<Dictionary<string, string>> rows, Order o, string zip, string city) =>
        rows.Where(r => Norm(Get(r, "company")) == Norm(o.Company)
                     && Norm(Get(r, "first_name")) == Norm(o.FirstName)
                     && Norm(Get(r, "name")) == Norm(o.LastName)
                     && Norm(Get(r, "zip")) == Norm(zip)
                     && Norm(Get(r, "city")) == Norm(city)).ToList();

    public static List<Dictionary<string, string>> ProductMatches(
        List<Dictionary<string, string>> rows, string sku) =>
        rows.Where(r => Norm(Get(r, "item_no")) == Norm(sku)).ToList();

    public static T? PickOne<T>(List<T> matches, string what) where T : class
    {
        if (matches.Count > 1) throw new ManualReviewException($"{matches.Count} conflicting exact matches for {what}");
        return matches.Count == 1 ? matches[0] : null;
    }
}
