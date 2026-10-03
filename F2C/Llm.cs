using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace F2C;

public static class Llm
{
    public static readonly string Model = Environment.GetEnvironmentVariable("F2C_MODEL") ?? "gemini-1.5-flash-latest";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };
    const int MaxRetries = 4;
    static readonly int[] BackoffMs = [2000, 5000, 10000, 20000];

    public static string ApiKey =>
        Environment.GetEnvironmentVariable("GEMINI_API_KEY")
        ?? throw new InvalidOperationException("Set GEMINI_API_KEY first.");

    public static bool HasKey =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GEMINI_API_KEY"));

    static string Send(string body)
    {
        var key = ApiKey;
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{Model}:generateContent?key={key}";

        for (int attempt = 0; ; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            using var resp = Http.Send(req);
            var text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (resp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(text);
                return doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString() ?? "";
            }

            var code = (int)resp.StatusCode;
            bool transient = code == 429 || code == 500 || code == 502 || code == 503;

            if (!transient || attempt >= MaxRetries)
                throw new InvalidOperationException($"Gemini API error {code}: {text}");

            var delay = BackoffMs[Math.Min(attempt, BackoffMs.Length - 1)];
            Console.WriteLine($"[LLM] {code} on attempt {attempt + 1}/{MaxRetries + 1}, retrying in {delay / 1000}s...");
            Thread.Sleep(delay);
        }
    }

    public static string AskVision(byte[] image, string mediaType, string prompt)
    {
        var body = JsonSerializer.Serialize(new
        {
            contents = new[] { new {
                parts = new object[] {
                    new { inline_data = new { mime_type = mediaType, data = Convert.ToBase64String(image) } },
                    new { text = prompt }
                }
            }},
            generationConfig = new { temperature = 0.0, maxOutputTokens = 4000 }
        });
        return Send(body);
    }

    public static string AskText(string prompt)
    {
        var body = JsonSerializer.Serialize(new
        {
            contents = new[] { new {
                parts = new object[] { new { text = prompt } }
            }},
            generationConfig = new { temperature = 0.0, maxOutputTokens = 2000 }
        });
        return Send(body);
    }

    public static JsonElement ParseJson(string text)
    {
        var clean = Regex.Replace(text.Trim(), @"^```(?:json)?|```$", "", RegexOptions.Multiline).Trim();
        return JsonDocument.Parse(clean).RootElement.Clone();
    }
}
