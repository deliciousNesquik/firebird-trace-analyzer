using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace FirebirdTraceAnalyzer.Core;

/// <summary>
/// A class derived from ObservableCollection, optimized for high-performance handling of large collections and batch insertions.
/// </summary>
/// <typeparam name="T">The type of elements in the collection.</typeparam>
public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    /// <summary>
    /// Replaces the entire collection with the specified items.
    /// </summary>
    /// <param name="items">The items to replace the collection with.</param>
    /// <example>
    ///     <code>
    ///         var collection = new RangeObservableCollection&lt;string&gt; { "item1", "item2" };
    ///         collection.ReplaceRange(new[] { "newItem1", "newItem2" });
    ///     </code>
    /// </example>
    public void ReplaceRange(IEnumerable<T>? items)
    {
        if (items == null)
            return;

        CheckReentrancy();

        var newItems = items.ToList();

        Items.Clear();

        foreach (var item in newItems)
            Items.Add(item);

        OnCollectionChanged(
            new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset));

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
    }
}