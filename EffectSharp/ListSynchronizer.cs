using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EffectSharp
{
    public static class ListSynchronizer
    {
        /// <summary>
        /// Synchronizes the contents of the source collection with the target list, ensuring that the source contains
        /// exactly the unique elements from the target, matched by key. Elements are added, removed, or reordered in
        /// the source as needed to reflect the target's unique items and order.
        /// </summary>
        /// <remarks>This method modifies the source collection in place to match the target list, using
        /// the provided key selector and comparer to determine uniqueness. Only elements with distinct keys are
        /// considered; duplicates in the target are ignored. The operation preserves the order of unique elements as
        /// they appear in the target. This method is useful for efficiently updating UI-bound collections to reflect
        /// changes in a backing data source.</remarks>
        /// <typeparam name="T">The type of elements contained in the source and target collections.</typeparam>
        /// <typeparam name="K">The type of key used to identify unique elements within the collections.</typeparam>
        /// <param name="source">The ObservableCollection to be updated so that its unique elements and order match those of the target list.
        /// Cannot be null.</param>
        /// <param name="target">The list whose unique elements and order will be reflected in the source collection. Cannot be null.</param>
        /// <param name="keySelector">A function that extracts the key from each element, used to determine uniqueness. Cannot be null.</param>
        /// <param name="keyComparer">An optional equality comparer for keys. If null, the default equality comparer for the key type is used.</param>
        /// <exception cref="ArgumentNullException">Thrown if source, target, or keySelector is null.</exception>
        public static void SyncUnique<T, K>(
            this ObservableCollection<T> source,
            IList<T> target,
            Func<T, K> keySelector,
            IEqualityComparer<K> keyComparer = null)
        {
            // Validation
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (keyComparer == null)
                keyComparer = EqualityComparer<K>.Default;

            // Calculate lengths of common prefix and suffix
            var (commonPrefixLength, commonSuffixLength) = CalculateCommonPrefixSuffix(
                source,
                target,
                keySelector,
                keyComparer);
            if (commonPrefixLength == source.Count &&
                commonPrefixLength == target.Count)
            {
                // Source and target are identical; no action needed
                return;
            }

            // Build initial key-to-index mappings
            var sourceKeyMap = BuildKeyToIndexMap(source, commonPrefixLength, source.Count - commonSuffixLength,
                keySelector, keyComparer);

            if (commonPrefixLength != source.Count && commonSuffixLength != source.Count)
            {
                var targetKeyMap = BuildKeyToIndexMap(target, commonPrefixLength, target.Count - commonSuffixLength,
                    keySelector, keyComparer);

                // Remove elements not in target from source in descending index order (to avoid index shifting)
                var indicesToDelete = sourceKeyMap.Where(pair => !targetKeyMap.ContainsKey(pair.Key))
                    .Select(pair => pair.Value)
                    .OrderByDescending(idx => idx);
                foreach (var deleteIndex in indicesToDelete)
                {
                    source.RemoveAt(deleteIndex);
                }
            }

            if (commonPrefixLength != source.Count && commonSuffixLength != source.Count
                && commonPrefixLength != target.Count && commonSuffixLength != target.Count)
            {
                // Rebuild source key-to-index map after deletions
                sourceKeyMap = BuildKeyToIndexMap(source, commonPrefixLength, source.Count - commonSuffixLength,
                    keySelector, keyComparer);
                // Prepare target keys that exist in source
                var targetToSource = new List<int>();
                for (int i = commonPrefixLength; i < target.Count - commonSuffixLength; i++)
                {
                    var targetItem = target[i];
                    var targetKey = keySelector(targetItem);
                    if (sourceKeyMap.TryGetValue(targetKey, out var sourceIndex))
                    {
                        targetToSource.Add(sourceIndex);
                    }
                    else
                    {
                        continue;
                    }
                }

                // Move disordered elements to their target positions
                MoveDisorderedElements(source, targetToSource,
                    commonPrefixLength, source.Count - commonSuffixLength);
            }

            // Insert new elements from target into source
            if (commonPrefixLength != target.Count && commonSuffixLength != target.Count)
                InsertNewElements(source, target, commonPrefixLength, target.Count - commonSuffixLength,
                    source.Count - commonSuffixLength, keySelector, keyComparer);
        }

        #region Utilities for SyncUnique

        private static (int, int) CalculateCommonPrefixSuffix<T, K>(
            IList<T> source,
            IList<T> target,
            Func<T, K> keySelector,
            IEqualityComparer<K> keyComparer)
        {
            int commonPrefixLength = 0;
            int minLength = Math.Min(source.Count, target.Count);
            while (commonPrefixLength < minLength &&
                   keyComparer.Equals(
                       keySelector(source[commonPrefixLength]),
                       keySelector(target[commonPrefixLength])))
            {
                commonPrefixLength++;
            }
            int commonSuffixLength = 0;
            while (commonSuffixLength < (minLength - commonPrefixLength) &&
                   keyComparer.Equals(
                       keySelector(source[source.Count - 1 - commonSuffixLength]),
                       keySelector(target[target.Count - 1 - commonSuffixLength])))
            {
                commonSuffixLength++;
            }
            return (commonPrefixLength, commonSuffixLength);
        }

        /// <summary>
        /// Build a mapping from keys to their indices in the collection
        /// </summary>
        private static Dictionary<K, int> BuildKeyToIndexMap<T, K>(
            IList<T> collection,
            int startIndex,
            int endIndex,
            Func<T, K> keySelector,
            IEqualityComparer<K> keyComparer)
        {
            var map = new Dictionary<K, int>(keyComparer);

            for (int i = startIndex; i < endIndex; i++)
            {
                var item = collection[i];
                var key = keySelector(item);
                if (map.ContainsKey(key))
                {
                    throw new InvalidOperationException("Duplicate key detected: " + key);
                }
                map[key] = i;
            }

            return map;
        }

        /// <summary>
        /// Move disordered elements in source to match target
        /// </summary>
        private static void MoveDisorderedElements<T>(
            ObservableCollection<T> source,
            List<int> targetToSource,
            int startIndex,
            int endIndex)
        {
            var movements = new List<(int, int)>();
            for (int i = startIndex; i < endIndex; i++)
            {
                var sourceIndex = targetToSource[i - startIndex];
                foreach (var (Min, Max) in movements)
                {
                    if (sourceIndex >= Min && sourceIndex <= Max)
                    {
                        sourceIndex++;
                    }
                }
                if (sourceIndex != i)
                {
                    source.Move(sourceIndex, i);
                    movements.Add((i, sourceIndex - 1));
                }
            }
        }

        /// <summary>
        /// Insert new elements from target into source
        /// </summary>
        private static void InsertNewElements<T, K>(
            ObservableCollection<T> source,
            IList<T> target,
            int startIndex,
            int targetEndIndex,
            int sourceEndIndex,
            Func<T, K> keySelector,
            IEqualityComparer<K> keyComparer)
        {
            for (int targetIndex = startIndex; targetIndex < targetEndIndex; targetIndex++)
            {
                if (targetIndex >= sourceEndIndex)
                {
                    for (; targetIndex < targetEndIndex; targetIndex++)
                    {
                        if (sourceEndIndex == source.Count)
                        {
                            source.Add(target[targetIndex]);
                            sourceEndIndex++;
                        }
                        else
                        {
                            source.Insert(sourceEndIndex++, target[targetIndex]);
                        }
                    }
                    break;
                }
                var sourceKey = keySelector(source[targetIndex]);
                var targetKey = keySelector(target[targetIndex]);
                if (!keyComparer.Equals(sourceKey, targetKey))
                {
                    source.Insert(targetIndex, target[targetIndex]);
                    sourceEndIndex++;
                }
            }
        }

        #endregion

        /// <summary>
        /// Synchronizes the contents of the source collection with the target list by updating, inserting, removing,
        /// and reordering items so that the source matches the target, using keys selected by the specified function
        /// and allowing for duplicate keys.
        /// </summary>
        /// <remarks>This method efficiently updates the source collection to match the target list,
        /// minimizing changes by preserving common prefixes and suffixes and only modifying differing elements. The
        /// synchronization is based on keys, allowing for custom matching logic. The method is useful for keeping
        /// UI-bound collections in sync with data models or other sources. Thread safety is not guaranteed; callers
        /// should ensure appropriate synchronization if accessing collections from multiple threads.</remarks>
        /// <typeparam name="T">The type of elements contained in the source and target collections.</typeparam>
        /// <typeparam name="K">The type of key used to identify and match elements between the collections.</typeparam>
        /// <param name="source">The observable collection to be updated so that its contents match those of the target list. Cannot be null.</param>
        /// <param name="target">The list whose contents and order will be reflected in the source collection. Cannot be null.</param>
        /// <param name="keySelector">A function that extracts a key from each element, used to identify and match items between the source and
        /// target collections. Cannot be null.</param>
        /// <param name="keyComparer">An optional equality comparer used to compare keys. If null, the default equality comparer for the key type
        /// is used.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/>, <paramref name="target"/>, or <paramref name="keySelector"/> is null.</exception>
        public static void Sync<T, K>(
            this ObservableCollection<T> source,
            IList<T> target,
            Func<T, K> keySelector,
            IEqualityComparer<K> keyComparer = null)
        {
            // Validation
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (keyComparer == null)
                keyComparer = EqualityComparer<K>.Default;

            // Calculate lengths of common prefix and suffix
            var (commonPrefixLength, commonSuffixLength) = CalculateCommonPrefixSuffix(
                source,
                target,
                keySelector,
                keyComparer);
            if (commonPrefixLength == source.Count &&
                commonPrefixLength == target.Count)
            {
                // Source and target are identical; no action needed
                return;
            }

            // Build initial key-to-index mappings
            var sourceKeyMap = BuildKeyToIndexQueueMap(source, commonPrefixLength, source.Count - commonSuffixLength,
                keySelector, keyComparer);

            if (commonPrefixLength != source.Count && commonSuffixLength != source.Count)
            {
                var targetKeyMap = BuildKeyToIndexQueueMap(target, commonPrefixLength, target.Count - commonSuffixLength,
                    keySelector, keyComparer);

                // Remove elements not in or more than target from source in descending index order (to avoid index shifting)
                for (int i = source.Count - commonSuffixLength - 1; i >= commonPrefixLength; i--)
                {
                    var sourceItem = source[i];
                    var sourceKey = keySelector(sourceItem);
                    if (!targetKeyMap.TryGetValue(sourceKey, out var targetQueue) || targetQueue.Count == 0)
                    {
                        source.RemoveAt(i);
                    }
                    else
                    {
                        targetQueue.Dequeue();
                    }
                }
            }

            if (commonPrefixLength != source.Count && commonSuffixLength != source.Count
                && commonPrefixLength != target.Count && commonSuffixLength != target.Count)
            {
                // Rebuild source key-to-index map after deletions
                sourceKeyMap = BuildKeyToIndexQueueMap(source, commonPrefixLength, source.Count - commonSuffixLength,
                    keySelector, keyComparer);
                // Prepare target keys that exist in source
                var targetToSource = new List<int>();
                for (int i = commonPrefixLength; i < target.Count - commonSuffixLength; i++)
                {
                    var targetItem = target[i];
                    var targetKey = keySelector(targetItem);
                    if (sourceKeyMap.TryGetValue(targetKey, out var sourceQueue) && sourceQueue.Count != 0)
                    {
                        targetToSource.Add(sourceQueue.Dequeue());
                    }
                    else
                    {
                        continue;
                    }
                }

                // Move disordered elements to their target positions
                MoveDisorderedElements(source, targetToSource,
                    commonPrefixLength, source.Count - commonSuffixLength);
            }

            // Insert new elements from target into source
            if (commonPrefixLength != target.Count && commonSuffixLength != target.Count)
                InsertNewElements(source, target, commonPrefixLength, target.Count - commonSuffixLength,
                    source.Count - commonSuffixLength, keySelector, keyComparer);
        }

        /// <summary>
        /// Build a mapping from keys to queues of their indices in the collection
        /// </summary>
        private static Dictionary<K, Queue<int>> BuildKeyToIndexQueueMap<T, K>(
            IList<T> collection,
            int startIndex,
            int endIndex,
            Func<T, K> keySelector,
            IEqualityComparer<K> keyComparer)
        {
            var map = new Dictionary<K, Queue<int>>(keyComparer);
            for (int i = startIndex; i < endIndex; i++)
            {
                var item = collection[i];
                var key = keySelector(item);
                if (!map.TryGetValue(key, out var indexQueue))
                {
                    indexQueue = new Queue<int>();
                    map[key] = indexQueue;
                }
                indexQueue.Enqueue(i);
            }
            return map;
        }
    }
}
