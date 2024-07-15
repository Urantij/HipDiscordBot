using System.Text.Json;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using HipDiscordBot.Discord;

namespace HipDiscordBot.Work;

public class SavedRole(string text, ulong roleId)
{
    public string Text { get; set; } = text;
    public ulong RoleId { get; set; } = roleId;
}

public class DiscordRoleApplierConfig
{
    public ulong? ChannelId { get; set; }
    public ulong? MessageId { get; set; }
    public ulong? CreateInteractionId { get; set; }
    public ulong? CheckRoleInteractionId { get; set; }

    public ICollection<SavedRole>? Roles { get; set; }
}

public class DiscordRoleApplierWorker : IHostedService
{
    private const string _persistStoragePath = "DiscordRoleApplierWorker.json";

    private const string _mainCommandCustomId = "roleinteraction";

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
        _discordService.DiscordClient.ComponentInteractionCreated += Interacted;

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

            DiscordChannel channel = await _discordService.DiscordClient.GetChannelAsync(_config.ChannelId.Value);

            await CreateMainCommandMessageAsync(channel);
        }
        else
        {
            if (_config.ChannelId == null)
            {
                _logger.LogWarning("А канала нет...");
                await CreateCreationCommand();
                return;
            }

            DiscordChannel channel = await _discordService.DiscordClient.GetChannelAsync(_config.ChannelId.Value);

            try
            {
                await channel.GetMessageAsync(_config.MessageId.Value);
            }
            catch (NotFoundException e)
            {
                _logger.LogWarning("Сообщение не найдено, создаём...");

                await CreateMainCommandMessageAsync(channel);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _discordService.DiscordClient.ComponentInteractionCreated -= Interacted;

        return Task.CompletedTask;
    }

    private async Task Interacted(DiscordClient client, ComponentInteractionCreateEventArgs args)
    {
        if (_config == null)
            return;

        DiscordInteraction interaction = args.Interaction;

        try
        {
            if (interaction.Data.Id == _config.CheckRoleInteractionId)
            {
                await HandleRoleCheckCommandAsync(interaction);
            }
            else if (interaction.Data.Id == _config.CreateInteractionId)
            {
                _logger.LogInformation("Создаём основную команду...");

                await HandleCreateCommandAsync(interaction);
            }
            else if (args.Message.Id == _config.MessageId && interaction.Data.CustomId.StartsWith(_mainCommandCustomId))
            {
                _logger.LogDebug("Работаем с командой role...");

                await HandleRoleButtonAsync(interaction);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Не удалось...");
        }
    }

    private async Task HandleRoleCheckCommandAsync(DiscordInteraction discordInteraction)
    {
        if (_config == null)
        {
            _logger.LogWarning($"{nameof(HandleRoleCheckCommandAsync)} конфиг нул");
            return;
        }

        if (!discordInteraction.Data.Options.Any())
        {
            await discordInteraction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("И че?").AsEphemeral());
            return;
        }

        if (discordInteraction.Data.Options.First().Value is not ulong roleId)
        {
            await discordInteraction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                new DiscordInteractionResponseBuilder().WithContent("клево ты придумал").AsEphemeral());
            return;
        }

        string? text = discordInteraction.Data.Options.ElementAtOrDefault(1)?.Value as string;

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
                await discordInteraction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource,
                    new DiscordInteractionResponseBuilder().WithContent("а текст кто писать будет").AsEphemeral());
                return;
            }

            _logger.LogDebug("Добавляем роль...");

            _config.Roles ??= new List<SavedRole>();

            _config.Roles.Add(new SavedRole(text, roleId));
        }

        await discordInteraction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AsEphemeral());

        await SaveConfigAsync();
        await UpdateMainCommandMessageAsync();

        await discordInteraction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent("Сделана!"));
    }

    private async Task HandleCreateCommandAsync(DiscordInteraction discordInteraction)
    {
        if (_config == null)
        {
            _logger.LogWarning($"{nameof(HandleCreateCommandAsync)} конфиг нул");
            return;
        }

        await discordInteraction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AsEphemeral());

        await _discordService.DiscordClient.DeleteGlobalApplicationCommandAsync(_config.CreateInteractionId
            .Value);
        _config.CreateInteractionId = null;

        DiscordChannel channel = await _discordService.DiscordClient.GetChannelAsync(discordInteraction.ChannelId);

        try
        {
            await CreateMainCommandMessageAsync(channel, saveChanges: false);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Не удалось создание сообщение...");

            _config.ChannelId = discordInteraction.ChannelId;
            await SaveConfigAsync();
            return;
        }

        await SaveConfigAsync();

        await discordInteraction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent("Сделана!"));
    }

    private async Task HandleRoleButtonAsync(DiscordInteraction discordInteraction)
    {
        if (_config == null)
        {
            _logger.LogWarning($"{nameof(HandleRoleButtonAsync)} конфиг нул");
            return;
        }

        await discordInteraction.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource,
            new DiscordInteractionResponseBuilder().AsEphemeral());

        if (!int.TryParse(discordInteraction.Data.CustomId[_mainCommandCustomId.Length..], out int index))
        {
            _logger.LogError("Кривой customid {id}", discordInteraction.Data.CustomId);

            await discordInteraction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent("пошёл нахуй"));
            return;
        }

        SavedRole? saveRole = _config.Roles?.ElementAtOrDefault(index);

        if (saveRole == null)
        {
            _logger.LogError("Кривой индекс {id}", index);

            await discordInteraction.EditOriginalResponseAsync(
                new DiscordWebhookBuilder().WithContent("сори братик всё сломано"));
            return;
        }

        DiscordRole? role = discordInteraction.Guild?.GetRole(saveRole.RoleId);
        if (role is null)
        {
            _logger.LogWarning("Роль не найдена.");

            await discordInteraction.EditOriginalResponseAsync(
                new DiscordWebhookBuilder().WithContent("сори братик всё сломано"));
            return;
        }

        if (discordInteraction.User is not DiscordMember member)
        {
            _logger.LogDebug("Не нашли мембера...");

            await discordInteraction.EditOriginalResponseAsync(
                new DiscordWebhookBuilder().WithContent("сори братик всё сломано"));
            member = await discordInteraction.Guild.GetMemberAsync(discordInteraction.User.Id);
        }

        bool exist = member.Roles.Any(r => r.Id == role.Id);

        if (exist)
        {
            await member.RevokeRoleAsync(role, "Попросил!");
        }
        else
        {
            await member.GrantRoleAsync(role, "Попросил!");
        }

        await discordInteraction.EditOriginalResponseAsync(
            new DiscordWebhookBuilder().WithContent("Всё сделано в лучшем виде, приходите ещё."));
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

        DiscordChannel channel = await _discordService.DiscordClient.GetChannelAsync(_config.ChannelId.Value);
        DiscordMessage message = await channel.GetMessageAsync(_config.MessageId.Value);

        // ЭЭ, пачему то не работает. BadRequest
        // await message.ModifyAsync(b => MakeMainCommandMessage(b, _config.Roles ?? []));

        await message.DeleteAsync();
        await CreateMainCommandMessageAsync(channel);
    }

    private async Task CreateMainCommandMessageAsync(DiscordChannel channel, bool saveChanges = true)
    {
        _logger.LogInformation("Создаём основное сообщение...");

        if (_config == null)
        {
            _logger.LogWarning("Попытка создать сообщение без конфига.");
            return;
        }

        DiscordMessage message;
        try
        {
            message = await channel.SendMessageAsync(b => MakeMainCommandMessage(b, _config.Roles ?? []));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Не удалось создать сообщение.");
            return;
        }

        _config.ChannelId = channel.Id;
        _config.MessageId = message.Id;

        if (saveChanges)
        {
            await SaveConfigAsync();
        }
    }

    private static void MakeMainCommandMessage(DiscordMessageBuilder b, ICollection<SavedRole> roles)
    {
        b.WithContent("кто нажал тот сдохнет");

        if (roles.Count > 0)
        {
            b.AddComponents(roles.Select((role, i) => new DiscordButtonComponent(ButtonStyle.Primary,
                _mainCommandCustomId + i.ToString(), $"хочу знать когда {role.Text}") as DiscordComponent).ToArray());
        }
    }

    private async Task CreateRoleSelectCommandAsync(bool saveChanges = true)
    {
        _logger.LogInformation("Создаём команду роли...");

        if (_config == null)
        {
            _logger.LogWarning("Попытка создать команду роли без конфига.");
            return;
        }

        DiscordApplicationCommand interaction = await _discordService.DiscordClient.CreateGlobalApplicationCommandAsync(
            new DiscordApplicationCommand(
                "roling", "чек роли", defaultMemberPermissions: Permissions.Administrator, options:
                [
                    new DiscordApplicationCommandOption("role", "роль", ApplicationCommandOptionType.Role),
                    new DiscordApplicationCommandOption("text", "залупа", ApplicationCommandOptionType.String)
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

        DiscordApplicationCommand created =
            await _discordService.DiscordClient.CreateGlobalApplicationCommandAsync(
                new DiscordApplicationCommand(
                    "here", "насри тут, дружище", defaultMemberPermissions: Permissions.Administrator));

        _config.CreateInteractionId = created.Id;

        if (saveChanges)
        {
            await SaveConfigAsync();
        }
    }

    private async Task<DiscordRoleApplierConfig> LoadConfigAsync()
    {
        if (File.Exists(_persistStoragePath))
            return JsonSerializer.Deserialize<DiscordRoleApplierConfig>(
                await File.ReadAllTextAsync(_persistStoragePath));

        return new DiscordRoleApplierConfig();
    }

    private Task SaveConfigAsync()
    {
        return File.WriteAllTextAsync(_persistStoragePath, JsonSerializer.Serialize(_config, new JsonSerializerOptions()
        {
            WriteIndented = true
        }));
    }
}