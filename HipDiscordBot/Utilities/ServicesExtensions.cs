using System.Diagnostics.CodeAnalysis;

namespace HipDiscordBot.Utilities;

public static class ServicesExtensions
{
    // TODO разобратьсй бы, за4ем нужны эти атрибуты к типу
    // Без при aot di не находит конструктор, как я понимаю
    // мож без этих атрибутов компилятор тримит неиспользумые напрямую конструкторы? 
    
    public static void AddHostedSingleton<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IServiceCollection collection) where T : class, IHostedService
    {
        collection.AddSingleton<T>();
        collection.AddSingleton<IHostedService, T>(p => p.GetRequiredService<T>());
    }

    public static void AddHostedSingleton<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T, TInterface>(this IServiceCollection collection)
        where T : class, IHostedService, TInterface
        where TInterface : class
    {
        collection.AddSingleton<T>();
        collection.AddSingleton<IHostedService, T>(p => p.GetRequiredService<T>());
        collection.AddSingleton<TInterface, T>(p => p.GetRequiredService<T>());
    }
}