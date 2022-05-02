public static class ConIdManager
{
    private static readonly IDictionary<string, string> ConIdDictionary = new Dictionary<string, string>();
    private static Func<string, Task<string>> _fetchConId = _ => Task.FromResult("");

    public static void SetFetchFunction(Func<string, Task<string>> fetchConId) => _fetchConId = fetchConId ?? throw new ArgumentNullException(nameof(fetchConId));
    public static void SetConId(string symbol, string conId) => ConIdDictionary[symbol] = conId;

    public static async Task<string> GetConId(string symbol)
    {
        if (ConIdDictionary.ContainsKey(symbol)) return ConIdDictionary[symbol];
        var conId = await _fetchConId(symbol);
        if (string.IsNullOrWhiteSpace(conId))
            throw new ArgumentNullException(nameof(symbol), $"ConId for {symbol} is not found");
        ConIdDictionary[symbol] = conId;
        return conId;
    }
}
