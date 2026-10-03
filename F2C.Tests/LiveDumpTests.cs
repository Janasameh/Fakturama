using F2C;
using FlaUI.Core.Definitions;
using Xunit;

public class LiveDumpTests
{
    [Fact(Skip = "Live diagnostic tool")]
    public void DumpUiTree()
    {
        using var ui = new Ui();
        ui.Dump("uia_dump.txt");
    }

    [Fact(Skip = "Live diagnostic tool")]
    public void PickContactDiagnostic()
    {
        using var ui = new Ui();
        var sb = new System.Text.StringBuilder();

        var descendants = ui.Main.FindAllDescendants().Where(e => !e.IsOffscreen).ToList();
        sb.AppendLine($"Total visible descendants in Main: {descendants.Count}");
        foreach (var el in descendants)
        {
            string name = "", help = "", id = "", type = el.ControlType.ToString();
            try { name = el.Name ?? ""; } catch { }
            try { help = el.HelpText ?? ""; } catch { }
            try { id = el.AutomationId ?? ""; } catch { }
            if (name.Length > 0 || help.Length > 0 || id.Length > 0 || type == "Button" || type == "Image" || type == "ToolBar")
            {
                sb.AppendLine($"  {type,-10} | Name='{name}' | Help='{help}' | Id='{id}' | Bounds={el.BoundingRectangle}");
            }
        }
        System.IO.File.WriteAllText("pick_contact_diag.txt", sb.ToString());
    }
}
