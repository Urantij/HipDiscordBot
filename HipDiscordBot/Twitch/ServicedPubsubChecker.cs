using Microsoft.Extensions.Options;
using TwitchUtils;
using TwitchUtils.Checkers.Pubsub;

namespace HipDiscordBot.Twitch;

public class ServicedPubsubChecker : PubsubChecker, IHostedService
{
    public ServicedPubsubChecker(IOptions<TwitchStatuserConfig> options, IHostApplicationLifetime lifetime,
        ILoggerFactory loggerFactory) : base(options.Value, loggerFactory, lifetime.ApplicationStopping)
    {
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return base.StartAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();

        return Task.CompletedTask;
    }
}