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
        // https://github.com/dotnet/runtime/issues/95006

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Logging.SetMinimumLevel(LogLevel.Debug);

        {
            CancerConfigLoader bind = CancerConfigLoader.Load();

            builder.Services.AddCancerOptions<DiscordConfig>("Discord", bind);
            builder.Services.AddHostedSingleton<DiscordService>();

            builder.Services.AddCancerOptions<TwitchStatuserConfig>("Twitch", bind);
            builder.Services.AddHostedSingleton<ServicedPubsubChecker, ITwitchChecker>();

            if (bind.TryLoadConfig<HelixConfig>("Twitch/Helix", out HelixConfig? helixConfig))
            {
                System.Console.WriteLine("хеликс");

                builder.Services.AddCancerOptions<HelixConfig>(helixConfig);
                builder.Services.AddHostedSingleton<ServicedHelixChecker, ITwitchChecker>();
            }

            builder.Services.AddSingleton<ServicedStatuser>();
            builder.Services.AddSingleton<TwitchStatuser>(sp => sp.GetRequiredService<ServicedStatuser>());

            builder.Services.AddCancerOptions<StreamAnnounceConfig>("Announce", bind);
            builder.Services.AddHostedSingleton<StreamAnnounceWorker>();

            builder.Services.AddHostedSingleton<DiscordRoleApplierWorker>();
        }

        IHost host = builder.Build();
        host.Run();
    }
}