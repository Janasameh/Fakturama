using System.Globalization;
namespace F2C;

public static class Money
{
    public static decimal Q2(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);
    public static string Fmt2(decimal x) => x.ToString("0.00", CultureInfo.InvariantCulture);
    public static string Num(decimal x) => x.ToString("0.##", CultureInfo.InvariantCulture);
}

public record Address(string Name, string Street, string Zip, string City, string Country);

public record Item(string Sku, string Description, decimal Qty, string Unit, decimal UnitNet,
                   decimal DiscountPct, decimal VatPct, decimal SourceLineTotal)
{
    public decimal LineNet() => Money.Q2(Qty * UnitNet * (1 - DiscountPct / 100m));
    public decimal ProductGrossPrice() => Money.Q2(UnitNet * (1 + VatPct / 100m));
}

public record Order(
    string ExternalRef, DateOnly OrderDate, string CustomerId, string Currency,
    string Company, string FirstName, string LastName, string Alias, string Email, string Phone,
    Address Billing, Address Delivery, string PaymentMethod, bool Paid, DateOnly? PaymentDate,
    IReadOnlyList<Item> Items, decimal SourceNet, decimal SourceVat, decimal SourceGross)
{
    public decimal NetTotal() => Money.Q2(Items.Sum(i => i.LineNet()));
    public decimal VatTotal() =>
        Money.Q2(Items.GroupBy(i => i.VatPct).Sum(g => g.Sum(i => i.LineNet()) * g.Key / 100m));
    public decimal GrossTotal() => NetTotal() + VatTotal();
    public bool SameAddress() =>
        (Billing.Street, Billing.Zip, Billing.City, Billing.Country) ==
        (Delivery.Street, Delivery.Zip, Delivery.City, Delivery.Country);
}
