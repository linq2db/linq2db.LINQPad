namespace System.Collections.Generic;

internal static class ReadOnlySetExtensions
{
	public static IReadOnlySet<T> AsReadOnly<T>(this HashSet<T> set)
	{
#if NET5_0_OR_GREATER
		return set;
#else
		return new ReadOnlyHashSet<T>(set);
#endif
	}
}
