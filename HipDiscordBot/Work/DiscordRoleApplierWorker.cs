using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using HipDiscordBot.Discord;
using HipDiscordBot.Utilities;
using NetCord;
using NetCord.Rest;

namespace HipDiscordBot.Work;

[JsonSerializable(typeof(DiscordRoleApplierConfig))]
public partial class DiscordRoleApplierConfigSerializerContext : JsonSerializerContext
{
}

public class DiscordRoleApplierWorker : IHostedService
{
    private const string PersistStoragePath = "DiscordRoleApplierWorker.json";

    private const string MainCommandCustomId = "roleinteraction";

    private readonly DiscordService _discordService;
    private readonly ILogger<DiscordRoleApplierWorker> _logger;

    private DiscordRoleApplierConfig? _config;

    public DiscordRoleApplierWorker(DiscordService discordService, ILogger<DiscordRoleApplierWorker> logger)
    {
        _discordService = discordService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _discordService.DiscordClient.InteractionCreate += DiscordClientOnInteractionCreate;

        _config = await LoadConfigAsync();

        // Дискорд очень смешные ошибки выкидывает, если не подождать.
        await Task.Delay(5000, cancellationToken);

        if (_config.CheckRoleInteractionId == null)
        {
            await CreateRoleSelectCommandAsync();
        }

        if (_config.MessageId == null)
        {
            if (_config.CreateInteractionId == null)
            {
                await CreateCreationCommand();
                return;
            }

            if (_config.ChannelId == null)
            {
                _logger.LogWarning("А канала нет...");
                await CreateCreationCommand();
                return;
            }

            await CreateMainCommandMessageAsync(_config.ChannelId.Value);
            return;
        }

        if (_config.ChannelId == null)
        {
            _logger.LogWarning("А канала нет...");
            await CreateCreationCommand();
            return;
        }

        try
        {
            // Вот бы документацию, какая ошибка, если сообщения нет
            await _discordService.DiscordClient.Rest.GetMessageAsync(_config.ChannelId.Value,
                _config.MessageId.Value, cancellationToken: cancellationToken);
        }
        catch (RestException restException) when (restException.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Сообщение не найдено, создаём...");

            await CreateMainCommandMessageAsync(_config.ChannelId.Value);
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
                if (slash.Data.Id == _config.CheckRoleInteractionId)
                {
                    await HandleRoleCheckCommandAsync(slash);
                }
                else if (slash.Data.Id == _config.CreateInteractionId)
                {
                    _logger.LogInformation("Создаём основную команду...");

                    await HandleCreateCommandAsync(slash);
                }
            }

            if (interaction is ButtonInteraction button)
            {
                if (button.Message.Id == _config.MessageId &&
                    button.Data.CustomId.StartsWith(MainCommandCustomId))
                {
                    _logger.LogDebug("Работаем с командой role...");

                    await HandleRoleButtonAsync(button);
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Не удалось...");
        }
    }

    private async Task HandleRoleCheckCommandAsync(SlashCommandInteraction discordInteraction)
    {
        if (_config == null)
        {
            _logger.LogWarning($"{nameof(HandleRoleCheckCommandAsync)} конфиг нул");
            return;
        }

        if (!discordInteraction.Data.Options.Any())
        {
            await discordInteraction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties()
                .WithContent("И че?")
                .WithFlags(MessageFlags.Ephemeral)));
            return;
        }

        if (discordInteraction.Data.Options[0].Type != ApplicationCommandOptionType.Role)
        {
            await discordInteraction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties()
                .WithContent("клево ты придумал")
                .WithFlags(MessageFlags.Ephemeral)));
            return;
        }

        if (discordInteraction.Data.Options[0].Value is null)
        {
            _logger.LogError("дискорд капитально сломался");

            await discordInteraction.SendResponseAsync(InteractionCallback.Message(new InteractionMessageProperties()
                .WithContent("дискорд капитально сломался")
                .WithFlags(MessageFlags.Ephemeral)));
            return;
        }

        // TODO я не понимаю, почему он требует !
        ulong roleId = ulong.Parse(discordInteraction.Data.Options[0].Value!);

        string? text = discordInteraction.Data.Options.ElementAtOrDefault(1)?.Value;

        SavedRole? existingRole = _config.Roles?.FirstOrDefault(r => r.RoleId == roleId);
        if (existingRole != null)
        {
            _logger.LogDebug("Убираем роль...");

            _config.Roles!.Remove(existingRole);
        }
        else
        {
            if (text == null)
            {
                await discordInteraction.SendResponseAsync(InteractionCallback.Message(
                    new InteractionMessageProperties()
                        .WithContent("а текст кто писать будет")
                        .WithFlags(MessageFlags.Ephemeral)));
                return;
            }

            _logger.LogDebug("Добавляем роль...");

            _config.Roles ??= new List<SavedRole>();

            _config.Roles.Add(new SavedRole(text, roleId));
        }

        await discordInteraction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        await SaveConfigAsync();
        await UpdateMainCommandMessageAsync();

        await discordInteraction.ModifyResponseAsync(o => o.WithContent("Сделана!"));
    }

    private async Task HandleCreateCommandAsync(SlashCommandInteraction discordInteraction)
    {
        if (_config == null)
        {
            _logger.LogWarning($"{nameof(HandleCreateCommandAsync)} конфиг нул");
            return;
        }

        if (_config.CreateInteractionId == null)
        {
            _logger.LogWarning($"{nameof(HandleCreateCommandAsync)} {nameof(_config.CreateInteractionId)} нул");
            return;
        }

        await discordInteraction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        await _discordService.DiscordClient.Rest.DeleteGlobalApplicationCommandAsync(0,
            _config.CreateInteractionId.Value);
        _config.CreateInteractionId = null;

        try
        {
            await CreateMainCommandMessageAsync(discordInteraction.Channel.Id, saveChanges: false);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Не удалось создание сообщение...");

            _config.ChannelId = discordInteraction.Channel.Id;
            await SaveConfigAsync();
            return;
        }

        await SaveConfigAsync();

        await discordInteraction.ModifyResponseAsync(o => o.WithContent("Сделана!"));
    }

    private async Task HandleRoleButtonAsync(ButtonInteraction discordInteraction)
    {
        if (_config == null)
        {
            _logger.LogWarning($"{nameof(HandleRoleButtonAsync)} конфиг нул");
            return;
        }

        await discordInteraction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

        if (!int.TryParse(discordInteraction.Data.CustomId[MainCommandCustomId.Length..], out int index))
        {
            _logger.LogError("Кривой customid {id}", discordInteraction.Data.CustomId);

            await discordInteraction.ModifyResponseAsync(o => o.WithContent("пошёл нахуй"));
            return;
        }

        SavedRole? saveRole = _config.Roles?.ElementAtOrDefault(index);

        if (saveRole == null)
        {
            _logger.LogError("Кривой индекс {id}", index);

            await discordInteraction.ModifyResponseAsync(o => o.WithContent("сори братик всё сломано"));
            return;
        }

        Role? role = null;
        if (discordInteraction.Guild != null)
            role = await discordInteraction.Guild.GetRoleAsync(saveRole.RoleId);

        if (role is null)
        {
            _logger.LogWarning("Роль не найдена.");

            await discordInteraction.ModifyResponseAsync(o => o.WithContent("сори братик всё сломано"));
            return;
        }

        // мне пришлось запускать приложение и через дебаг смотреть что за тип типочек тут лежит.
        // документацию бы..
        if (discordInteraction.User is not GuildInteractionUser member)
        {
            _logger.LogDebug("Не нашли мембера...");
            await discordInteraction.ModifyResponseAsync(o => o.WithContent("сори братик всё сломано"));
            return;
        }

        bool exist = member.RoleIds.Any(r => r == role.Id);

        if (exist)
        {
            await member.RemoveRoleAsync(role.Id, new RestRequestProperties()
            {
                AuditLogReason = "Попросил!"
            });
        }
        else
        {
            await member.AddRoleAsync(role.Id, new RestRequestProperties()
            {
                AuditLogReason = "Попросил!"
            });
        }

        await discordInteraction.ModifyResponseAsync(o => o.WithContent("Всё сделано в лучшем виде, приходите ещё."));
    }

    private async Task UpdateMainCommandMessageAsync()
    {
        _logger.LogInformation("Обновляем основное сообщение...");

        if (_config == null)
        {
            _logger.LogWarning("Попытка обновить сообщение без конфига.");
            return;
        }

        if (_config.ChannelId == null || _config.MessageId == null)
        {
            _logger.LogWarning("Попытка обновить сообщение без айди канала или сообщения.");
            return;
        }

        // ЭЭ, пачему то не работает. BadRequest
        // await message.ModifyAsync(b => MakeMainCommandMessage(b, _config.Roles ?? []));

        await _discordService.DiscordClient.Rest.DeleteMessageAsync(_config.ChannelId.Value, _config.MessageId.Value);
        await CreateMainCommandMessageAsync(_config.ChannelId.Value);
    }

    private async Task CreateMainCommandMessageAsync(ulong channelId, bool saveChanges = true)
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
            message = await _discordService.DiscordClient.Rest.SendMessageAsync(channelId,
                MakeMainCommandMessage(_config.Roles ?? []));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Не удалось создать сообщение.");
            return;
        }

        _config.ChannelId = channelId;
        _config.MessageId = message.Id;

        if (saveChanges)
        {
            await SaveConfigAsync();
        }
    }

    // Создаёт параметры сообщения с кнопками
    private static MessageProperties MakeMainCommandMessage(ICollection<SavedRole> roles)
    {
        MessageProperties properties = new();

        properties.WithContent("кто нажал тот сдохнет");

        if (roles.Count > 0)
        {
            properties.AddComponents(new MessageComponentProperties[]
            {
                new ActionRowProperties(roles.Select(MakeComponent).ToArray())
            });
        }

        return properties;
    }

    private static ButtonProperties MakeComponent(SavedRole role, int index)
    {
        return new ButtonProperties(
            customId: MainCommandCustomId + index.ToString(),
            label: $"хочу знать когда {role.Text}",
            style: ButtonStyle.Primary);
    }

    private async Task CreateRoleSelectCommandAsync(bool saveChanges = true)
    {
        _logger.LogInformation("Создаём команду роли...");

        if (_config == null)
        {
            _logger.LogWarning("Попытка создать команду роли без конфига.");
            return;
        }

        if (_discordService.App == null)
        {
            _logger.LogWarning("Попытка создать команду роли без апп.");
            return;
        }

        // ну и как мне опции засунуть?
        // ApplicationCommandService<SlashCommandContext> applicationCommandService = new();
        // applicationCommandService.AddSlashCommand("roling", "чек роли", () =>
        // {
        //     
        // }, Permissions.Administrator);

        ApplicationCommand interaction = await _discordService.DiscordClient.Rest.CreateGlobalApplicationCommandAsync(
            _discordService.App.Id,
            new SlashCommandProperties("roling", "чек роли")
                .WithDefaultGuildUserPermissions(Permissions.Administrator)
                .WithOptions([
                    new ApplicationCommandOptionProperties(ApplicationCommandOptionType.Role, "role", "роль"),
                    new ApplicationCommandOptionProperties(ApplicationCommandOptionType.String, "text", "залупа")
                ]));

        _config.CheckRoleInteractionId = interaction.Id;

        if (saveChanges)
        {
            await SaveConfigAsync();
        }
    }

    private async Task CreateCreationCommand(bool saveChanges = true)
    {
        _logger.LogInformation("Создаём команду создания...");

        if (_config == null)
        {
            _logger.LogWarning("Попытка создать создание без конфига.");
            return;
        }

        if (_discordService.App == null)
        {
            _logger.LogWarning("Попытка создать создание без апп.");
            return;
        }

        ApplicationCommand created = await _discordService.DiscordClient.Rest.CreateGlobalApplicationCommandAsync(
            _discordService.App.Id,
            new SlashCommandProperties("here", "насри тут, дружище")
                .WithDefaultGuildUserPermissions(Permissions.Administrator));

        _config.CreateInteractionId = created.Id;

        if (saveChanges)
        {
            await SaveConfigAsync();
        }
    }

    private async Task<DiscordRoleApplierConfig> LoadConfigAsync()
    {
        if (File.Exists(PersistStoragePath))
            return JsonSerializerExtensions.Deserialize<DiscordRoleApplierConfig>(
                await File.ReadAllTextAsync(PersistStoragePath),
                DiscordRoleApplierConfigSerializerContext.Default);

        return new DiscordRoleApplierConfig();
    }

    [UnconditionalSuppressMessage("Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "ну там стоит контект в опции")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "ну там стоит контект в опции")]
    private Task SaveConfigAsync()
    {
        return File.WriteAllTextAsync(PersistStoragePath, JsonSerializer.Serialize(_config, new JsonSerializerOptions()
        {
            WriteIndented = true,
            TypeInfoResolver = DiscordRoleApplierConfigSerializerContext.Default
        }));
    }
}