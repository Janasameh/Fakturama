using System.Runtime.InteropServices;
using F2C;

// Usage: dotnet run --project F2C -- path\to\order.png [--dry-run]
[DllImport("user32.dll")] static extern bool SetProcessDPIAware();
SetProcessDPIAware(); // so screenshot pixels == click pixels on scaled displays

if (args.Length == 0) { Console.WriteLine("Usage: F2C <order.json|image> [--dry-run] | F2C --dump"); return 1; }

if (args[0] == "--enum")
{
    Console.WriteLine("=== UIA3 ===");
    using (var auto3 = new FlaUI.UIA3.UIA3Automation())
    {
        var desktop3 = auto3.GetDesktop();
        var children3 = desktop3.FindAllChildren();
        Console.WriteLine($"Found {children3.Length} desktop children (UIA3)");
        foreach (var c in children3)
        {
            try
            {
                var name = c.Name ?? "(null)";
                var cls = c.ClassName ?? "(null)";
                var ct = c.ControlType;
                int pid = 0;
                try { pid = c.Properties.ProcessId.ValueOrDefault; } catch { }
                Console.WriteLine($"  PID={pid} Type={ct} Class='{cls}' Name='{name}'");
            }
            catch (Exception ex) { Console.WriteLine($"  [error: {ex.Message}]"); }
        }
    }
    Console.WriteLine("\n=== UIA2 ===");
    using (var auto2 = new FlaUI.UIA2.UIA2Automation())
    {
        var desktop2 = auto2.GetDesktop();
        var children2 = desktop2.FindAllChildren();
        Console.WriteLine($"Found {children2.Length} desktop children (UIA2)");
        foreach (var c in children2)
        {
            try
            {
                var name = c.Name ?? "(null)";
                var cls = c.ClassName ?? "(null)";
                var ct = c.ControlType;
                int pid = 0;
                try { pid = c.Properties.ProcessId.ValueOrDefault; } catch { }
                Console.WriteLine($"  PID={pid} Type={ct} Class='{cls}' Name='{name}'");
            }
            catch (Exception ex) { Console.WriteLine($"  [error: {ex.Message}]"); }
        }
    }
    return 0;
}
if (args[0] == "--dump")
{
    try
    {
        using var dui = new Ui();
        Directory.CreateDirectory("artifacts");
        dui.Dump(Path.Combine("artifacts", "ui_dump.txt"));
        Console.WriteLine(@"Wrote artifacts\ui_dump.txt");
        return 0;
    }
    catch (StepFailedException e) { Console.WriteLine($"FAILED: {e.Message}"); return 4; }
}
if (!File.Exists(args[0]))
{
    Console.WriteLine($"File not found: '{args[0]}'");
    Console.WriteLine("Please provide a valid path to an existing JSON order file or invoice image (e.g., samples\\order.json).");
    return 1;
}

Order order;
try { order = Extraction.Extract(args[0]); }
catch (ValidationException e) { Console.WriteLine($"STOP (validation): {e.Message}"); return 2; }

Console.WriteLine($"Extracted {order.ExternalRef}: {order.Items.Count} items, " +
                  $"net {order.NetTotal()} vat {order.VatTotal()} gross {order.GrossTotal()}");
if (args.Contains("--dry-run")) return 0;

try
{
    using var ui = new Ui();
    Flows.Run(ui, order);
}
catch (ManualReviewException e) { Console.WriteLine($"STOP (manual review needed): {e.Message}"); return 3; }
catch (StepFailedException e) { Console.WriteLine($"FAILED: {e.Message} (see artifacts\\ for screenshots)"); return 4; }

Console.WriteLine("DONE: Order and linked Invoice saved and verified.");
return 0;
