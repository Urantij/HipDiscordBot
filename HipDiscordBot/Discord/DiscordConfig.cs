using System.ComponentModel.DataAnnotations;

namespace HipDiscordBot.Discord;

public class DiscordConfig
{
    [Required] public required ulong ChannelId { get; set; }
    [Required] public required string Token { get; set; }
    public string? Proxy { get; set; }
}