using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Options;

namespace HipDiscordBot.Discord;

public class DiscordService : IHostedService
{
    public DiscordClient DiscordClient { get; }
    private readonly ulong _channelId;

    public DiscordService(IOptions<DiscordConfig> config)
    {
        _channelId = config.Value.ChannelId;

        DiscordClient = new DiscordClient(new DiscordConfiguration()
        {
            Token = config.Value.Token,
            TokenType = TokenType.Bot,
            Intents = DiscordIntents.AllUnprivileged | DiscordIntents.GuildIntegrations
        });
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return DiscordClient.ConnectAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return DiscordClient.DisconnectAsync();
    }

    public async Task SendMessageAsync(string text)
    {
        DiscordChannel channel = await DiscordClient.GetChannelAsync(_channelId);

        await channel.SendMessageAsync(text);
    }
}