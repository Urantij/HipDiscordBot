using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using HipDiscordBot.Utilities;

namespace HipDiscordBot.Work;

public static class MyConfig
{
    public static async Task<T?> LoadConfigAsync<T>(string path, JsonSerializerContext context) where T : class
    {
        if (File.Exists(path))
            return JsonSerializerExtensions.Deserialize<T>(
                await File.ReadAllTextAsync(path), context);

        return null;
    }

    [UnconditionalSuppressMessage("Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "ну там стоит контект в опции")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "ну там стоит контект в опции")]
    public static Task SaveConfigAsync<T>(string path, T save, JsonSerializerContext context)
    {
        return File.WriteAllTextAsync(path, JsonSerializer.Serialize(save, new JsonSerializerOptions()
        {
            WriteIndented = true,
            TypeInfoResolver = context
        }));
    }
}