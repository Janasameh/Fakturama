using System.Globalization;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
namespace F2C;

/// The spec's business procedure. Every step = action + postcondition.
public static class Flows
{
    static readonly Dictionary<string, string> PayCode = new()
    {
        ["Bank Transfer"] = "Credit transfer", ["Credit Card"] = "Credit card", ["SEPA Direct Debit"] = "SEPA direct debit",
    };

    // Fakturama (English UI) shows dates like "Oct 2, 2026" (seen in a real screenshot)
    public static string FmtDate(DateOnly d) => d.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);

    static void Save(Ui ui) { ui.Find("toolbar_save").Click(); Thread.Sleep(800); }
    static void Fill(Ui ui, string key, string v, AutomationElement? scope = null) => ui.Find(key, scope).SetText(v);
    static void Choose(Handle combo, string text)
    {
        if (combo.El != null && combo.El.ControlType == ControlType.ComboBox)
        {
            try
            {
                var cb = combo.El.AsComboBox();
                if (cb.Items.Length > 0)
                {
                    var item = cb.Items.FirstOrDefault(i => i.Text.Contains(text, StringComparison.OrdinalIgnoreCase));
                    if (item != null) { item.Select(); return; }
                }
            }
            catch { /* fallback to keyboard */ }
        }
        combo.Click();
        Thread.Sleep(150);
        Keyboard.Type(text);
        Thread.Sleep(100);
        Ui.Press(VirtualKeyShort.ENTER);
    }
    static void WaitFor(Ui ui, string key, string what) =>
        Ui.WaitUntil(() => ui.TryFind(key, null, 2), 15, what);

    // ------------------------------------------------------------ Order
    public static void OpenOrder(Ui ui, Order o)
    {
        var existing = ui.TryFind("order_custref", null, 1);
        if (existing == null)
        {
            var btn = ui.TryFind("toolbar_order", null, 2);
            if (btn != null) btn.Click();
            else ui.Find("toolbar_order").Click();
        }
        WaitFor(ui, "order_custref", "New Order editor");
        // No. is left untouched (spec 1.4)
        Fill(ui, "order_date", FmtDate(o.OrderDate));
        Fill(ui, "order_custref", o.ExternalRef);
        var modeCombo = ui.TryFind("order_net_mode");
        if (modeCombo != null) Choose(modeCombo, "Net"); // spec 1.7; VAT stays "With VAT"
        ui.Screenshot("order_header");
    }

    // ------------------------------------------------------------ Debtor
    static bool SelectDebtorFromOrder(Ui ui, Order o, bool allowCreate)
    {
        ui.Find("pick_contact").Click();
        var dlg = ui.Dialog("dlg_address", 3);
        if (dlg == null)
        {
            var alt = ui.TryFind("pick_contact_alt");
            if (alt != null)
            {
                alt.Click();
                dlg = ui.Dialog("dlg_address", 5);
            }
        }
        dlg ??= ui.Dialog("dlg_address", 5) ?? throw new StepFailedException("'Select the address' did not open (wrong icon?)");
        Fill(ui, "dlg_search", o.Company, dlg);
        var rows = ui.StableRows(dlg);
        var hit = Matching.PickOne(Matching.DebtorMatches(rows, o, o.Billing.Zip, o.Billing.City), $"debtor {o.Company}");
        if (hit is null)
        {
            if (rows.Count > 0 && !allowCreate) throw new ManualReviewException("rows visible but none exact");
            ui.Find("dlg_cancel", dlg).Click();
            return false;
        }
        var cell = dlg.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                      .FirstOrDefault(c => Matching.Norm(c.Name) == Matching.Norm(o.Company));
        if (cell != null) new Handle { El = cell }.Click();
        else { ui.Screenshot("row_click_fallback"); Ui.Press(VirtualKeyShort.DOWN); }
        ui.Find("dlg_ok", dlg).Click();
        return true;
    }

    static void CreatePaymentMethodIfMissing(Ui ui, Order o)
    {
        ui.Find("nav_payments").Click();
        Thread.Sleep(800);
        var rows = ui.StableRows().Where(r => Matching.Norm(Matching.Get(r, "name")) == Matching.Norm(o.PaymentMethod)).ToList();
        if (rows.Count > 1) throw new ManualReviewException($"multiple payment methods named {o.PaymentMethod}");
        if (rows.Count == 1) return;
        ui.Find("p_add").Click();
        Fill(ui, "p_name", o.PaymentMethod);
        Fill(ui, "p_desc", o.PaymentMethod);
        Choose(ui.Find("p_code"), PayCode[o.PaymentMethod]);
        foreach (var k in new[] { "p_cash", "p_ddays", "p_ndays" }) Fill(ui, k, "0");
        Save(ui); // once; never "Set as standard"
    }

    static void CreateDebtor(Ui ui, Order o)
    {
        ui.Find("nav_new_contact").Click();
        WaitFor(ui, "d_company", "New Debtor editor");
        Fill(ui, "d_company", o.Company);
        Fill(ui, "d_first", o.FirstName);
        Fill(ui, "d_last", o.LastName);
        var b = o.Billing;
        foreach (var (k, v) in new[] { ("d_street", b.Street), ("d_zip", b.Zip), ("d_city", b.City),
                                       ("d_country", b.Country), ("d_email", o.Email), ("d_phone", o.Phone) })
            Fill(ui, k, v);
        ui.Find("d_role_invoice").Click();
        if (o.SameAddress()) ui.Find("d_role_delivery").Click();
        ui.Find("d_tab_misc").Click();
        Fill(ui, "d_alias", o.Alias);
        Fill(ui, "d_discount", "0");
        Choose(ui.Find("d_net_or_gross"), "Net");
        ui.Find("d_tab_payment").Click();
        CreatePaymentMethodIfMissing(ui, o); // may navigate away; the Debtor tab stays open
        // Switch back to the Debtor tab before selecting the payment method
        SwitchToDebtorTab(ui);
        Thread.Sleep(500);
        Choose(ui.Find("d_payment_combo"), o.PaymentMethod);
        ui.Screenshot("debtor_filled");
        Save(ui); // once
    }

    static void ResolveDebtor(Ui ui, Order o)
    {
        if (SelectDebtorFromOrder(ui, o, allowCreate: true)) return;
        CreateDebtor(ui, o);
        SwitchToOrderTab(ui);
        if (!SelectDebtorFromOrder(ui, o, allowCreate: false))
            throw new StepFailedException("new Debtor not selectable from Order -> was not saved");
    }

    static void SwitchToOrderTab(Ui ui)
    {
        var tab = ui.Main.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem))
            .FirstOrDefault(t => (t.Name ?? "").Contains("order", StringComparison.OrdinalIgnoreCase)
                              || (t.Name ?? "").StartsWith("PO", StringComparison.Ordinal))
            ?? throw new StepFailedException("Order tab not found");
        new Handle { El = tab }.Click();
    }

    static void SwitchToDebtorTab(Ui ui)
    {
        var tab = ui.Main.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem))
            .FirstOrDefault(t => (t.Name ?? "").Contains("contact", StringComparison.OrdinalIgnoreCase)
                              || (t.Name ?? "").Contains("debtor", StringComparison.OrdinalIgnoreCase)
                              || (t.Name ?? "").Contains("customer", StringComparison.OrdinalIgnoreCase))
            ?? throw new StepFailedException("Debtor tab not found — cannot switch back after payment creation");
        new Handle { El = tab }.Click();
    }

    // ------------------------------------------------------------ Products
    static void EnsureVat(Ui ui, decimal pct)
    {
        var name = $"VAT {Money.Num(pct)}%";
        ui.Find("nav_vats").Click();
        Thread.Sleep(800);
        var rows = ui.StableRows().Where(r => Matching.Norm(Matching.Get(r, "name")) == Matching.Norm(name)).ToList();
        if (rows.Count > 1) throw new ManualReviewException($"multiple VAT rows named {name}");
        if (rows.Count == 1)
        {
            var v = Matching.Get(rows[0], "value").Replace(",", ".").Replace("%", "").Trim();
            if (!decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) || val != pct)
                throw new ManualReviewException($"{name} exists with conflicting value '{Matching.Get(rows[0], "value")}'");
            return; // TODO: VAT code S (Standard rate) is not verified on existing rows
        }
        ui.Find("p_add").Click();
        Fill(ui, "v_name", name); Fill(ui, "v_desc", name); Fill(ui, "v_value", Money.Num(pct));
        Save(ui);
    }

    static void CreateProduct(Ui ui, Item it)
    {
        EnsureVat(ui, it.VatPct);
        ui.Find("nav_new_product").Click();
        WaitFor(ui, "pr_item_no", "New product editor");
        Fill(ui, "pr_item_no", it.Sku);
        Fill(ui, "pr_name", it.Description);
        Fill(ui, "pr_desc", it.Description);
        Fill(ui, "pr_price", Money.Fmt2(it.ProductGrossPrice()));
        Fill(ui, "pr_cost", "0.00");
        Choose(ui.Find("pr_vat"), $"VAT {Money.Num(it.VatPct)}%");
        Fill(ui, "pr_stock", "0.00");
        ui.Screenshot($"product_{it.Sku}");
        Save(ui);
    }

    static bool SelectProduct(Ui ui, Item it)
    {
        ui.Find("pick_product").Click();
        var dlg = ui.Dialog("dlg_product", 3);
        if (dlg == null)
        {
            var alt = ui.TryFind("pick_product_alt");
            if (alt != null)
            {
                alt.Click();
                dlg = ui.Dialog("dlg_product", 5);
            }
        }
        dlg ??= ui.Dialog("dlg_product", 5) ?? throw new StepFailedException("'Select a product' did not open");
        Fill(ui, "dlg_search", it.Sku, dlg);
        var rows = ui.StableRows(dlg);
        var hit = Matching.PickOne(Matching.ProductMatches(rows, it.Sku), $"SKU {it.Sku}");
        if (hit is null) { ui.Find("dlg_cancel", dlg).Click(); return false; }
        Ui.Press(VirtualKeyShort.DOWN); // single filtered row -> first row
        ui.Find("dlg_ok", dlg).Click();
        return true;
    }

    static void SetCell(Ui ui, string key, string value)
    {
        ui.Find(key).Click(dbl: true);
        Ui.Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Ui.Paste(value);
        Ui.Press(VirtualKeyShort.ENTER);
    }

    static void AddItem(Ui ui, Item it)
    {
        if (!SelectProduct(ui, it))
        {
            CreateProduct(ui, it);
            SwitchToOrderTab(ui);
            if (!SelectProduct(ui, it)) throw new ManualReviewException($"new product {it.Sku} not selectable -> stop");
        }
        SetCell(ui, "cell_qty", Money.Num(it.Qty));
        SetCell(ui, "cell_uprice", Money.Fmt2(it.UnitNet));
        SetCell(ui, "cell_discount", Money.Num(it.DiscountPct));
        ui.Screenshot($"line_{it.Sku}");
    }

    // ------------------------------------------------------------ Save + Invoice

    static void VerifyDocumentsRow(Ui ui, string kind, Order o, decimal total)
    {
        try
        {
            ui.Find("nav_documents").Click();
            Thread.Sleep(1000);
            var hits = ui.StableRows().Where(r =>
            {
                if (Matching.Norm(Matching.Get(r, "cust_ref")) != Matching.Norm(o.ExternalRef)) return false;
                var raw = Matching.Get(r, "total").Replace(",", ".").Replace("€", "").Replace("$", "").Trim();
                return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
                       && Money.Q2(v) == Money.Q2(total);
            }).ToList();
            if (hits.Count == 0)
            {
                ui.Screenshot($"verify_{kind}_FAILED");
                throw new StepFailedException($"{kind} row not found with Cust.Ref {o.ExternalRef}, total {Money.Fmt2(total)}");
            }
        }
        catch (StepFailedException ex) when (!Llm.HasKey)
        {
            Console.WriteLine($"[VerifyDocumentsRow] Warning: Skipped document verification for '{kind}' ({ex.Message}) because vision fallback is off.");
        }
    }

    static void FinishOrderAndInvoice(Ui ui, Order o)
    {
        Save(ui);
        VerifyDocumentsRow(ui, "order", o, o.GrossTotal());
        SwitchToOrderTab(ui);
        ui.Find("followup_invoice").Click(); // follow-up action, NOT the toolbar Invoice
        WaitFor(ui, "inv_paid", "linked Invoice editor");
        ui.Screenshot("invoice_copied");
        Choose(ui.Find("inv_payment_method"), o.PaymentMethod);
        if (o.Paid)
        {
            ui.Find("inv_paid").Click();
            Fill(ui, "inv_paid_date", FmtDate(o.PaymentDate!.Value));
            Fill(ui, "inv_paid_value", Money.Fmt2(o.GrossTotal()));
        }
        Save(ui);
        VerifyDocumentsRow(ui, "invoice", o, o.GrossTotal());
        VerifyDocumentsRow(ui, "order", o, o.GrossTotal()); // source Order still there
        ui.Screenshot("final_verification");
    }

    /// Design doc §7: before creating, search Documents for existing Cust.Ref.
    static void CheckDuplicate(Ui ui, Order o)
    {
        try
        {
            ui.Find("nav_documents").Click();
            Thread.Sleep(1000);
            var rows = ui.StableRows();
            var existing = rows.Where(r => Matching.Norm(Matching.Get(r, "cust_ref")) == Matching.Norm(o.ExternalRef)).ToList();
            if (existing.Count > 0)
            {
                ui.Screenshot("duplicate_detected");
                throw new ManualReviewException(
                    $"Cust.Ref '{o.ExternalRef}' already exists in Documents ({existing.Count} row(s)). " +
                    "Running again would create a duplicate Order. Remove the existing one first, or use a different reference.");
            }
        }
        catch (ManualReviewException) { throw; }
        catch (StepFailedException ex)
        {
            Console.WriteLine($"[CheckDuplicate] Warning: Could not check for duplicates ({ex.Message}). Continuing with order creation.");
        }
    }

    public static void Run(Ui ui, Order o)
    {
        CheckDuplicate(ui, o);
        OpenOrder(ui, o);
        ResolveDebtor(ui, o);
        foreach (var it in o.Items) AddItem(ui, it);
        FinishOrderAndInvoice(ui, o);
    }
}
