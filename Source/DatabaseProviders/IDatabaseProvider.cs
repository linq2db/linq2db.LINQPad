using LINQPad.Extensibility.DataContext;

namespace LinqToDB.LINQPad;

/// <summary>
/// Provides database provider abstraction.
/// </summary>
internal interface IDatabaseProvider
{
	/// <summary>
	/// Gets database name (generic <see cref="ProviderName"/> value).
	/// </summary>
	string Database { get; }

	/// <summary>
	/// Gets database provider name for UI.
	/// </summary>
	string Description { get; }

	/// <summary>
	/// When <c>true</c>, database provider supports secondary connection for database schema population.
	/// </summary>
	bool SupportsSecondaryConnection { get; }

	/// <summary>
	/// Returns provider factory name. Used to initialize <see cref="IDatabaseInfo.Provider"/>, but probably we don't need it at all as we override
	/// <see cref="DataContextDriver.GetIDbConnection(IConnectionInfo)"/>.
	/// </summary>
	string? GetProviderFactoryName(string providerName);

	/// <summary>
	/// Release all connections.
	/// </summary>
	void ClearAllPools(string providerName);

	/// <summary>
	/// Returns last schema update time.
	/// </summary>
	DateTime? GetLastSchemaUpdate(ConnectionSettings settings);

	/// <summary>
	/// Returns additional reference assemblies for dynamic model compilation (except main provider assembly).
	/// </summary>
	IReadOnlyCollection<Assembly> GetAdditionalReferences(string providerName);

	/// <summary>
	/// List of supported provider names for provider.
	/// </summary>
	IReadOnlyList<ProviderInfo> Providers { get; }

	/// <summary>
	/// When <c>true</c>, connection settings UI doesn't allow user to select provider type.
	/// <see cref="GetProviderByConnectionString(string)"/> method will be used to infer provider automatically.
	/// Note that provider selection also unavailable when there is only one provider supported by database.
	/// </summary>
	bool AutomaticProviderSelection { get; }

	/// <summary>
	/// Tries to infer provider by database connection string.
	/// </summary>
	ProviderInfo? GetProviderByConnectionString(string connectionString);

	/// <summary>
	/// Returns <c>true</c>, if specified provider for current database provider supports provider assembly path configuration.
	/// </summary>
	bool IsProviderPathSupported(string providerName);

	/// <summary>
	/// If provider supports assembly path configuration, method
	/// returns help text for configuration UI to help user locate and/or install provider.
	/// </summary>
	string? GetProviderAssemblyName(string providerName);

	/// <summary>
	/// If provider supports assembly path configuration, method could return URL to provider download page.
	/// </summary>
	string? GetProviderDownloadUrl(string? providerName);

	/// <summary>
	/// If provider supports assembly path configuration (<see cref="IsProviderPathSupported(string)"/>), method tries to return default path to provider assembly,
	/// but only if assembly exists on specified path.
	/// </summary>
	string? TryGetDefaultPath(string providerName);

	/// <summary>
	/// If provider supports assembly path configuration (<see cref="IsProviderPathSupported(string)"/>), method
	/// performs provider factory registration to allow Linq To DB locate provider assembly.
	/// </summary>
	void RegisterProviderFactory(string providerName, string providerPath);
}
