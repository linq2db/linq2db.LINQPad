using System;

using CodeJam.Strings;
using Humanizer;

namespace LinqToDB.LINQPad
{
	static class Pluralization
	{
		static string GetLastWord(string str)
		{
			if (str.IsNullOrWhiteSpace())
				return string.Empty;

			var i = str.Length - 1;
			var isLower = char.IsLower(str[i]);

			while (i > 0 && char.IsLower(str[i - 1]) == isLower)
				i--;

			return str.Substring(isLower && i > 0 ? i - 1 : i);
		}

		public static string ToPlural(string str)
		{
			var word    = GetLastWord(str);
			var newWord = word.Pluralize();

			if (string.Compare(word, newWord, StringComparison.OrdinalIgnoreCase) != 0)
			{
				if (char.IsUpper(word[0]))
					newWord = char.ToUpper(newWord[0]) + newWord.Substring(1, newWord.Length - 1);

				return word == str ? newWord : str.Substring(0, str.Length - word.Length) + newWord;
			}

			return str;
		}

		public static string ToSingular(string str)
		{
			var word    = GetLastWord(str);
			var newWord = word.Singularize();

			if (string.Compare(word, newWord, StringComparison.OrdinalIgnoreCase) != 0)
			{
				if (char.IsUpper(word[0]))
					newWord = char.ToUpper(newWord[0]) + newWord.Substring(1, newWord.Length - 1);

				return word == str ? newWord : str.Substring(0, str.Length - word.Length) + newWord;
			}

			return str;
		}
	}
}
