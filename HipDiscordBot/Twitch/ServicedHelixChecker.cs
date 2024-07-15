using Microsoft.Extensions.Options;
using TwitchUtils;
using TwitchUtils.Checkers.Helix;

namespace HipDiscordBot.Twitch;

public class ServicedHelixChecker : HelixChecker, IHostedService
{
    public ServicedHelixChecker(IOptions<TwitchStatuserConfig> options,
        ILoggerFactory? loggerFactory = null,
        CancellationToken cancellationToken = default) : base(options.Value, loggerFactory, cancellationToken)
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