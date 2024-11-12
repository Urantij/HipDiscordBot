using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using HipDiscordBot.Discord;
using HipDiscordBot.Work;
using TwitchUtils;

namespace HipDiscordBot.Utilities;

// Генерируемая работа с конфигом просто мусор
// Оно не работает с конструкторами. То есть все мои конфиги должны иметь паблик сет.
// Очень удобно, никогда не укусит меня в жопу.
// Во вторых оно просто не может в обязательные поля)
// Лучше я на другой язык пересяду, чем буду вручную валидацией полей заниматься

[JsonSerializable(typeof(DiscordConfig))]
[JsonSerializable(typeof(TwitchStatuserConfig))]
[JsonSerializable(typeof(StreamAnnounceConfig))]
public partial class ConfigSerializerContext : JsonSerializerContext
{
}

public class CancerConfigLoader
{
    private const string MyConfigPath = "./appsettings.json";

    private readonly JsonNode _root;

    public CancerConfigLoader(JsonNode root)
    {
        this._root = root;
    }

    public bool TryLoadConfig<TConfig>(string path, [MaybeNullWhen(false)] out TConfig config) where TConfig : class
    {
        JsonNode? targetNode = LocateNode(_root, path);

        if (targetNode == null)
        {
            config = null;
            return false;
        }

        var result = targetNode.Deserialize(typeof(TConfig), ConfigSerializerContext.Default) as TConfig;

        if (result == null)
            throw new Exception("result null");

        config = result;
        return true;
    }

    public TConfig LoadConfig<TConfig>(string path) where TConfig : class
    {
        JsonNode? targetNode = LocateNode(_root, path);

        if (targetNode == null)
        {
            throw new Exception("node null");
        }

        var result = targetNode.Deserialize(typeof(TConfig), ConfigSerializerContext.Default) as TConfig;

        if (result == null)
            throw new Exception("result null");

        return result;
    }

    private static JsonNode? LocateNode(JsonNode start, string path)
    {
        string[] entries = path.Split('/');

        JsonNode? targetNode = start;

        foreach (string entry in entries)
        {
            JsonNode? node = targetNode[entry];

            if (node == null)
                return null;

            targetNode = node;
        }

        return targetNode;
    }

    public static CancerConfigLoader Load()
    {
        string content = File.ReadAllText(MyConfigPath);

        JsonNode? root = JsonNode.Parse(content);
        if (root == null)
            throw new Exception("root null");

        return new CancerConfigLoader(root);
    }
}