using System.Text.Json.Serialization;

namespace HipDiscordBot.Work.Bingo;

[JsonSerializable(typeof(BingoConfig))]
public partial class BingoConfigSerializerContext : JsonSerializerContext
{
}

public class BingoConfig
{
    public ulong? AddBingoCommandId { get; set; }

    /// <summary>
    /// Айди сообщения, на котором висит кнопка бинго
    /// </summary>
    public ulong? MessageId { get; set; }

    /// <summary>
    /// Айди канала, в котором висит сообщение
    /// </summary>
    public ulong? ChannelId { get; set; }

    public int Sides { get; set; } = 5;
}