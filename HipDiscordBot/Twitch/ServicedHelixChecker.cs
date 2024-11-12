using Microsoft.Extensions.Options;
using TwitchUtils;
using TwitchUtils.Checkers.Helix;

namespace HipDiscordBot.Twitch;

public class ServicedHelixChecker : HelixChecker, IHostedService
{
    public ServicedHelixChecker(IOptions<TwitchStatuserConfig> options, IHostApplicationLifetime lifetime,
        ILoggerFactory loggerFactory) : base(options.Value, loggerFactory, lifetime.ApplicationStopping)
    {
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Start();

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}