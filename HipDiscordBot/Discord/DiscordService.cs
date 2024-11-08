using System.Net;
using HipDiscordBot.Work;
using Microsoft.Extensions.Options;
using NetCord;
using NetCord.Gateway;
using NetCord.Gateway.WebSockets;
using NetCord.Rest;

namespace HipDiscordBot.Discord;

public class DiscordService : IHostedService
{
    public GatewayClient DiscordClient { get; }
    private readonly ulong _channelId;

    public CurrentApplication? App { get; private set; }

    public DiscordService(IOptions<DiscordConfig> config, ILogger<DiscordService> logger)
    {
        _channelId = config.Value.ChannelId;

        WebProxy? proxy = null;
        if (config.Value.Proxy != null)
        {
            logger.LogInformation("Юзаем прокси");
            proxy = new WebProxy(config.Value.Proxy);
        }

        RestClientConfiguration? restClientConfiguration = null;
        if (proxy != null)
        {
            restClientConfiguration = new RestClientConfiguration()
            {
                RequestHandler = new RestRequestHandler(new HttpClientHandler()
                {
                    Proxy = proxy
                })
            };
        }

        IWebSocketConnectionProvider? webSocketConnectionProvider = null;
        if (proxy != null)
        {
            webSocketConnectionProvider = new MyWebSocketConnectionProvider(proxy);
        }

        DiscordClient = new GatewayClient(new BotToken(config.Value.Token), new GatewayClientConfiguration()
        {
            RestClientConfiguration = restClientConfiguration,
            WebSocketConnectionProvider = webSocketConnectionProvider,
            Intents = GatewayIntents.AllNonPrivileged | GatewayIntents.GuildIntegrations
        });
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await DiscordClient.StartAsync(cancellationToken: cancellationToken);

        App = await DiscordClient.Rest.GetCurrentApplicationAsync(cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return DiscordClient.CloseAsync(cancellationToken: cancellationToken);
    }

    public Task SendMessageAsync(string text)
    {
        return DiscordClient.Rest.SendMessageAsync(_channelId, new MessageProperties()
        {
            Content = text
        });
    }
}