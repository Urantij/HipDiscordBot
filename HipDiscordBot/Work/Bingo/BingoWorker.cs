using HipDiscordBot.Bingo;
using HipDiscordBot.Discord;
using NetCord;
using NetCord.Rest;

namespace HipDiscordBot.Work.Bingo;

public class BingoWorker : IHostedService
{
    private const string PersistStoragePath = "BingoWorker.json";

    private const string buttonCommandId = "bingo";

    private readonly DiscordService _discordService;
    private readonly ILogger _logger;

    private BingoConfig? _config;

    private readonly Lock _lock = new();

    public BingoWorker(DiscordService discordService, ILogger<BingoWorker> logger)
    {
        _discordService = discordService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _discordService.DiscordClient.InteractionCreate += DiscordClientOnInteractionCreate;

        await Task.Delay(5000, cancellationToken);

        // Сомнительная херня. Документации у методов нет, почему то Delegate для хендлеров.
        // ApplicationCommandService<ApplicationCommandContext> applicationCommandService = new();

        _config =
            await MyConfig.LoadConfigAsync<BingoConfig>(PersistStoragePath, BingoConfigSerializerContext.Default) ??
            new BingoConfig();

        if (_config.AddBingoCommandId != null)
        {
            try
            {
                await _discordService.DiscordClient.Rest.GetGlobalApplicationCommandAsync(
                    _discordService.DiscordClient.Id,
                    _config.AddBingoCommandId.Value, cancellationToken: cancellationToken);
            }
            catch
            {
                // Документации нет, надеюсь, оно кидает ошибку.
                _config.AddBingoCommandId = null;

                await MyConfig.SaveConfigAsync(PersistStoragePath, _config, BingoConfigSerializerContext.Default);
            }
        }

        if (_config.AddBingoCommandId == null)
        {
            ApplicationCommand created = await _discordService.DiscordClient.Rest.CreateGlobalApplicationCommandAsync(
                _discordService.DiscordClient.Id,
                new SlashCommandProperties("addbingo", "создать кнопку бинго тут")
                    .WithDefaultGuildUserPermissions(Permissions.Administrator));

            _config.AddBingoCommandId = created.Id;

            await MyConfig.SaveConfigAsync(PersistStoragePath, _config, BingoConfigSerializerContext.Default);
        }

        if (_config.MessageId != null && _config.ChannelId != null)
        {
            try
            {
                await _discordService.DiscordClient.Rest.GetMessageAsync(_config.ChannelId.Value,
                    _config.MessageId.Value, cancellationToken: cancellationToken);
            }
            catch
            {
                // Наверное ошибку кидает
                _config.MessageId = null;
                _config.ChannelId = null;

                await MyConfig.SaveConfigAsync(PersistStoragePath, _config, BingoConfigSerializerContext.Default);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _discordService.DiscordClient.InteractionCreate -= DiscordClientOnInteractionCreate;

        return Task.CompletedTask;
    }

    private async ValueTask DiscordClientOnInteractionCreate(Interaction interaction)
    {
        if (_config == null)
            return;

        try
        {
            if (interaction is SlashCommandInteraction slash)
            {
                if (slash.Data.Id == _config.AddBingoCommandId)
                {
                    await HandleAddBingoAsync(slash);
                    return;
                }
            }

            if (interaction is ButtonInteraction button)
            {
                if (button.Message.Id == _config.MessageId &&
                    button.Data.CustomId.StartsWith(buttonCommandId))
                {
                    _logger.LogDebug("Работаем с командой бынго");

                    await HandleBingoAsync(button);
                    return;
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Не удалось...");
        }
    }

    private async Task HandleBingoAsync(ButtonInteraction button)
    {
        if (_config == null)
            return;

        await button.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        MemoryStream? content;
        {
            CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

            content = await BingoGen.GenAsync(_config.Sides, cts.Token);
        }

        if (content == null)
        {
            await button.ModifyResponseAsync(o => o.WithContent("не удалось. не пробуй больше."));
            return;
        }

        try
        {
            DMChannel dm = await button.User.GetDMChannelAsync();

            await dm.SendMessageAsync(
                new MessageProperties()
                    .WithContent("Держи браток")
                    .WithAttachments([new AttachmentProperties("bingo.webp", content)])
            );

            await button.ModifyResponseAsync(o => o.WithContent("лови."));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Не удалось отправить картинку.");

            await button.ModifyResponseAsync(o =>
                o.WithContent("не удалось тебе отправить картинку, мож личка закрыта, хызы."));
        }
        finally
        {
            await content.DisposeAsync();
        }
    }

    private async Task HandleAddBingoAsync(SlashCommandInteraction slash)
    {
        _logger.LogInformation("Создаём основное сообщение...");

        if (_config == null)
        {
            _logger.LogWarning("Попытка создать сообщение без конфига.");
            return;
        }

        RestMessage message;
        try
        {
            message = await _discordService.DiscordClient.Rest.SendMessageAsync(slash.Channel.Id,
                MakeMainCommandMessage());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Не удалось создать сообщение.");
            return;
        }

        _config.ChannelId = slash.Channel.Id;
        _config.MessageId = message.Id;

        await MyConfig.SaveConfigAsync(PersistStoragePath, _config, BingoConfigSerializerContext.Default);

        await slash.SendResponseAsync(InteractionCallback.Message(
            new InteractionMessageProperties()
                .WithContent("готово")
                .WithFlags(MessageFlags.Ephemeral)));
    }

    private static MessageProperties MakeMainCommandMessage()
    {
        MessageProperties properties = new();

        properties.WithContent("");

        properties.AddComponents(new ActionRowProperties([
            new ButtonProperties(
                customId: buttonCommandId,
                label: $"хочу бинго",
                style: ButtonStyle.Primary)
        ]));

        return properties;
    }
}