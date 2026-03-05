using System.Text.Json.Serialization;

namespace ZakYip.PlcBridge.Core.Models.SignalR {

    /// <summary>
    /// Invoke 统一入口请求体。
    /// </summary>
    public sealed record class InvokeEnvelope {
        [JsonPropertyName("commandName")]
        public string? CommandName { get; init; }

        [JsonPropertyName("request")]
        public object? Request { get; init; }
    }
}
