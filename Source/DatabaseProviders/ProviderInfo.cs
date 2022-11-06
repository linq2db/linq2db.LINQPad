namespace LinqToDB.LINQPad;

/// <summary>
/// Database provider descriptor for specific database.
/// </summary>
/// <param name="Name">Provider identifier (e.g. value from <see cref="ProviderName"/> class).</param>
/// <param name="DisplayName">Provider display name in settings dialog.</param>
internal sealed record ProviderInfo(string Name, string DisplayName);
