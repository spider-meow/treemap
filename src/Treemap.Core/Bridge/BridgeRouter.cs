using System.Text.Json;
using System.Text.Json.Serialization;

namespace Treemap.Core.Bridge;

/// <summary>
/// Routes the JSON messages sent by the web interface to named handlers and builds the replies.
/// It knows nothing about WebView2: the window only moves strings in and out, which keeps this testable.
///
/// Wire format (must match <c>src/Treemap.Web/src/bridge/bridge.ts</c>):
///   web to host   { "id": 1, "type": "specs.get", "payload": ... }
///   host to web   { "kind": "response", "id": 1, "ok": true, "payload": ... } or { ..., "ok": false, "error": "..." }
///                 { "kind": "event", "type": "metrics", "payload": ... }
/// </summary>
public sealed class BridgeRouter
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private readonly Dictionary<string, Func<JsonElement, Task<object?>>> _handlers = new(StringComparer.Ordinal);

    /// <summary>Registers the handler for a message type. It receives the payload (an undefined payload arrives as a default element).</summary>
    public void Register(string type, Func<JsonElement, Task<object?>> handler) => _handlers[type] = handler;

    public void Register(string type, Func<Task<object?>> handler) => _handlers[type] = _ => handler();

    /// <summary>Handles one incoming message; returns the reply to post back, or null when the message is unusable.</summary>
    public async Task<string?> HandleAsync(string messageJson)
    {
        int id;
        string type;
        JsonElement payload;
        try
        {
            using var document = JsonDocument.Parse(messageJson);
            var root = document.RootElement;
            id = root.GetProperty("id").GetInt32();
            type = root.GetProperty("type").GetString() ?? string.Empty;
            payload = root.TryGetProperty("payload", out var p) ? p.Clone() : default;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null; // Not one of our messages: without an id there is nobody to answer.
        }

        if (!_handlers.TryGetValue(type, out var handler))
        {
            return Failure(id, $"Message inconnu : {type}");
        }

        try
        {
            return Success(id, await handler(payload));
        }
        catch (Exception ex)
        {
            // Whatever a handler throws must reach the interface as an error reply, never kill the bridge.
            return Failure(id, ex.Message);
        }
    }

    /// <summary>The message pushing an event (live metrics, scan progress...) to the interface.</summary>
    public static string EventJson(string type, object? payload) =>
        JsonSerializer.Serialize(new { kind = "event", type, payload }, Options);

    private static string Success(int id, object? payload) =>
        JsonSerializer.Serialize(new { kind = "response", id, ok = true, payload }, Options);

    private static string Failure(int id, string error) =>
        JsonSerializer.Serialize(new { kind = "response", id, ok = false, error }, Options);
}
