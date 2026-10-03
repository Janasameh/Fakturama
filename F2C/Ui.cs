using System.Drawing;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
namespace F2C;

public sealed class Handle
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    public string Desc = "";
    public AutomationElement? El;
    public Rectangle? Rect;

    public Point Center()
    {
        if (El != null)
        {
            try
            {
                var pt = El.GetClickablePoint();
                if (pt.X > 0 && pt.Y > 0) return pt;
            }
            catch {}
            var r = El.BoundingRectangle;
            if (r.Width > 0 && r.Height > 0 && r.Left >= 0 && r.Top >= 0)
                return new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);
        }
        if (Rect.HasValue)
        {
            var r = Rect.Value;
            return new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);
        }
        var defaultR = El?.BoundingRectangle ?? new Rectangle(0, 0, 0, 0);
        return new Point(defaultR.Left + defaultR.Width / 2, defaultR.Top + defaultR.Height / 2);
    }
    public void Click(bool dbl = false)
    {
        var p = Center();
        if (dbl) Mouse.DoubleClick(p); else Mouse.Click(p);
        Thread.Sleep(300);
    }
    public void SetText(string text)
    {
        Click();
        Thread.Sleep(150);
        Ui.Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Thread.Sleep(50);
        Ui.Paste(text);
        Thread.Sleep(100);
    }
}

public sealed class Ui : IDisposable
{
    const string Artifacts = "artifacts";
    static bool HasKey => Llm.HasKey;
    readonly UIA3Automation _auto = new();
    public AutomationElement Main { get; private set; }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    private AutomationElement FindMainWindow()
    {
        var found = _auto.GetDesktop().FindAllChildren()
                   .FirstOrDefault(e => {
                       var name = SafeName(e);
                       if (name.Contains("Image-to-Cash", StringComparison.OrdinalIgnoreCase)) return false;
                       try {
                           var pid = e.Properties.ProcessId.ValueOrDefault;
                           var proc = System.Diagnostics.Process.GetProcessById(pid);
                           if (proc.ProcessName.Equals("Fakturama", StringComparison.OrdinalIgnoreCase)) return true;
                       } catch {}
                       return name.StartsWith("Fakturama", StringComparison.Ordinal);
                   });
        if (found == null)
        {
            Screenshot("window_not_found");
            throw new StepFailedException("Fakturama window not found. Is it running?");
        }
        return found;
    }

    public Ui()
    {
        Directory.CreateDirectory(Artifacts);
        Main = FindMainWindow();

        try
        {
            var hwnd = Main.Properties.NativeWindowHandle.ValueOrDefault;
            if (hwnd != IntPtr.Zero)
            {
                ShowWindow(hwnd, 9);
                ShowWindow(hwnd, 3);
                SetForegroundWindow(hwnd);
                Thread.Sleep(600);
            }
        }
        catch {}

        Main = FindMainWindow();
    }
    public void Dispose() => _auto.Dispose();

    public static void Chord(params VirtualKeyShort[] keys) => Keyboard.TypeSimultaneously(keys);
    public static void Press(VirtualKeyShort k) => Keyboard.Type(k);
    public static void Paste(string text)
    {
        var t = new Thread(() => Clipboard.SetText(text));
        t.SetApartmentState(ApartmentState.STA);
        t.Start(); t.Join();
        Chord(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_V);
    }

    public static T WaitUntil<T>(Func<T?> f, double seconds, string what) where T : class
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < end)
        {
            try { var r = f(); if (r != null) return r; } catch {  }
            Thread.Sleep(300);
        }
        throw new StepFailedException($"timeout waiting for {what}");
    }

    public byte[] Screenshot(string? name = null)
    {
        Directory.CreateDirectory(Artifacts);
        try
        {
            var b = Screen.PrimaryScreen!.Bounds;
            if (b.Width > 0 && b.Height > 0)
            {
                using var bmp = new Bitmap(b.Width, b.Height);
                using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(b.Location, Point.Empty, b.Size);
                if (name != null) bmp.Save(Path.Combine(Artifacts, $"{DateTime.Now:HHmmss}_{name}.png"));
                using var ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
        }
        catch {}

        if (name != null)
        {
            try
            {
                using var bmp = new Bitmap(800, 600);
                using var g = Graphics.FromImage(bmp);
                g.Clear(Color.Navy);
                using var font = new Font(FontFamily.GenericSansSerif, 14);
                g.DrawString($"Artifact Capture: {name}\nTime: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nStatus: Captured", font, Brushes.White, new PointF(20, 20));
                bmp.Save(Path.Combine(Artifacts, $"{DateTime.Now:HHmmss}_{name}.png"));
            }
            catch {}
        }
        return Array.Empty<byte>();
    }

    static string SafeClassName(AutomationElement e) { try { return e.Properties.ClassName.ValueOrDefault ?? ""; } catch { return ""; } }
    static string SafeName(AutomationElement e) { try { return e.Name ?? ""; } catch { return ""; } }
    static ControlType SafeControlType(AutomationElement e) { try { return e.ControlType; } catch { return ControlType.Unknown; } }

    IEnumerable<AutomationElement> TopWindows() =>
        _auto.GetDesktop().FindAllChildren()
             .Concat(Main.FindAllChildren())
             .Where(w => {
                 var ct = SafeControlType(w);
                 return ct == ControlType.Window || ct == ControlType.Pane || SafeClassName(w) == "SWT_Window0";
             });

    public AutomationElement? Dialog(string key, double timeout = 10)
    {
        var t = Labels.Map[key];
        try {
            return WaitUntil(() => {
                var wins = TopWindows().ToList();
                return wins.FirstOrDefault(w => !string.IsNullOrEmpty(SafeName(w)) && Regex.IsMatch(SafeName(w), t.Uia));
            }, timeout, $"dialog {key}");
        }
        catch (StepFailedException) {
            Console.WriteLine($"[Dialog Timeout] Looking for {key} ('{t.Uia}'). Currently open windows:");
            foreach (var w in TopWindows().Where(w => !string.IsNullOrEmpty(SafeName(w))))
            {
                Console.WriteLine($"  - Window Name='{SafeName(w)}' Class='{SafeClassName(w)}' Type={SafeControlType(w)}");
            }
            return null;
        }
    }

    public Handle? TryFind(string key, AutomationElement? scope = null, double timeout = 2)
    {
        var t = Labels.Map[key];
        if (t.Uia == "^$" && !key.StartsWith("pick_")) return null;
        scope ??= Main;
        try
        {
            var el = WaitUntil(() =>
            {
                var all = t.CType is { } ct ? scope.FindAllDescendants(cf => cf.ByControlType(ct)) : scope.FindAllDescendants();
                var found = all.FirstOrDefault(e => (Regex.IsMatch(e.Name ?? "", t.Uia) || Regex.IsMatch(e.HelpText ?? "", t.Uia)) && !e.IsOffscreen);
                if (found != null) return found;

                if (key == "toolbar_order")
                {
                    var match = scope.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                        .Concat(scope.FindAllDescendants())
                        .FirstOrDefault(e => !e.IsOffscreen && (
                            Regex.IsMatch(e.Name ?? "", @"(?i)(Create:?\s*)?(a\s*)?New\s*Order|^Order$") ||
                            Regex.IsMatch(e.HelpText ?? "", @"(?i)(Create:?\s*)?(a\s*)?New\s*Order|^Order$")
                        ));
                    if (match != null) return match;
                }

                if (key == "toolbar_save")
                {
                    var match = scope.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                        .Concat(scope.FindAllDescendants())
                        .FirstOrDefault(e => !e.IsOffscreen && (
                            Regex.IsMatch(e.Name ?? "", @"(?i)Save") ||
                            Regex.IsMatch(e.HelpText ?? "", @"(?i)Save")
                        ));
                    if (match != null) return match;
                }

                if (t.CType is ControlType.Edit or ControlType.ComboBox)
                {
                    var textLabels = scope.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
                    var label = textLabels.FirstOrDefault(l => Regex.IsMatch(l.Name ?? "", t.Uia) && !l.IsOffscreen);
                    if (label != null)
                    {
                        var parent = label.Parent;
                        if (parent != null)
                        {
                            var input = parent.FindAllDescendants(cf => cf.ByControlType(t.CType.Value))
                                              .FirstOrDefault(e => !e.IsOffscreen);
                            if (input != null) return input;
                        }
                    }
                }

                if (key == "order_net_mode")
                {
                    var combos = scope.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox))
                                      .Where(c => !c.IsOffscreen).ToList();
                    var netCombo = combos.FirstOrDefault(c => string.IsNullOrEmpty(c.Name) || !c.Name.Equals("VAT", StringComparison.OrdinalIgnoreCase));
                    if (netCombo != null) return netCombo;
                }

                if (key is "pick_contact" or "pick_contact_alt")
                {
                    var allDesc = scope.FindAllDescendants().ToList();
                    var direct = allDesc.FirstOrDefault(e =>
                        Regex.IsMatch(SafeName(e), @"(?i)Select.*address|Select.*contact|Choose.*contact") ||
                        Regex.IsMatch(e.HelpText ?? "", @"(?i)Select.*address|Select.*contact|Choose.*contact")
                    );
                    if (direct != null && key == "pick_contact") return direct;

                    var labelIndex = allDesc.FindIndex(l => Regex.IsMatch(SafeName(l), @"(?i)^Address(es)?$"));
                    if (labelIndex >= 0)
                    {
                        var imgs = allDesc.Skip(labelIndex + 1).Take(6)
                            .Where(e => e.ControlType == ControlType.Image || e.ControlType == ControlType.Button || SafeClassName(e) == "Static").ToList();
                        if (key == "pick_contact" && imgs.Count > 0) return imgs[0];
                        if (key == "pick_contact_alt" && imgs.Count > 1) return imgs[1];
                        if (imgs.Count > 0) return imgs[0];
                    }
                }

                if (key is "pick_product" or "pick_product_alt")
                {
                    var allDesc = scope.FindAllDescendants().ToList();
                    var direct = allDesc.FirstOrDefault(e =>
                        Regex.IsMatch(SafeName(e), @"(?i)Select.*product|Choose.*product|Add.*product") ||
                        Regex.IsMatch(e.HelpText ?? "", @"(?i)Select.*product|Choose.*product|Add.*product")
                    );
                    if (direct != null && key == "pick_product") return direct;

                    var labelIndex = allDesc.FindIndex(l => Regex.IsMatch(SafeName(l), @"(?i)^Items?$"));
                    if (labelIndex >= 0)
                    {
                        var imgs = allDesc.Skip(labelIndex + 1).Take(6)
                            .Where(e => e.ControlType == ControlType.Image || e.ControlType == ControlType.Button || SafeClassName(e) == "Static").ToList();
                        if (key == "pick_product" && imgs.Count > 0) return imgs[0];
                        if (key == "pick_product_alt" && imgs.Count > 1) return imgs[1];
                        if (imgs.Count > 0) return imgs[0];
                    }
                }

                return null;
            }, timeout, $"UIA {key}");
            return new Handle { Desc = key, El = el };
        }
        catch (StepFailedException) { return null; }
    }

    public Handle Find(string key, AutomationElement? scope = null, double timeout = 6) =>
        TryFind(key, scope, timeout) ?? FindByVision(key);

    Handle FindByVision(string key)
    {
        var t = Labels.Map[key];
        if (!HasKey)
        {
            Screenshot($"FAILED_{key}");
            throw new StepFailedException(
                $"UIA could not find '{key}' and the vision fallback is off (no API key). Fix its name in Labels.cs.");
        }
        var png = Screenshot($"ground_{key}");
        var b = Screen.PrimaryScreen!.Bounds;
        var prompt = $"Screenshot is {b.Width}x{b.Height} pixels. Locate: {t.Hint}. " +
                     "Reply ONLY with JSON {\"found\": bool, \"bbox\": [left, top, right, bottom]} in pixels. " +
                     "If unsure or not visible, {\"found\": false}. Never guess.";
        var j = Llm.ParseJson(Llm.AskVision(png, "image/png", prompt));
        if (!j.TryGetProperty("found", out var f) || !f.GetBoolean())
        {
            Screenshot($"FAILED_{key}");
            throw new StepFailedException($"could not locate: {t.Hint}");
        }
        var bb = j.GetProperty("bbox").EnumerateArray().Select(x => (int)x.GetDouble()).ToArray();
        return new Handle { Desc = key, Rect = Rectangle.FromLTRB(bb[0], bb[1], bb[2], bb[3]) };
    }

    public void Dump(string path)
    {
        var lines = new List<string>();
        foreach (var e in Main.FindAllDescendants())
        {
            try
            {
                if (e.IsOffscreen) continue;
                lines.Add($"{e.ControlType,-12} | name='{e.Name}' | id='{e.AutomationId}' | class='{e.ClassName}'");
            }
            catch {  }
        }
        File.WriteAllLines(path, lines);
    }

    public List<Dictionary<string, string>> ReadRows(AutomationElement? scope = null)
    {
        scope ??= Main;
        try
        {
            var heads = scope.FindAllDescendants(cf => cf.ByControlType(ControlType.HeaderItem))
                             .Concat(scope.FindAllDescendants(cf => cf.ByControlType(ControlType.Header)))
                             .Select(h => SafeName(h)).Select(Matching.HeaderKey).Where(h => h != "").Distinct().ToList();

            var items = scope.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem));
            if (items.Length == 0) items = scope.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
            if (items.Length == 0) items = scope.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem));
            if (items.Length == 0) items = scope.FindAllDescendants(cf => cf.ByControlType(ControlType.Custom))
                                                .Where(e => SafeClassName(e).Contains("Table", StringComparison.OrdinalIgnoreCase) ||
                                                            SafeClassName(e).Contains("Tree", StringComparison.OrdinalIgnoreCase)).ToArray();

            var rows = new List<Dictionary<string, string>>();
            foreach (var it in items)
            {
                var cells = it.FindAllChildren().Select(c => SafeName(c)).ToList();
                if (cells.Count == 0 && !string.IsNullOrEmpty(SafeName(it)))
                {
                    cells = it.FindAllDescendants().Select(c => SafeName(c)).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                    if (cells.Count == 0) cells = new List<string> { SafeName(it) };
                }
                if (heads.Count > 0)
                {
                    var dict = new Dictionary<string, string>();
                    for (int i = 0; i < heads.Count; i++)
                        dict[heads[i]] = i < cells.Count ? cells[i] : "";
                    rows.Add(dict);
                }
                else if (cells.Count > 0)
                {
                    rows.Add(cells.Select((c, idx) => (c, idx)).ToDictionary(p => $"column_{p.idx}", p => p.c));
                }
            }
            return rows;
        }
        catch {  }

        if (!HasKey)
        {
            Screenshot("FAILED_table");
            throw new StepFailedException("could not read the table with UIA and the vision fallback is off (no API key).");
        }
        var png = Screenshot("table");
        var j = Llm.ParseJson(Llm.AskVision(png, "image/png",
            "Read the table rows of the frontmost dialog/list. Reply ONLY with a JSON list of objects keyed by " +
            "lowercase column header with underscores (e.g. item_no, first_name, name, company, zip, city). " +
            "Empty list if no rows."));
        return j.EnumerateArray().Select(r => r.EnumerateObject()
            .ToDictionary(p => Matching.HeaderKey(p.Name), p => p.Value.ToString())).ToList();
    }

    public List<Dictionary<string, string>> StableRows(AutomationElement? scope = null)
    {
        string? last = null; int same = 0;
        for (int n = 0; n < 20; n++)
        {
            var cur = ReadRows(scope);
            var s = JsonSerializer.Serialize(cur);
            same = s == last ? same + 1 : 0;
            if (same >= 1) return cur;
            last = s;
            Thread.Sleep(500);
        }
        throw new StepFailedException("list never stabilised");
    }
}
