using System.Text;

namespace ReWASD.Core.Models;

/// <summary>An HTTP request sent from a macro: <c>METHOD url [-H "Name=Value"]... [body]</c>.</summary>
public sealed record WebhookRequest(string Method, string Url, IReadOnlyList<KeyValuePair<string, string>> Headers, string Body)
{
    public static readonly string[] Methods = { "GET", "POST", "PUT", "DELETE", "PATCH" };

    public static bool TryParse(string text, out WebhookRequest request, out string error)
    {
        request = new WebhookRequest("GET", string.Empty, Array.Empty<KeyValuePair<string, string>>(), string.Empty);
        error = string.Empty;

        text = (text ?? string.Empty).Trim();
        int i = 0;
        string method = NextWord(text, ref i);
        string url;

        if (Methods.Contains(method, StringComparer.OrdinalIgnoreCase))
        {
            url = NextWord(text, ref i);
        }
        else
        {
            // A bare address means GET
            url = method;
            method = "GET";
            i = 0;
            url = NextWord(text, ref i);
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = $"'{url}' is not an http or https address";
            return false;
        }

        var headers = new List<KeyValuePair<string, string>>();
        while (true)
        {
            int save = i;
            string word = NextWord(text, ref i);
            if (word != "-H") { i = save; break; }

            string header = NextWord(text, ref i);
            int eq = header.IndexOf('=');
            if (eq <= 0)
            {
                error = $"A header must look like -H \"Name=Value\", not '{header}'";
                return false;
            }
            headers.Add(new(header[..eq].Trim(), header[(eq + 1)..].Trim()));
        }

        string body = text[Math.Min(i, text.Length)..].Trim();
        request = new WebhookRequest(method.ToUpperInvariant(), uri.ToString(), headers, body);
        return true;
    }

    // A word is text up to the next space, or a "quoted text" kept whole
    private static string NextWord(string text, ref int i)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i])) i++;
        if (i >= text.Length) return string.Empty;

        var sb = new StringBuilder();
        if (text[i] == '"')
        {
            i++;
            while (i < text.Length && text[i] != '"') sb.Append(text[i++]);
            if (i < text.Length) i++;
            return sb.ToString();
        }

        while (i < text.Length && !char.IsWhiteSpace(text[i])) sb.Append(text[i++]);
        return sb.ToString();
    }
}

/// <summary>An OBS WebSocket request sent from a macro: <c>RequestType [json]</c>, or one of the short forms.</summary>
public sealed record ObsRequest(string RequestType, string DataJson)
{
    public static bool TryParse(string text, out ObsRequest request, out string error)
    {
        request = new ObsRequest(string.Empty, string.Empty);
        error = string.Empty;

        text = (text ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            error = "Obs needs a request, like Obs:StartRecord or Obs:Scene Gaming";
            return false;
        }

        int space = text.IndexOf(' ');
        string word = space < 0 ? text : text[..space];
        string rest = space < 0 ? string.Empty : text[(space + 1)..].Trim();

        // Short forms for the requests people use most
        switch (word.ToLowerInvariant())
        {
            case "scene":
                return Named("SetCurrentProgramScene", "sceneName", rest, "Obs:Scene needs the scene's name", out request, out error);
            case "mute":
                return Named("SetInputMute", "inputName", rest, "Obs:Mute needs the input's name", out request, out error, extra: "\"inputMuted\":true");
            case "unmute":
                return Named("SetInputMute", "inputName", rest, "Obs:Unmute needs the input's name", out request, out error, extra: "\"inputMuted\":false");
            case "togglemute":
                return Named("ToggleInputMute", "inputName", rest, "Obs:ToggleMute needs the input's name", out request, out error);
            case "record":
                request = new ObsRequest("ToggleRecord", string.Empty);
                return true;
            case "stream":
                request = new ObsRequest("ToggleStream", string.Empty);
                return true;
        }

        if (rest.Length > 0)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(rest);
                if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) throw new System.Text.Json.JsonException();
            }
            catch (System.Text.Json.JsonException)
            {
                error = $"The details after {word} must be a JSON object like {{\"sceneName\":\"Gaming\"}}";
                return false;
            }
        }

        request = new ObsRequest(word, rest);
        return true;
    }

    private static bool Named(string type, string field, string value, string missing, out ObsRequest request, out string error, string? extra = null)
    {
        request = new ObsRequest(string.Empty, string.Empty);
        error = string.Empty;
        if (value.Length == 0)
        {
            error = missing;
            return false;
        }

        string json = "{" + $"\"{field}\":{System.Text.Json.JsonSerializer.Serialize(value)}" + (extra == null ? "" : "," + extra) + "}";
        request = new ObsRequest(type, json);
        return true;
    }
}
