using System.Text.Json;
using System.Text.Json.Serialization;

namespace DemoPrecompute.Output;

/// <summary>
/// フロント（<c>types/*.ts</c>）が無改造で読めるよう、API と同一の JSON 設定で直列化する
/// （camelCase / enum は文字列 / null 省略）。
/// </summary>
internal static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        MaxDepth = 128,
        WriteIndented = false,
    };

    public static void Write(string path, object value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
    }
}
