using Microsoft.Extensions.Options;
using TwitchUtils;
using TwitchUtils.Checkers.Pubsub;

namespace HipDiscordBot.Twitch;

public class ServicedPubsubChecker : PubsubChecker, IHostedService
{
    public ServicedPubsubChecker(IOptions<TwitchStatuserConfig> options,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default) : base(options.Value, loggerFactory, cancellationToken)
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