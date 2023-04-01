using System.Data.Common;
using System.Globalization;
using System.Numerics;
using FirebirdSql.Data.FirebirdClient;
using FirebirdSql.Data.Types;

namespace LinqToDB.LINQPad;

internal sealed class FirebirdProvider : DatabaseProviderBase
{
	private static readonly IReadOnlyList<ProviderInfo> _providers = new ProviderInfo[]
	{
		new (ProviderName.Firebird, "Firebird"),
	};

	public FirebirdProvider()
		: base(ProviderName.Firebird, "Firebird", _providers)
	{
	}

	public override void ClearAllPools(string providerName)
	{
		FbConnection.ClearAllPools();
	}

	public override DateTime? GetLastSchemaUpdate(ConnectionSettings settings)
	{
		// no information in schema
		return null;
	}

	public override DbProviderFactory GetProviderFactory(string providerName)
	{
		return FirebirdClientFactory.Instance;
	}

	public override IEnumerable<(Type type, TypeRenderer renderer)> GetTypeRenderers()
	{
		yield return (typeof(FbDecFloat)     , RenderFbDecFloat);
		yield return (typeof(FbZonedTime)    , RenderFbZonedTime);
		yield return (typeof(FbZonedDateTime), RenderFbZonedDateTime);
	}

	private static void RenderFbDecFloat(ref object? value)
	{
		// type reders as {Coefficient}E{Exponent} which is not very noice
		var typedValue = (FbDecFloat)value!;
		var isNegative = typedValue.Coefficient < 0;
		var strValue = (isNegative ? BigInteger.Negate(typedValue.Coefficient) : typedValue.Coefficient).ToString(CultureInfo.InvariantCulture);

		// semi-localized rendering...
		if (typedValue.Exponent < 0)
		{
			var exp = -typedValue.Exponent;
			if (exp < strValue.Length)
				strValue = strValue.Insert(strValue.Length - exp, CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
			else if (exp == strValue.Length)
				strValue = $"0{CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator}{strValue}";
			else // Exponent > len(Coefficient)
				strValue = $"0{CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator}{new string('0', exp - strValue.Length)}{strValue}";
		}
		else if (typedValue.Exponent > 0)
			strValue = $"{strValue}{new string('0', typedValue.Exponent)}";

		value = isNegative ? $"-{strValue}" : strValue;
	}

	private static void RenderFbZonedTime(ref object? value)
	{
		// type already implements rendering of all data
		value = value!.ToString();
	}

	private static void RenderFbZonedDateTime(ref object? value)
	{
		// type already implements rendering of all data
		value = value!.ToString();
	}
}
