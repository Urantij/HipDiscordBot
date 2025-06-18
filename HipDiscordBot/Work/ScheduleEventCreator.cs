using HipDiscordBot.Discord;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Rest;
using TwitchUtils;
using TwitchUtils.Checkers;

namespace HipDiscordBot.Work;

// TODO добавить какую то логику сохранения инфы о стриме, чтобы переподруб бота во время стрима нормально работал
// И переподруб бота после конца стрима.

public class ScheduleEventCreatorConfig
{
    public string Name { get; set; } = "СТРИМЧИК";

    public string Location { get; set; } = "https://www.twitch.tv/hipstocrat";

    public string? Description { get; set; } = null;

    public string? ImagePath { get; set; } = null;

    // Асинхронность надо думать, потом как нить
    // public string EventIdPath { get; set; } = "./created_event_id";
}

public class ScheduleEventCreator : IHostedService
{
    private readonly TwitchStatuser _statuser;
    private readonly DiscordService _discordService;
    private readonly IOptions<ScheduleEventCreatorConfig> _options;
    private readonly IOptions<DiscordConfig> _discordOptions;
    private readonly ILogger _logger;

    private GuildScheduledEvent? _currentEvent;

    public ScheduleEventCreator(TwitchStatuser statuser, DiscordService discordService,
        IOptions<ScheduleEventCreatorConfig> options, IOptions<DiscordConfig> discordOptions,
        ILogger<ScheduleEventCreator> logger)
    {
        _statuser = statuser;
        _discordService = discordService;
        _options = options;
        _discordOptions = discordOptions;
        _logger = logger;

        _statuser.ChannelOnline += StatuserOnChannelOnline;
        _statuser.ChannelOffline += StatuserOnChannelOffline;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // if (File.Exists(_options.Value.EventIdPath))
        // {
        //     Task.Run(async () =>
        //     {
        //         try
        //         {
        //         }
        //         catch (Exception e)
        //         {
        //             _logger.LogError(e, "Не удалось ударить старый ивент при запуске");
        //         }
        //     }, cancellationToken);
        // }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _statuser.ChannelOnline -= StatuserOnChannelOnline;
        _statuser.ChannelOffline -= StatuserOnChannelOffline;

        return Task.CompletedTask;
    }

    private void StatuserOnChannelOnline(TwitchCheckInfo obj)
    {
        // TODO нормально сделать но мне впадву

        Task.Run(async () =>
        {
            try
            {
                Channel channel =
                    await _discordService.DiscordClient.Rest.GetChannelAsync(_discordOptions.Value.ChannelId);

                if (channel is not IGuildChannel guildChannel)
                {
                    _logger.LogError("таргет ченел не гилд ченел шож делать {typename}", channel.GetType().Name);
                    return;
                }

                ImageProperties? imageProperties = null;

                if (_options.Value.ImagePath != null)
                {
                    try
                    {
                        byte[] imageBytes = await File.ReadAllBytesAsync(_options.Value.ImagePath);

                        string extension = Path.GetExtension(_options.Value.ImagePath).Substring(1);

                        ImageFormat format = Enum.Parse<ImageFormat>(extension, true);

                        imageProperties = new ImageProperties(format, imageBytes);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Не удалось загрузить имедж для ивента");
                    }
                }

                GuildScheduledEvent ev = await _discordService.DiscordClient.Rest.CreateGuildScheduledEventAsync(
                    guildChannel.GuildId,
                    new GuildScheduledEventProperties(_options.Value.Name,
                        GuildScheduledEventPrivacyLevel.GuildOnly,
                        DateTimeOffset.UtcNow + TimeSpan.FromMinutes(30),
                        GuildScheduledEventEntityType.External)
                    {
                        Description = _options.Value.Description,
                        Image = imageProperties,
                        Metadata = new GuildScheduledEventMetadataProperties(_options.Value.Location),
                        ScheduledEndTime = DateTimeOffset.UtcNow + TimeSpan.FromHours(12)
                    });

                // не знаю надо локать или нет. о4 маловеройтно заролйет. а если обрабатывать, логики много надо
                _currentEvent = ev;

                await ev.ModifyAsync(a => a.Status = GuildScheduledEventStatus.Active);

                // await File.WriteAllTextAsync(_options.Value.EventIdPath, ev.Id.ToString());

                _logger.LogInformation("Отправили ивент.");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Не удалось сделать ивент.");
            }
        });
    }

    private void StatuserOnChannelOffline(TwitchCheckInfo obj)
    {
        if (_currentEvent == null)
            return;

        GuildScheduledEvent ev = _currentEvent;
        _currentEvent = null;

        Task.Run(async () =>
        {
            try
            {
                await ev.DeleteAsync();

                // да да асинхронность без локов да да да
                // File.Delete(_options.Value.EventIdPath);

                _logger.LogInformation("Убили ивент.");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Не удалось убить ивент.");
            }
        });
    }
}