using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EffectSharp
{
    /// <summary>
    /// Helpers to synchronize an <see cref="ObservableCollection{T}"/> with a target list by keys,
    /// supporting unique and duplicate keys, minimal moves, and order preservation.
    /// </summary>
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
        public static void SyncWithUnique<T, K>(
            this ObservableCollection<T> source,
            IList<T> target,
            Func<T, K> keySelector,
            IEqualityComparer<K>? keyComparer = null)
        {
            // Validation
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (keyComparer == null)
                keyComparer = EqualityComparer<K>.Default;

            // Calculate lengths of common prefix and suffix and make them longer by moving side elements
            var (commonPrefixLength, commonSuffixLength) = ExtendCommonPrefixAndSuffix(
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

            var targetKeyMap = BuildKeyToIndexMap(target, commonPrefixLength, target.Count - commonSuffixLength,
                keySelector, keyComparer);
            if (commonLengthSum != source.Count)
            {
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

                // Recalculate common prefix & suffix length after removals
                (commonPrefixLength, commonSuffixLength) = ExtendCommonPrefixAndSuffix(
                    source,
                    target,
                    keySelector,
                    keyComparer,
                    commonPrefixLength,
                    commonSuffixLength);
                commonLengthSum = commonPrefixLength + commonSuffixLength;
            }

            if (commonLengthSum != source.Count && commonLengthSum != target.Count)
            {
                var sourceToTarget = new int[source.Count - commonLengthSum];
                for (int i = commonPrefixLength; i < source.Count - commonSuffixLength; i++)
                {
                    var sourceItem = source[i];
                    var sourceKey = keySelector(sourceItem);
                    sourceToTarget[i - commonPrefixLength] = targetKeyMap[sourceKey];
                }

                // Move disordered elements to their target positions
                MoveDisorderedElements(source, sourceToTarget, commonPrefixLength);
            }

            // Insert new elements from target into source
            if (commonLengthSum != target.Count)
                InsertNewElements(source, target, commonPrefixLength, target.Count - commonSuffixLength,
                    source.Count - commonSuffixLength, keySelector, keyComparer);
        }

        #region Utilities for SyncUnique
        /// <summary>
        /// Calculates lengths of common prefix and suffix and extends them by moving side elements when possible.
        /// </summary>
        private static (int, int) ExtendCommonPrefixAndSuffix<T, K>(
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
        /// Builds a mapping from unique keys to their indices in the collection.
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
        /// Moves disordered elements in source to match target with minimal moves using LIS.
        /// </summary>
        /// <remarks>
        /// Sorts the mapping array with the minimum number of moves computed as n - LIS length.
        /// </remarks>
        private static void MoveDisorderedElements<T>(
            ObservableCollection<T> source,
            int[] sourceToTarget,
            int sourceOffset)
        {
            if (sourceToTarget.Length <= 1)
                return;

            // LIS indices
            var lisIndices = ComputeLisIndices(sourceToTarget);
            if (lisIndices.Length == sourceToTarget.Length)
                return; // already sorted

            // LIS target indices
            var lis = lisIndices.Select(i => sourceToTarget[i]).ToArray();
            // LIS gap pointers
            var lisGapPtrs = new int[lis.Length + 1];

            // Loop variables
            int currentGap = 0;
            int currentPtr = 0;

            while (true)
            {
                while (true)
                {
                    // Reach the end
                    if (currentGap == lis.Length && currentPtr == sourceToTarget.Length)
                        return;
                    // Reach the end of current gap
                    if (currentGap != lis.Length && currentPtr == lisIndices[currentGap])
                        break;

                    int targetIndex = sourceToTarget[currentPtr];
                    // Find target gap for the element
                    int targetGap = ~Array.BinarySearch(lis, targetIndex);
                    // Calculate target gap offset and pointer
                    int targetGapOffset = targetGap == 0 ? 0 : lisIndices[targetGap - 1] + 1;
                    int targetGapPtr = lisGapPtrs[targetGap];
                    // Find target pointer in sourceToTarget
                    int targetPtr = ~Array.BinarySearch(sourceToTarget, targetGapOffset, targetGapPtr, targetIndex);

                    if (targetGap > currentGap)
                    {
                        // Adjust targetPtr to account for shift after moving currentPtr
                        targetPtr--;
                    }

                    source.Move(sourceOffset + currentPtr, sourceOffset + targetPtr);
                    // Update sourceToTarget array
                    MoveArrayItem(sourceToTarget, currentPtr, targetPtr);
                    // Update LIS gap pointers
                    lisGapPtrs[targetGap]++;

                    // Adjust LIS indices after move
                    if (targetGap < currentGap)
                    {
                        for (int i = targetGap; i < currentGap; i++)
                        {
                            lisIndices[i]++;
                        }
                        currentPtr++;
                    }
                    else
                    {
                        for (int i = currentGap; i < targetGap; i++)
                        {
                            lisIndices[i]--;
                        }
                    }
                }

                currentGap++;
                currentPtr = lisIndices[currentGap - 1] + 1 + lisGapPtrs[currentGap]; // skip LIS element
            }
        }

        /// <summary>
        /// Computes indices of a longest increasing subsequence (LIS) in the input array.
        /// Returns indices forming the LIS in increasing order. Complexity: O(n log n).
        /// </summary>
        private static int[] ComputeLisIndices(int[] arr)
        {
            int n = arr.Length;
            if (n == 0)
                return Array.Empty<int>();

            var parent = new int[n];
            var piles = new List<int>(1);

            parent[0] = -1;
            piles.Add(0);

            for (int i = 1; i < n; i++)
            {
                int x = arr[i];

                if (arr[piles[piles.Count - 1]] < x)
                {
                    // fast path: arr is monotonically increasing
                    parent[i] = piles[piles.Count - 1];
                    piles.Add(i);
                }
                else if (x <= arr[piles[0]])
                {
                    // fast path: arr is monotonically decreasing
                    parent[i] = -1;
                    piles[0] = i;
                }
                else
                {
                    // binary search
                    int lo = 1, hi = piles.Count - 1;
                    while (lo < hi)
                    {
                        int mid = (lo + hi) >> 1;
                        if (arr[piles[mid]] < x)
                            lo = mid + 1;
                        else
                            hi = mid;
                    }
                    parent[i] = piles[lo - 1];
                    piles[lo] = i;
                }
            }

            // reconstruct
            int len = piles.Count;
            var lis = new int[len];
            int cur = piles[len - 1];
            for (int k = len - 1; k >= 0; k--)
            {
                lis[k] = cur;
                cur = parent[cur];
            }

            return lis;
        }


        /// <summary>
        /// Moves an array item from one index to another.
        /// </summary>
        private static void MoveArrayItem<T>(T[] array, int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex)
                return;
            var temp = array[fromIndex];
            if (fromIndex < toIndex)
            {
                Array.Copy(array, fromIndex + 1, array, fromIndex, toIndex - fromIndex);
            }
            else
            {
                Array.Copy(array, toIndex, array, toIndex + 1, fromIndex - toIndex);
            }
            array[toIndex] = temp;
        }

        /// <summary>
        /// Inserts new elements from target into source between specified ranges.
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
            int targetIndex = startIndex;
            for (; targetIndex < sourceEndIndex; targetIndex++)
            {
                var sourceKey = keySelector(source[targetIndex]);
                var targetKey = keySelector(target[targetIndex]);
                if (!keyComparer.Equals(sourceKey, targetKey))
                {
                    source.Insert(targetIndex, target[targetIndex]);
                    sourceEndIndex++;
                }
            }
            for (; targetIndex < targetEndIndex; targetIndex++)
            {
                source.Insert(sourceEndIndex++, target[targetIndex]);
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
        public static void SyncWith<T, K>(
            this ObservableCollection<T> source,
            IList<T> target,
            Func<T, K> keySelector,
            IEqualityComparer<K>? keyComparer = null)
        {
            // Validation
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (keyComparer == null)
                keyComparer = EqualityComparer<K>.Default;

            // Calculate lengths of common prefix and suffix
            var (commonPrefixLength, commonSuffixLength) = ExtendCommonPrefixAndSuffix(
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

                // Recalculate common prefix & suffix length after removals
                (commonPrefixLength, commonSuffixLength) = ExtendCommonPrefixAndSuffix(
                    source,
                    target,
                    keySelector,
                    keyComparer,
                    commonPrefixLength,
                    commonSuffixLength);
                commonLengthSum = commonPrefixLength + commonSuffixLength;
            }

            if (commonLengthSum != source.Count && commonLengthSum != target.Count)
            {
                var targetKeyMap = BuildKeyToIndexQueueMap(target, commonPrefixLength, target.Count - commonSuffixLength,
                    keySelector, keyComparer);

                var sourceToTarget = new int[source.Count - commonLengthSum];
                for (int i = commonPrefixLength; i < source.Count - commonSuffixLength; i++)
                {
                    var sourceItem = source[i];
                    var sourceKey = keySelector(sourceItem);
                    sourceToTarget[i - commonPrefixLength] = targetKeyMap[sourceKey].Dequeue();
                }

                // Move disordered elements to their target positions
                MoveDisorderedElements(source, sourceToTarget, commonPrefixLength);
            }

            // Insert new elements from target into source
            if (commonLengthSum != target.Count)
                InsertNewElements(source, target, commonPrefixLength, target.Count - commonSuffixLength,
                    source.Count - commonSuffixLength, keySelector, keyComparer);
        }

        /// <summary>
        /// Builds a mapping from keys to queues of their indices in the collection (supports duplicates).
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
