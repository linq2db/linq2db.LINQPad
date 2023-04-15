namespace LinqToDB.LINQPad;

/// <summary>
/// Database provider descriptor for specific database.
/// </summary>
/// <param name="Name">Provider identifier (e.g. value from <see cref="ProviderName"/> class).</param>
/// <param name="DisplayName">Provider display name in settings dialog.</param>
/// <param name="IsDefault">When set, specified provider dialect will be selected automatically.</param>
internal sealed record ProviderInfo(string Name, string DisplayName, bool IsDefault = false);
