using System.Text.Json;
using Treemap.Core.Bridge;
using Xunit;

namespace Treemap.Core.Tests;

public class BridgeRouterTests
{
    private static JsonElement Parse(string? json)
    {
        Assert.NotNull(json);
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public async Task KnownType_RepliesWithPayloadAndSameId()
    {
        var router = new BridgeRouter();
        router.Register("ping", () => Task.FromResult<object?>(new { value = 42 }));

        var reply = Parse(await router.HandleAsync("""{"id":7,"type":"ping"}"""));

        Assert.Equal("response", reply.GetProperty("kind").GetString());
        Assert.Equal(7, reply.GetProperty("id").GetInt32());
        Assert.True(reply.GetProperty("ok").GetBoolean());
        Assert.Equal(42, reply.GetProperty("payload").GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task Handler_ReceivesPayload()
    {
        var router = new BridgeRouter();
        router.Register("echo", payload => Task.FromResult<object?>(new { path = payload.GetProperty("path").GetString() }));

        var reply = Parse(await router.HandleAsync("""{"id":1,"type":"echo","payload":{"path":"C:\\Jeux"}}"""));

        Assert.Equal("C:\\Jeux", reply.GetProperty("payload").GetProperty("path").GetString());
    }

    [Fact]
    public async Task UnknownType_RepliesWithError()
    {
        var router = new BridgeRouter();

        var reply = Parse(await router.HandleAsync("""{"id":3,"type":"nope"}"""));

        Assert.False(reply.GetProperty("ok").GetBoolean());
        Assert.Equal(3, reply.GetProperty("id").GetInt32());
        Assert.Contains("nope", reply.GetProperty("error").GetString());
    }

    [Fact]
    public async Task ThrowingHandler_RepliesWithErrorInsteadOfThrowing()
    {
        var router = new BridgeRouter();
        router.Register("boom", () => throw new InvalidOperationException("disque absent"));

        var reply = Parse(await router.HandleAsync("""{"id":9,"type":"boom"}"""));

        Assert.False(reply.GetProperty("ok").GetBoolean());
        Assert.Equal("disque absent", reply.GetProperty("error").GetString());
    }

    [Theory]
    [InlineData("pas du json")]
    [InlineData("""{"type":"ping"}""")]
    [InlineData("""{"id":"x","type":"ping"}""")]
    [InlineData("[]")]
    public async Task UnusableMessage_IsIgnored(string message)
    {
        var router = new BridgeRouter();
        router.Register("ping", () => Task.FromResult<object?>(null));

        Assert.Null(await router.HandleAsync(message));
    }

    [Fact]
    public void Event_UsesCamelCaseAndStringEnums()
    {
        var json = BridgeRouter.EventJson("metrics", new { CpuPercent = 12.5, Kind = DayOfWeek.Friday });
        var message = Parse(json);

        Assert.Equal("event", message.GetProperty("kind").GetString());
        Assert.Equal("metrics", message.GetProperty("type").GetString());
        Assert.Equal(12.5, message.GetProperty("payload").GetProperty("cpuPercent").GetDouble());
        Assert.Equal("friday", message.GetProperty("payload").GetProperty("kind").GetString());
    }
}
