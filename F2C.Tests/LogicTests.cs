using System.Text.Json;
using F2C;
using Xunit;

public class LogicTests
{
    const string Good = """
    {"external_reference":"WEB-2026-0714-A17","order_date":"2026-07-14","customer_id":"CUST-1007","currency":"EUR",
     "company":"Northstar Office GmbH","contact_first_name":"Marta","contact_last_name":"Klein",
     "alias":"NORTHSTAR-BERLIN","email":"marta.klein@example.test","phone":"+49 30 5550 1420",
     "billing":{"name":"Northstar Office GmbH","street":"Friedrichstrasse 88","zip":"10117","city":"Berlin","country":"Germany"},
     "delivery":{"name":"Northstar Office Warehouse","street":"Beusselstrasse 44","zip":"10553","city":"Berlin","country":"Germany"},
     "payment_method":"Bank Transfer","paid_status":"PAID","payment_date":"2026-07-18",
     "items":[
       {"sku":"CHR-ERG-01","description":"Ergonomic Desk Chair","qty":2,"unit":"pcs","unit_net":250.00,"discount_pct":10,"vat_pct":19,"line_net":450.00},
       {"sku":"MAT-DESK-02","description":"Anti-Fatigue Desk Mat","qty":3,"unit":"pcs","unit_net":40.00,"discount_pct":0,"vat_pct":19,"line_net":120.00}],
     "totals":{"net":570.00,"vat":108.30,"gross":678.30}}
    """;

    static Order Make(string json) => Extraction.FromJson(JsonDocument.Parse(json).RootElement);

    [Fact] public void SampleOrderValidates()
    {
        var o = Make(Good);
        Validation.Validate(o);
        Assert.Equal(678.30m, o.GrossTotal());
        Assert.True(o.Paid);
        Assert.False(o.SameAddress());
    }

    [Fact] public void ProductMasterPriceIgnoresLineDiscount()
    {
        var o = Make(Good);
        Assert.Equal(297.50m, o.Items[0].ProductGrossPrice());
        Assert.Equal(450.00m, o.Items[0].LineNet());
    }

    [Fact] public void MisreadDigitIsCaught() =>
        Assert.Throws<ValidationException>(() => Validation.Validate(Make(Good.Replace("\"gross\":678.30", "\"gross\":679.30"))));

    [Fact] public void PaidRequiresDate() =>
        Assert.Throws<ValidationException>(() => Validation.Validate(Make(Good.Replace("\"payment_date\":\"2026-07-18\"", "\"payment_date\":null"))));

    [Fact] public void UnknownPaymentMethodRejected() =>
        Assert.Throws<ValidationException>(() => Validation.Validate(Make(Good.Replace("Bank Transfer", "Cheque"))));

    [Fact] public void HeaderKey()
    {
        Assert.Equal("item_no", Matching.HeaderKey("Item No."));
        Assert.Equal("first_name", Matching.HeaderKey("First Name"));
    }

    static Dictionary<string, string> Row(string zip = "10117", string first = "Marta", string company = "Northstar Office GmbH") =>
        new() { ["company"] = company, ["first_name"] = first, ["name"] = "Klein", ["zip"] = zip, ["city"] = "Berlin" };

    [Fact] public void DebtorExactMatchIgnoresCase()
    {
        var o = Make(Good);
        Assert.Single(Matching.DebtorMatches([Row(company: "northstar office gmbh")], o, "10117", "Berlin"));
    }

    [Fact] public void NearMatchIsNotExact()
    {
        var o = Make(Good);
        Assert.Empty(Matching.DebtorMatches([Row(zip: "10118")], o, "10117", "Berlin"));
        Assert.Empty(Matching.DebtorMatches([Row(first: "Maria")], o, "10117", "Berlin"));
    }

    [Fact] public void AmbiguousStopsForReview()
    {
        Assert.Throws<ManualReviewException>(() => Matching.PickOne([Row(), Row()], "debtor"));
        Assert.Null(Matching.PickOne(new List<Dictionary<string, string>>(), "debtor"));
    }

    [Fact] public void ProductSkuExact()
    {
        var rows = new List<Dictionary<string, string>> { new() { ["item_no"] = "CHR-ERG-01" }, new() { ["item_no"] = "CHR-ERG-011" } };
        Assert.Single(Matching.ProductMatches(rows, "chr-erg-01"));
    }

    [Fact] public void DateFormatMatchesFakturama() =>
        Assert.Equal("Oct 2, 2026", Flows.FmtDate(new DateOnly(2026, 10, 2)));

    [Fact] public void MissingExternalRefRejected()
    {
        var json = Good.Replace("\"WEB-2026-0714-A17\"", "\"\"");
        Assert.Contains("ExternalRef", Assert.Throws<ValidationException>(() => Validation.Validate(Make(json))).Message);
    }

    [Fact] public void MissingCompanyRejected()
    {
        var json = Good.Replace("\"Northstar Office GmbH\"", "\"\"");
        Assert.Contains("Company", Assert.Throws<ValidationException>(() => Validation.Validate(Make(json))).Message);
    }

    [Fact] public void MissingLastNameRejected()
    {
        var json = Good.Replace("\"Klein\"", "\"\"");
        Assert.Contains("LastName", Assert.Throws<ValidationException>(() => Validation.Validate(Make(json))).Message);
    }

    [Fact] public void MissingSkuRejected()
    {
        var json = Good.Replace("\"CHR-ERG-01\"", "\"\"");
        Assert.Contains("missing SKU", Assert.Throws<ValidationException>(() => Validation.Validate(Make(json))).Message);
    }

    [Fact] public void NetMismatchCaught() =>
        Assert.Throws<ValidationException>(() => Validation.Validate(Make(Good.Replace("\"net\":570.00", "\"net\":571.00"))));

    [Fact] public void VatMismatchCaught() =>
        Assert.Throws<ValidationException>(() => Validation.Validate(Make(Good.Replace("\"vat\":108.30", "\"vat\":109.30"))));

    [Fact] public void LineTotalMismatchCaught() =>
        Assert.Throws<ValidationException>(() => Validation.Validate(Make(Good.Replace("\"line_net\":450.00", "\"line_net\":451.00"))));

    const string MultiVat = """
    {"external_reference":"REF-X","order_date":"2026-01-01","customer_id":"C1","currency":"EUR",
     "company":"Test GmbH","contact_first_name":"A","contact_last_name":"B",
     "alias":"T","email":"a@b.c","phone":"123",
     "billing":{"name":"Test","street":"S","zip":"1","city":"C","country":"D"},
     "delivery":{"name":"Test","street":"S","zip":"1","city":"C","country":"D"},
     "payment_method":"Bank Transfer","paid_status":"UNPAID","payment_date":null,
     "items":[
       {"sku":"A1","description":"Item A","qty":1,"unit":"pcs","unit_net":100.00,"discount_pct":0,"vat_pct":19,"line_net":100.00},
       {"sku":"A2","description":"Item B","qty":1,"unit":"pcs","unit_net":200.00,"discount_pct":0,"vat_pct":7,"line_net":200.00}],
     "totals":{"net":300.00,"vat":33.00,"gross":333.00}}
    """;

    [Fact] public void MultiVatRatesGroupedCorrectly()
    {
        var o = Make(MultiVat);
        Validation.Validate(o);
        Assert.Equal(300.00m, o.NetTotal());

        Assert.Equal(33.00m, o.VatTotal());
        Assert.Equal(333.00m, o.GrossTotal());
    }

    [Fact] public void MissingItemsThrowsValidation() =>
        Assert.Throws<ValidationException>(() => Extraction.FromJson(
            JsonDocument.Parse("""{"totals":{"net":0,"vat":0,"gross":0},"order_date":"2026-01-01"}""").RootElement));

    [Fact] public void MissingTotalsThrowsValidation() =>
        Assert.Throws<ValidationException>(() => Extraction.FromJson(
            JsonDocument.Parse("""{"items":[],"order_date":"2026-01-01"}""").RootElement));

    [Fact] public void MissingOrderDateThrowsValidation() =>
        Assert.Throws<ValidationException>(() => Extraction.FromJson(
            JsonDocument.Parse("""{"items":[],"totals":{"net":0,"vat":0,"gross":0}}""").RootElement));

    [Fact] public void SameAddressDetected()
    {
        var json = MultiVat;
        var o = Make(json);
        Assert.True(o.SameAddress());
    }

    [Fact] public void UnpaidWithoutDateIsValid()
    {
        var o = Make(MultiVat);
        Assert.False(o.Paid);
        Assert.Null(o.PaymentDate);
        Validation.Validate(o);
    }
}
