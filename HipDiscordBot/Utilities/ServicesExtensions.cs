namespace HipDiscordBot.Utilities;

public static class ServicesExtensions
{
    public static void AddHostedSingleton<T>(this IServiceCollection collection) where T : class, IHostedService
    {
        collection.AddSingleton<T>();
        collection.AddSingleton<IHostedService, T>(p => p.GetRequiredService<T>());
    }

    public static void AddHostedSingleton<T, TInterface>(this IServiceCollection collection)
        where T : class, IHostedService, TInterface
        where TInterface : class
    {
        collection.AddSingleton<T>();
        collection.AddSingleton<IHostedService, T>(p => p.GetRequiredService<T>());
        collection.AddSingleton<TInterface, T>(p => p.GetRequiredService<T>());
    }
}