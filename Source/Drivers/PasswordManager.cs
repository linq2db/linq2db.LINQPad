using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using LINQPad;

namespace LinqToDB.LINQPad
{
	internal static partial class PasswordManager
	{
		private static readonly Regex _tokenReplacer = new(@"\{pm:([^\}]+)\}", RegexOptions.Compiled);

		[return: NotNullIfNotNull(nameof(value))]
		public static string? ResolvePasswordManagerFields(string? value)
		{
			if (value == null)
				return null;

			return _tokenReplacer.Replace(value, m => Util.GetPassword(m.Groups[1].Value));
		}
	}
}
