using System.Text.Json;
using System.Text.Json.Serialization;

namespace HipDiscordBot.Utilities;

public static class JsonSerializerExtensions
{
    public static T? TryDeserialize<T>(string content, JsonSerializerContext context) where T : class
    {
        return JsonSerializer.Deserialize(content, typeof(T), context) as T;
    }

    public static T Deserialize<T>(string content, JsonSerializerContext context) where T : class
    {
        T? result = JsonSerializer.Deserialize(content, typeof(T), context) as T;

        if (result == null)
            throw new Exception("Unable to Deserialize");

        return result;
    }
}