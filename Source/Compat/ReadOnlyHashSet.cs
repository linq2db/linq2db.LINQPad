#if !NET5_0_OR_GREATER
namespace System.Collections.Generic;

internal sealed class ReadOnlyHashSet<T> : IReadOnlySet<T>
{
	private readonly ISet<T> _set;

	public ReadOnlyHashSet(ISet<T> set)
	{
		_set = set;
	}

	int IReadOnlyCollection<T>.Count => _set.Count;

	bool IReadOnlySet<T>.Contains(T item) => _set.Contains(item);

	IEnumerator<T> IEnumerable<T>.GetEnumerator() => _set.GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_set).GetEnumerator();

	bool IReadOnlySet<T>.IsProperSubsetOf(IEnumerable<T> other) => _set.IsProperSubsetOf(other);

	bool IReadOnlySet<T>.IsProperSupersetOf(IEnumerable<T> other) => _set.IsProperSupersetOf(other);

	bool IReadOnlySet<T>.IsSubsetOf(IEnumerable<T> other) => _set.IsSubsetOf(other);

	bool IReadOnlySet<T>.IsSupersetOf(IEnumerable<T> other) => _set.IsSupersetOf(other);

	bool IReadOnlySet<T>.Overlaps(IEnumerable<T> other) => _set.Overlaps(other);

	bool IReadOnlySet<T>.SetEquals(IEnumerable<T> other) => _set.SetEquals(other);

	
}
#endif
