namespace Typeshift.Samples.Web;

// The chats of the guide's scenario: eight of the sample customer's, and one of another customer
internal sealed class SampleChats : IChats
{
    private static readonly Dictionary<int, int> Owners = new()
    {
        [41] = 7,
        [42] = 7,
        [43] = 7,
        [44] = 7,
        [45] = 7,
        [46] = 7,
        [47] = 7,
        [48] = 7,
        [99] = 8
    };

    public Task<bool> BelongsToAsync(int chatId, int customer, CancellationToken ct) =>
        Task.FromResult(Owners.TryGetValue(chatId, out var owner) && owner == customer);
}
