using HipDiscordBot.Discord;
using Microsoft.Extensions.Options;
using TwitchUtils;
using TwitchUtils.Checkers;

namespace HipDiscordBot.Work;

public class StreamAnnounceConfig
{
    public string Text { get; set; } = "Ура";

    public TimeSpan RebootIgnoreTime { get; set; } = TimeSpan.FromMinutes(10);
}

public class StreamAnnounceWorker : IHostedService
{
    private readonly TwitchStatuser _statuser;
    private readonly DiscordService _discordService;
    private readonly ILogger<StreamAnnounceWorker> _logger;

    private readonly StreamAnnounceConfig _config;

    private DateTime? _lastOfflineDate = null;

    public StreamAnnounceWorker(TwitchStatuser statuser, DiscordService discordService,
        IOptions<StreamAnnounceConfig> options, ILogger<StreamAnnounceWorker> logger)
    {
        _statuser = statuser;
        _discordService = discordService;
        _config = options.Value;
        _logger = logger;

        _statuser.ChannelOnline += StatuserOnChannelOnline;
        _statuser.ChannelOffline += StatuserOnChannelOffline;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _statuser.ChannelOnline -= StatuserOnChannelOnline;
        _statuser.ChannelOffline -= StatuserOnChannelOffline;

        return Task.CompletedTask;
    }

    private void StatuserOnChannelOnline(TwitchCheckInfo checkInfo)
    {
        TimeSpan? timePassed = DateTime.UtcNow - _lastOfflineDate;

        if (timePassed < _config.RebootIgnoreTime)
            return;

        Task.Run(async () =>
        {
            try
            {
                await _discordService.SendMessageAsync(_config.Text);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Ошибка при отправке сообщения в дисковод");
            }
        });
    }

    private void StatuserOnChannelOffline(TwitchCheckInfo obj)
    {
        _lastOfflineDate = DateTime.UtcNow;
    }
}