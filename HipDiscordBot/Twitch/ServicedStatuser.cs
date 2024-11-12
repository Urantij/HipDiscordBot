using Microsoft.Extensions.Options;
using TwitchUtils;
using TwitchUtils.Checkers;

namespace HipDiscordBot.Twitch;

public class ServicedStatuser : TwitchStatuser
{
    public ServicedStatuser(IEnumerable<ITwitchChecker> checkers, IOptions<TwitchStatuserConfig> options, ILoggerFactory? loggerFactory = null) 
        : base(checkers, options.Value, loggerFactory)
    {
    }
}