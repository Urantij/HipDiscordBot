namespace HipDiscordBot.Work;

public class SavedRole(string text, ulong roleId)
{
    public string Text { get; set; } = text;
    public ulong RoleId { get; set; } = roleId;
}

public class DiscordRoleApplierConfig
{
    /// <summary>
    /// Айди канала, где должно быть сообщение с кнопками
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    /// Айди сообщения, на котором висят кнопки
    /// </summary>
    public ulong? MessageId { get; set; }

    public ulong? CreateInteractionId { get; set; }
    public ulong? CheckRoleInteractionId { get; set; }

    public ICollection<SavedRole>? Roles { get; set; }
}