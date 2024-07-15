using HipDiscordBot.Discord;
using HipDiscordBot.Twitch;
using HipDiscordBot.Utilities;
using HipDiscordBot.Work;
using TwitchUtils;
using TwitchUtils.Checkers;
using TwitchUtils.Checkers.Helix;

namespace HipDiscordBot;

public class Program
{
    public static void Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Logging.SetMinimumLevel(LogLevel.Debug);

        builder.Services.AddOptions<DiscordConfig>()
            .BindConfiguration("Discord")
            .ValidateOnStart();
        builder.Services.AddHostedSingleton<DiscordService>();

        builder.Services.AddOptions<TwitchStatuserConfig>()
            .BindConfiguration("Twitch")
            // .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddHostedSingleton<ServicedPubsubChecker, ITwitchChecker>();

        if (builder.Configuration.GetSection("Twitch").GetSection("Helix").Exists())
        {
            System.Console.WriteLine("хеликс");

            builder.Services.AddOptions<HelixConfig>()
                .BindConfiguration("Twitch/Helix")
                // .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.Services.AddHostedSingleton<ServicedHelixChecker, ITwitchChecker>();
        }

        builder.Services.AddSingleton<TwitchStatuser>();

        builder.Services.AddOptions<StreamAnnounceConfig>()
            .BindConfiguration("Announce")
            .ValidateOnStart();
        builder.Services.AddHostedSingleton<StreamAnnounceWorker>();

        builder.Services.AddHostedSingleton<DiscordRoleApplierWorker>();

        IHost host = builder.Build();
        host.Run();
    }
}