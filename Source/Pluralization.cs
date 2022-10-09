using CodeJam.Strings;
using Humanizer;

namespace LinqToDB.LINQPad;

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

		if (!string.Equals(word, newWord, StringComparison.OrdinalIgnoreCase))
		{
			if (char.IsUpper(word[0]))
				newWord = char.ToUpper(newWord[0]) + newWord.Substring(1, newWord.Length - 1);

#if LPX6
			return word == str ? newWord : string.Concat(str.AsSpan(0, str.Length - word.Length), newWord);
#else
			return word == str ? newWord : str.Substring(0, str.Length - word.Length) + newWord;
#endif
		}

		return str;
	}

	public static string ToSingular(string str)
	{
		var word    = GetLastWord(str);
		var newWord = word.Singularize();

		if (!string.Equals(word, newWord, StringComparison.OrdinalIgnoreCase))
		{
			if (char.IsUpper(word[0]))
				newWord = char.ToUpper(newWord[0]) + newWord.Substring(1, newWord.Length - 1);

#if LPX6
			return word == str ? newWord : string.Concat(str.AsSpan(0, str.Length - word.Length), newWord);
#else
			return word == str ? newWord : str.Substring(0, str.Length - word.Length) + newWord;
#endif
		}

		return str;
	}
}
