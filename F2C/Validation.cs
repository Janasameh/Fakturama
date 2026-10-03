namespace F2C;

/// Deterministic checks on extracted data, run BEFORE touching the UI.
public static class Validation
{
    public static readonly HashSet<string> KnownPaymentMethods = ["Bank Transfer", "Credit Card", "SEPA Direct Debit"];

    public static List<string> Problems(Order o)
    {
        var p = new List<string>();
        if (string.IsNullOrEmpty(o.ExternalRef)) p.Add("missing field: ExternalRef");
        if (string.IsNullOrEmpty(o.Company)) p.Add("missing field: Company");
        if (string.IsNullOrEmpty(o.LastName)) p.Add("missing field: LastName");
        if (o.Items.Count == 0) p.Add("no items extracted");
        if (!KnownPaymentMethods.Contains(o.PaymentMethod)) p.Add($"unsupported payment method: '{o.PaymentMethod}'");
        if (o.Paid && o.PaymentDate is null) p.Add("status is PAID but payment date is missing");
        for (int n = 0; n < o.Items.Count; n++)
        {
            var i = o.Items[n];
            if (string.IsNullOrEmpty(i.Sku)) p.Add($"item {n + 1}: missing SKU");
            if (i.LineNet() != Money.Q2(i.SourceLineTotal))
                p.Add($"item {n + 1} ({i.Sku}): computed {i.LineNet()} != source {i.SourceLineTotal}");
        }
        if (o.NetTotal() != Money.Q2(o.SourceNet)) p.Add($"net total: computed {o.NetTotal()} != source {o.SourceNet}");
        if (o.VatTotal() != Money.Q2(o.SourceVat)) p.Add($"VAT total: computed {o.VatTotal()} != source {o.SourceVat}");
        if (o.GrossTotal() != Money.Q2(o.SourceGross)) p.Add($"gross total: computed {o.GrossTotal()} != source {o.SourceGross}");
        return p;
    }

    public static void Validate(Order o)
    {
        var p = Problems(o);
        if (p.Count > 0) throw new ValidationException(string.Join("; ", p));
    }
}
