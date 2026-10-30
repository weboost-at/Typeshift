namespace Typeshift.Samples.Web;

// The host's own record of its chats: which customer each conversation id belongs to
internal interface IChats
{
    Task<bool> BelongsToAsync(int chatId, int customer, CancellationToken ct);
}
