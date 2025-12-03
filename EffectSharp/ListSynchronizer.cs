using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

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

            // Calculate lengths of common prefix and suffix and make them longer by moving side elements
            var (commonPrefixLength, commonSuffixLength) = MakeCommonPrefixSuffix(
                source,
                target,
                keySelector,
                keyComparer);
            int commonLengthSum = commonPrefixLength + commonSuffixLength;
            if (commonLengthSum == source.Count && commonLengthSum == target.Count)
            {
                // Source and target are identical; no action needed
                return;
            }

            if (commonLengthSum != source.Count)
            {
                var targetKeyMap = BuildKeyToIndexMap(target, commonPrefixLength, target.Count - commonSuffixLength,
                    keySelector, keyComparer);

                // Remove elements not in target from source in descending index order (to avoid index shifting)
                for (int i = source.Count - commonSuffixLength - 1; i >= commonPrefixLength; i--)
                {
                    var sourceItem = source[i];
                    var sourceKey = keySelector(sourceItem);
                    if (!targetKeyMap.ContainsKey(sourceKey))
                    {
                        source.RemoveAt(i);
                    }
                }
                // Recalculate common prefix and suffix lengths after removals
                (commonPrefixLength, commonSuffixLength) = MakeCommonPrefixSuffix(
                    source,
                    target,
                    keySelector,
                    keyComparer,
                    commonPrefixLength,
                    commonSuffixLength);
                commonLengthSum = commonPrefixLength + commonSuffixLength;
            }

            IEnumerable<int> insertIndices;

            if (commonLengthSum != source.Count && commonLengthSum != target.Count)
            {
                var sourceKeyMap = BuildKeyToIndexMap(source, commonPrefixLength, source.Count - commonSuffixLength,
                    keySelector, keyComparer);
                // Prepare target keys that exist in source
                var targetToSource = new int[source.Count - commonLengthSum];
                int[] insertIndicesArr = null;
                if (target.Count > source.Count)
                    insertIndices = insertIndicesArr = new int[target.Count - source.Count];
                else
                    insertIndices = Enumerable.Empty<int>();
                int filledCount = 0, insertCount = 0;
                for (int i = commonPrefixLength; i < target.Count - commonSuffixLength; i++)
                {
                    var targetItem = target[i];
                    var targetKey = keySelector(targetItem);
                    if (sourceKeyMap.TryGetValue(targetKey, out var sourceIndex))
                    {
                        targetToSource[filledCount++] = sourceIndex;
                    }
                    else
                    {
                        insertIndicesArr[insertCount++] = i;
                    }
                }

                // Move disordered elements to their target positions
                MoveDisorderedElements(source, targetToSource,
                    commonPrefixLength, source.Count - commonSuffixLength);
            }
            else
            {
                insertIndices = Enumerable.Range(commonPrefixLength, target.Count - commonLengthSum);
            }

            // Insert new elements from target into source
            if (commonLengthSum != target.Count)
                InsertNewElements(source, target, insertIndices);
        }

        #region Utilities for SyncUnique
        /// <summary>
        /// Calculate lengths of common prefix and suffix and make them longer by moving side elements
        /// </summary>
        private static (int, int) MakeCommonPrefixSuffix<T, K>(
            ObservableCollection<T> source,
            IList<T> target,
            Func<T, K> keySelector,
            IEqualityComparer<K> keyComparer,
            int baseCommonPrefix = 0,
            int baseCommonSuffix = 0)
        {
            int commonPrefixLength = baseCommonPrefix;
            int commonSuffixLength = baseCommonSuffix;
            int minLength = Math.Min(source.Count, target.Count);
            while (commonPrefixLength + commonSuffixLength < minLength)
            {
                while (commonPrefixLength + commonSuffixLength < minLength &&
                       keyComparer.Equals(
                           keySelector(source[commonPrefixLength]),
                           keySelector(target[commonPrefixLength])))
                {
                    commonPrefixLength++;
                }
                while (commonPrefixLength + commonSuffixLength < minLength &&
                       keyComparer.Equals(
                           keySelector(source[source.Count - 1 - commonSuffixLength]),
                           keySelector(target[target.Count - 1 - commonSuffixLength])))
                {
                    commonSuffixLength++;
                }
                if (commonPrefixLength + commonSuffixLength == minLength)
                    break;
                if (keyComparer.Equals(
                       keySelector(source[commonPrefixLength]),
                       keySelector(target[target.Count - commonSuffixLength - 1])))
                {
                    source.Move(commonPrefixLength, source.Count - commonSuffixLength - 1);
                    commonSuffixLength++;
                }
                else if (keyComparer.Equals(
                       keySelector(source[source.Count - commonSuffixLength - 1]),
                       keySelector(target[commonPrefixLength])))
                {
                    source.Move(source.Count - commonSuffixLength - 1, commonPrefixLength);
                    commonPrefixLength++;
                }
                else
                {
                    break;
                }
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
                // Use Add to ensure no duplicate key
                map.Add(key, i);
            }

            return map;
        }

        /// <summary>
        /// Move disordered elements in source to match target
        /// </summary>
        private static void MoveDisorderedElements<T>(
            ObservableCollection<T> source,
            int[] targetToSource,
            int startIndex,
            int endIndex)
        {
            if (targetToSource.Length <= 1)
                return;

            var sourceToTarget = new int[targetToSource.Length];
            for (int  i = 0; i < targetToSource.Length; i++)
            {
                sourceToTarget[targetToSource[i] - startIndex] = i;
            }
            int lPtr = startIndex, rPtr = endIndex - 1;
            var lSource = targetToSource[lPtr - startIndex];
            var rSource = targetToSource[rPtr - startIndex];
            while (rPtr - lPtr > 0)
            {
                if (lSource == lPtr)
                {
                    lPtr++;
                    lSource = targetToSource[lPtr - startIndex];
                    continue;
                }
                if (rSource == rPtr)
                {
                    rPtr--;
                    rSource = targetToSource[rPtr - startIndex];
                    continue;
                }
                var lDistance = lSource - lPtr;
                var rDistance = rPtr - rSource;
                // Heuristic optimization: move the element with a longer distance
                // to reduce the total number of moves
                if (rDistance > lDistance)
                {
                    source.Move(rSource, rPtr);
                    UpdateIndexMap(
                        targetToSource,
                        sourceToTarget,
                        rSource,
                        rPtr,
                        startIndex,
                        endIndex);
                    rPtr--;
                }
                else
                {
                    source.Move(lSource, lPtr);
                    UpdateIndexMap(
                        targetToSource,
                        sourceToTarget,
                        lSource,
                        lPtr,
                        startIndex,
                        endIndex);
                    lPtr++;
                }
                lSource = targetToSource[lPtr - startIndex];
                rSource = targetToSource[rPtr - startIndex];
            }
        }

        /// <summary>
        /// Update index map after moving an element
        /// </summary>
        private static void UpdateIndexMap(
            int[] targetToSource,
            int[] sourceToTarget,
            int fromSourceIndex,
            int toSourceIndex,
            int startIndex,
            int endIndex)
        {
            var movedTargetIndex = sourceToTarget[fromSourceIndex - startIndex];
            if (fromSourceIndex < toSourceIndex)
            {
                for (int j = fromSourceIndex + 1; j <= toSourceIndex; j++)
                {
                    var shiftedTargetIndex = sourceToTarget[j - startIndex];
                    targetToSource[shiftedTargetIndex]--;
                    sourceToTarget[j - startIndex - 1] = shiftedTargetIndex;
                }
            }
            else
            {
                for (int j = fromSourceIndex - 1; j >= toSourceIndex; j--)
                {
                    var shiftedTargetIndex = sourceToTarget[j - startIndex];
                    targetToSource[shiftedTargetIndex]++;
                    sourceToTarget[j - startIndex + 1] = shiftedTargetIndex;
                }
            }
            sourceToTarget[toSourceIndex - startIndex] = movedTargetIndex;
        }

        /// <summary>
        /// Insert new elements from target into source
        /// </summary>
        private static void InsertNewElements<T>(
            ObservableCollection<T> source,
            IList<T> target,
            IEnumerable<int> insertIndices)
        {
            foreach (var insertIndex in insertIndices)
            {
                source.Insert(insertIndex, target[insertIndex]);
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
            var (commonPrefixLength, commonSuffixLength) = MakeCommonPrefixSuffix(
                source,
                target,
                keySelector,
                keyComparer);
            int commonLengthSum = commonPrefixLength + commonSuffixLength;
            if (commonLengthSum == source.Count && commonLengthSum == target.Count)
            {
                // Source and target are identical; no action needed
                return;
            }

            if (commonLengthSum != source.Count)
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

                // Recalculate common prefix and suffix lengths after removals
                (commonPrefixLength, commonSuffixLength) = MakeCommonPrefixSuffix(
                    source,
                    target,
                    keySelector,
                    keyComparer,
                    commonPrefixLength,
                    commonSuffixLength);
                commonLengthSum = commonPrefixLength + commonSuffixLength;
            }

            IEnumerable<int> insertIndices;

            if (commonLengthSum != source.Count && commonLengthSum != target.Count)
            {
                var sourceKeyMap = BuildKeyToIndexQueueMap(source, commonPrefixLength, source.Count - commonSuffixLength,
                    keySelector, keyComparer);
                // Prepare target keys that exist in source
                var targetToSource = new int[source.Count - commonLengthSum];
                int[] insertIndicesArr = null;
                if (target.Count > source.Count)
                    insertIndices = insertIndicesArr = new int[target.Count - source.Count];
                else
                    insertIndices = Enumerable.Empty<int>();
                int filledCount = 0, insertCount = 0;
                for (int i = commonPrefixLength; i < target.Count - commonSuffixLength; i++)
                {
                    var targetItem = target[i];
                    var targetKey = keySelector(targetItem);
                    if (sourceKeyMap.TryGetValue(targetKey, out var sourceQueue) && sourceQueue.Count != 0)
                    {
                        targetToSource[filledCount++] = sourceQueue.Dequeue();
                    }
                    else
                    {
                        insertIndicesArr[insertCount++] = i;
                    }
                }

                // Move disordered elements to their target positions
                MoveDisorderedElements(source, targetToSource,
                    commonPrefixLength, source.Count - commonSuffixLength);
            }
            else
            {
                insertIndices = Enumerable.Range(commonPrefixLength, target.Count - commonLengthSum);
            }

            // Insert new elements from target into source
            if (commonLengthSum != target.Count)
                InsertNewElements(source, target, insertIndices);
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
