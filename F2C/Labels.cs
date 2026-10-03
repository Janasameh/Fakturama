using FlaUI.Core.Definitions;
namespace F2C;

/// UI targets as DATA. If a label is wrong on your install, fix it HERE.
///   Uia   = regex for the accessible name (Tier 1)
///   Hint  = plain-English description for vision grounding (Tier 2)
///   CType = optional UIA control type
/// NOTE: written from the spec's wording; NOT yet verified on a real install.
public record Target(string Uia, string Hint, ControlType? CType = null);

public static class Labels
{
    const string None = "^$"; // no usable accessible name -> goes straight to vision
    static Target Tg(string uia, string hint, ControlType? c = null) => new(uia, hint, c);

    public static readonly Dictionary<string, Target> Map = new()
    {
        // toolbar / panels
        ["toolbar_order"] = Tg(@"(?i)Create:\s*New\s*Order", "the 'Order' button in the top toolbar"),
        ["toolbar_save"] = Tg(@"(?i)Save.*current|^Save$", "the 'Save' (disk) button in the top toolbar"),
        ["nav_new_contact"] = Tg(@"New Contact", "'New Contact' link in the left 'New' panel"),
        ["nav_new_product"] = Tg(@"New product", "'New product' link in the left 'New' panel"),
        ["nav_documents"] = Tg(@"^Documents$", "'Documents' entry under 'Data' in the left panel"),
        ["nav_vats"] = Tg(@"^VATs$", "'VATs' entry under 'Data' in the left panel"),
        ["nav_payments"] = Tg(@"terms of payment", "'terms of payment' entry under 'Data' in the left panel"),
        // order header
        ["order_date"] = Tg(@"^Date$", "the Date input in the Order header", ControlType.Edit),
        ["order_custref"] = Tg(@"(?i)Cust.*Ref|Customer.*Ref|Reference", "the 'Cust.Ref.' input in the Order header", ControlType.Edit),
        ["order_net_mode"] = Tg(@"Gross|Net", "the document price-mode combo (currently 'Gross'), set it to Net", ControlType.ComboBox),
        ["pick_contact"] = Tg(None, "the UPPER existing-contact icon beside 'Addresses' (NOT the lower green +)"),
        ["pick_contact_alt"] = Tg(None, "alternative contact icon beside Addresses"),
        ["pick_product"] = Tg(None, "the UPPER product-selection icon beside the Items table (NOT the green +)"),
        ["pick_product_alt"] = Tg(None, "alternative product icon beside Items"),
        // selector dialogs
        ["dlg_address"] = Tg(@"(?i)Select.*(address|contact)|Address|Contact|Debtors", "dialog titled 'Select the address'"),
        ["dlg_product"] = Tg(@"(?i)Select.*product|Product|Products|Items", "dialog titled 'Select a product'"),
        ["dlg_search"] = Tg(@"Search", "the Search input at top-right of the dialog", ControlType.Edit),
        ["dlg_ok"] = Tg(@"^OK$", "the OK button of the dialog", ControlType.Button),
        ["dlg_cancel"] = Tg(@"^Cancel$", "the Cancel button of the dialog", ControlType.Button),
        // debtor editor
        ["d_company"] = Tg(@"Company", "Company input", ControlType.Edit),
        ["d_first"] = Tg(@"First Name", "First name input", ControlType.Edit),
        ["d_last"] = Tg(@"Last Name|^Name$", "Last name input", ControlType.Edit),
        ["d_street"] = Tg(@"Street", "Street input in Main address", ControlType.Edit),
        ["d_zip"] = Tg(@"ZIP", "ZIP input in Main address", ControlType.Edit),
        ["d_city"] = Tg(@"City", "City input in Main address", ControlType.Edit),
        ["d_country"] = Tg(@"Country", "Country input in Main address", ControlType.Edit),
        ["d_email"] = Tg(@"E-?Mail", "E-Mail input in Main address", ControlType.Edit),
        ["d_phone"] = Tg(@"Telephone", "Telephone input in Main address", ControlType.Edit),
        ["d_role_invoice"] = Tg(@"Invoice address", "'Invoice address' role checkbox for the Main address", ControlType.CheckBox),
        ["d_role_delivery"] = Tg(@"Delivery address", "'Delivery address' role checkbox", ControlType.CheckBox),
        ["d_tab_misc"] = Tg(@"Miscellaneous", "'Miscellaneous' sub-tab of the Debtor editor"),
        ["d_alias"] = Tg(@"Alias", "Alias name input", ControlType.Edit),
        ["d_discount"] = Tg(@"^Discount$", "Discount input (set to 0)", ControlType.Edit),
        ["d_net_or_gross"] = Tg(@"Net or Gross", "'Net or Gross' combo (set to Net)", ControlType.ComboBox),
        ["d_tab_payment"] = Tg(@"^Payment$", "'Payment' sub-tab of the Debtor editor"),
        ["d_payment_combo"] = Tg(@"Payment", "Payment method combo in the Debtor editor", ControlType.ComboBox),
        // payment method editor
        ["p_add"] = Tg(None, "the green + button at the upper-right of the list"),
        ["p_name"] = Tg(@"^Name$", "Name input", ControlType.Edit),
        ["p_desc"] = Tg(@"Description", "Description input", ControlType.Edit),
        ["p_code"] = Tg(@"payment code|code", "payment-code dropdown", ControlType.ComboBox),
        ["p_cash"] = Tg(@"Cash discount", "Cash discount input", ControlType.Edit),
        ["p_ddays"] = Tg(@"Discount Days", "Discount Days input", ControlType.Edit),
        ["p_ndays"] = Tg(@"Net Days", "Net Days input", ControlType.Edit),
        // VAT editor
        ["v_name"] = Tg(@"^Name$", "VAT Name input", ControlType.Edit),
        ["v_desc"] = Tg(@"Description", "VAT Description input", ControlType.Edit),
        ["v_value"] = Tg(@"^Value$", "VAT Value input", ControlType.Edit),
        // product editor
        ["pr_item_no"] = Tg(@"Item Number", "Item Number input", ControlType.Edit),
        ["pr_name"] = Tg(@"^Name$", "Name input", ControlType.Edit),
        ["pr_desc"] = Tg(@"Description", "Description text area"),
        ["pr_price"] = Tg(@"Price \(gross\)", "Price (gross) input", ControlType.Edit),
        ["pr_cost"] = Tg(@"cost price", "cost price (net) input", ControlType.Edit),
        ["pr_vat"] = Tg(@"^VAT$", "VAT combo", ControlType.ComboBox),
        ["pr_stock"] = Tg(@"Stock", "Stock input", ControlType.Edit),
        // order items grid (custom widget: expect vision)
        ["cell_qty"] = Tg(None, "the Qty. cell of the currently selected item row"),
        ["cell_uprice"] = Tg(None, "the U.Price cell of the currently selected item row"),
        ["cell_discount"] = Tg(None, "the Discount cell of the currently selected item row"),
        // follow-up + invoice
        ["followup_invoice"] = Tg(@"^Invoice$", "the 'Invoice' button inside 'Create a follow-up document' (NOT the top toolbar)"),
        ["inv_payment_method"] = Tg(None, "payment-method combo at the bottom-left of the Invoice"),
        ["inv_paid"] = Tg(@"^paid$", "the 'paid' checkbox at the bottom-left of the Invoice", ControlType.CheckBox),
        ["inv_paid_date"] = Tg(None, "the payment date input next to the paid checkbox"),
        ["inv_paid_value"] = Tg(None, "the 'Value' input next to the paid date"),
    };
}
