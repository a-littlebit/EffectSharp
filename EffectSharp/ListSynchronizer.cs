using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EffectSharp
{
    public static class ListSynchronizer
    {
        /// <summary>
        /// Synchronizes the elements of the source collection with the target list based on matching keys, updating,
        /// removing, and inserting items as needed to reflect the target's contents and order.
        /// </summary>
        /// <remarks>This method efficiently updates the source collection to match the target list by
        /// comparing keys, minimizing the number of moves and changes. Items in the source that do not exist in the
        /// target are removed, new items are inserted, and existing items are reordered to match the target. The method
        /// is optimized to reduce unnecessary collection changes, which is useful for UI scenarios where
        /// ObservableCollection change notifications are expensive.</remarks>
        /// <typeparam name="T">The type of elements contained in the source and target collections.</typeparam>
        /// <typeparam name="K">The type of key used to identify and match elements between the collections.</typeparam>
        /// <param name="source">The observable collection to be updated so that its contents and order match those of the target list.
        /// Cannot be null.</param>
        /// <param name="target">The list whose elements and order will be reflected in the source collection. Cannot be null.</param>
        /// <param name="keySelector">A function that extracts the key from each element, used to match items between the source and target
        /// collections. Cannot be null.</param>
        /// <param name="keyComparer">An optional equality comparer used to compare keys. If null, the default equality comparer for the key type
        /// is used.</param>
        /// <exception cref="ArgumentNullException">Thrown if source, target, or keySelector is null.</exception>
        public static void SyncKeyed<T, K>(
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

            // Build initial key-to-index mappings
            var sourceKeyMap = BuildKeyToIndexMap(source, keySelector, keyComparer);
            var targetKeyMap = BuildKeyToIndexMap(target, keySelector, keyComparer);

            // Remove elements not in target from source in descending index order (to avoid index shifting)
            var indicesToDelete = sourceKeyMap.Where(pair => !targetKeyMap.ContainsKey(pair.Key))
                .Select(pair => pair.Value)
                .OrderByDescending(idx => idx);
            foreach (var deleteIndex in indicesToDelete)
            {
                source.RemoveAt(deleteIndex);
            }
            // Rebuild source key map after deletions
            sourceKeyMap = BuildKeyToIndexMap(source, keySelector, keyComparer);

            // Extract common elements and their source indices
            var (targetCommonKeys, newSourceIndices) = ExtractCommonElements(target, sourceKeyMap, keySelector, keyComparer);
            if (targetCommonKeys.Count == 0)
            {
                // No common elements, clear and re-add all
                source.Clear();
                sourceKeyMap.Clear();
                foreach (var item in target)
                {
                    source.Add(item);
                    sourceKeyMap.Add(keySelector(item), source.Count - 1);
                }
                return;
            }

            // Calculate LIS on the source indices of the common elements to minimize moves
            var lisIndices = FindLongestIncreasingSubsequenceIndices(newSourceIndices);
            var lisIndexSet = new HashSet<int>(lisIndices);

            // Move non-LIS elements to their target positions
            MoveNonLisElements(source, targetCommonKeys, sourceKeyMap, lisIndexSet);

            // Rebuild source key map after moves
            sourceKeyMap = BuildKeyToIndexMap(source, keySelector, keyComparer);

            // Insert new elements from target into source
            InsertNewElements(source, target, sourceKeyMap, keySelector);
        }

        #region Utilities for SyncKeyed

        /// <summary>
        /// Build a mapping from keys to their indices in the collection
        /// </summary>
        private static Dictionary<K, int> BuildKeyToIndexMap<T, K>(
            IEnumerable<T> collection,
            Func<T, K> keySelector,
            IEqualityComparer<K> keyComparer)
        {
            var map = new Dictionary<K, int>(keyComparer);
            int index = 0;

            foreach (var item in collection)
            {
                var key = keySelector(item);
                if (map.ContainsKey(key))
                {
                    throw new InvalidOperationException("Duplicate key detected: " + key);
                }
                map[key] = index++;
            }

            return map;
        }

        /// <summary>
        /// Extract common elements between target and source, returning their keys and source indices
        /// </summary>
        private static (List<K> targetCommonKeys, List<int> newSourceIndices) ExtractCommonElements<T, K>(
            IList<T> target,
            Dictionary<K, int> sourceKeyMap,
            Func<T, K> keySelector,
            IEqualityComparer<K> keyComparer)
        {
            var targetCommonKeys = new List<K>();
            var newSourceIndices = new List<int>();

            foreach (var item in target)
            {
                var key = keySelector(item);
                if (sourceKeyMap.TryGetValue(key, out int sourceIndex))
                {
                    targetCommonKeys.Add(key);
                    newSourceIndices.Add(sourceIndex);
                }
            }

            return (targetCommonKeys, newSourceIndices);
        }

        /// <summary>
        /// Find indices of the Longest Increasing Subsequence (LIS) in the given sequence
        /// </summary>
        private static List<int> FindLongestIncreasingSubsequenceIndices(List<int> sequence)
        {
            if (sequence == null || sequence.Count == 0)
                return new List<int>();

            int n = sequence.Count;
            int[] tails = new int[n]; // tails[i]：Min tail index of LIS with length i+1
            int[] lengths = new int[n]; // lengths[i]：Length of LIS ending at sequence[i]
            int lisLength = 0;

            for (int i = 0; i < n; i++)
            {
                int current = sequence[i];
                int left = 0, right = lisLength;

                while (left < right)
                {
                    int mid = (left + right) / 2;
                    if (sequence[tails[mid]] >= current)
                        right = mid;
                    else
                        left = mid + 1;
                }

                tails[left] = i;
                lengths[i] = left + 1;
                if (left == lisLength)
                    lisLength++;
            }

            // Reconstruct LIS indices
            var lisIndices = new List<int>();
            int currentLength = lisLength;
            for (int i = n - 1; i >= 0; i--)
            {
                if (lengths[i] == currentLength)
                {
                    lisIndices.Add(i);
                    currentLength--;
                    if (currentLength == 0)
                        break;
                }
            }

            lisIndices.Reverse();
            return lisIndices;
        }

        /// <summary>
        /// Move non-LIS elements to their target positions
        /// </summary>
        private static void MoveNonLisElements<T, K>(
            ObservableCollection<T> source,
            List<K> targetCommonKeys,
            Dictionary<K, int> sourceKeyMap,
            HashSet<int> lisIndexSet)
        {
            var offsets = new List<(int From, int To, int Offset)>();
            for (int targetPosInCommon = 0; targetPosInCommon < targetCommonKeys.Count; targetPosInCommon++)
            {
                if (lisIndexSet.Contains(targetPosInCommon))
                    continue; // LIS element, skip

                var key = targetCommonKeys[targetPosInCommon];
                int currentIndex = sourceKeyMap[key];

                foreach (var (From, To, Offset) in offsets)
                {
                    if (currentIndex >= From && currentIndex <= To)
                    {
                        currentIndex += Offset;
                    }
                }

                // Move element to its target position in common elements
                if (currentIndex == targetPosInCommon)
                    continue;
                source.Move(currentIndex, targetPosInCommon);
                if (currentIndex < targetPosInCommon)
                {
                    offsets.Add((currentIndex, targetPosInCommon, -1));
                }
                else
                {
                    offsets.Add((targetPosInCommon, currentIndex, 1));
                }
            }
        }

        /// <summary>
        /// Insert new elements from target into source
        /// </summary>
        private static void InsertNewElements<T, K>(
            ObservableCollection<T> source,
            IList<T> target,
            Dictionary<K, int> sourceKeyMap,
            Func<T, K> keySelector)
        {
            int additionOffset = 0;
            for (int targetIndex = 0; targetIndex < target.Count; targetIndex++)
            {
                var item = target[targetIndex];
                var key = keySelector(item);
                if (!sourceKeyMap.ContainsKey(key))
                {
                    source.Insert(targetIndex + additionOffset, item);
                    additionOffset++;
                }
            }
        }

        #endregion

        /// <summary>
        /// Synchronizes the elements of the source ObservableCollection with the target IList using the Heckel diff algorithm.
        /// </summary>
        /// <param name="source">The source ObservableCollection to be synchronized. </param>
        /// <param name="target">The target IList to synchronize with. </param>
        /// <param name="comparer">The equality comparer to compare elements. If null, the default equality comparer is used. </param>
        public static void SyncUnkeyed<T>(
            ObservableCollection<T> source,
            IList<T> target,
            IEqualityComparer<T> comparer = null)
        {
            var operations = GenerateHeckelOperations(source, target, comparer);
            ApplyHeckelOperations(source, operations);
        }

        // Enum for Heckel operation types
        public enum HeckelOperationType
        {
            Delete,
            Move,
            Insert
        }

        // Represents a single Heckel operation
        public class HeckelOperation<T>
        {
            /// <summary>
            /// The type of operation
            /// </summary>
            public HeckelOperationType Type { get; }

            /// <summary>
            /// The item involved in the operation
            /// </summary>
            public T Item { get; }

            /// <summary>
            /// The source index (only valid for Delete/Move operations)
            /// </summary>
            public int SourceIndex { get; }

            /// <summary>
            /// The target index (only valid for Insert/Move operations)
            /// </summary>
            public int TargetIndex { get; }

            private HeckelOperation(HeckelOperationType type, T item, int sourceIndex, int targetIndex)
            {
                Type = type;
                Item = item;
                SourceIndex = sourceIndex;
                TargetIndex = targetIndex;
            }

            public static HeckelOperation<T> Delete(T item, int sourceIndex)
                => new HeckelOperation<T>(HeckelOperationType.Delete, item, sourceIndex, -1);

            public static HeckelOperation<T> Move(T item, int sourceIndex, int targetIndex)
                => new HeckelOperation<T>(HeckelOperationType.Move, item, sourceIndex, targetIndex);

            public static HeckelOperation<T> Insert(T item, int targetIndex)
                => new HeckelOperation<T>(HeckelOperationType.Insert, item, -1, targetIndex);
        }

        /// <summary>
        /// Generates a list of Heckel operations to transform the source collection into the target collection
        /// </summary>
        /// <param name="source">Source collection. </param>
        /// <param name="target">Target collection. </param>
        /// <param name="comparer">Element equality comparer. </param>
        /// <returns>A list of Heckel operations.</returns>
        public static List<HeckelOperation<T>> GenerateHeckelOperations<T>(
            IList<T> source,
            IList<T> target,
            IEqualityComparer<T> comparer = null)
        {
            if (comparer == null)
                comparer = EqualityComparer<T>.Default;
            var operations = new List<HeckelOperation<T>>();

            int sourceCount = source.Count;
            int targetCount = target.Count;

            // Build element to source indices mapping (HA)
            var elementSourceIndices = new Dictionary<T, List<int>>(comparer);
            for (int i = 0; i < sourceCount; i++)
            {
                T item = source[i];
                if (!elementSourceIndices.ContainsKey(item))
                {
                    elementSourceIndices[item] = new List<int>();
                }
                elementSourceIndices[item].Add(i);
            }

            // Build mappings between source and target indices (OA and OB)
            // OB: The target element's index in the source collection (-1 if not present)
            int[] targetToSourceIndices = new int[targetCount];
            // OA: The source element's index in the target collection (-1 if not present)
            int[] sourceToTargetIndices = new int[sourceCount];
            for (int i = 0; i < sourceCount; i++)
            {
                sourceToTargetIndices[i] = -1;
            }
            for (int i = 0; i < targetCount; i++)
            {
                targetToSourceIndices[i] = -1;
            }

            for (int targetIdx = 0; targetIdx < targetCount; targetIdx++)
            {
                T targetItem = target[targetIdx];
                if (elementSourceIndices.TryGetValue(targetItem, out var sourceIndices) && sourceIndices.Any())
                {
                    // Pick the first available source index (for duplicate elements, use them in order)
                    int sourceIdx = sourceIndices[0];
                    sourceIndices.RemoveAt(0);

                    targetToSourceIndices[targetIdx] = sourceIdx;
                    sourceToTargetIndices[sourceIdx] = targetIdx;
                }
            }

            // Collect source indices to be deleted
            var deleteSourceIndices = new List<int>();
            for (int sourceIdx = 0; sourceIdx < sourceCount; sourceIdx++)
            {
                if (sourceToTargetIndices[sourceIdx] == -1)
                {
                    deleteSourceIndices.Add(sourceIdx);
                }
            }

            // Calculate delete offsets and processed flags
            // Processed flags (deleted/moved elements marked as true)
            bool[] isTraced = new bool[sourceCount];
            // Delete offset: records the number of deleted elements before each source index
            int[] deleteOffset = new int[sourceCount];
            int deletedCount = 0;

            for (int sourceIdx = 0; sourceIdx < sourceCount; sourceIdx++)
            {
                deleteOffset[sourceIdx] = deletedCount;
                if (sourceToTargetIndices[sourceIdx] == -1)
                {
                    deletedCount++;
                    isTraced[sourceIdx] = true; // Deleted
                }
                else
                {
                    isTraced[sourceIdx] = false; // Not processed yet (may need to move)
                }
            }

            // Collect move and insert operations
            int tracePtr = 0;
            // Move the trace pointer to the first unprocessed element
            while (tracePtr < sourceCount && isTraced[tracePtr])
            {
                tracePtr++;
            }

            for (int targetIdx = 0; targetIdx < targetCount; targetIdx++)
            {
                int sourceIdx = targetToSourceIndices[targetIdx];
                if (sourceIdx == -1)
                {
                    // Target element not in source, add insert operation
                    operations.Add(HeckelOperation<T>.Insert(target[targetIdx], targetIdx));
                }
                else
                {
                    if (sourceIdx == tracePtr)
                    {
                        // Target element is at the correct position, just mark as processed
                        isTraced[sourceIdx] = true;
                        // Move pointer to the next unprocessed element
                        while (tracePtr < sourceCount && isTraced[tracePtr])
                        {
                            tracePtr++;
                        }
                    }
                    else
                    {
                        // Calculate adjusted source index after deletions
                        int adjustedSourceIndex = sourceIdx - deleteOffset[sourceIdx];
                        // Add move operation
                        operations.Add(HeckelOperation<T>.Move(source[sourceIdx], adjustedSourceIndex, targetIdx));
                        // Mark as processed
                        isTraced[sourceIdx] = true;
                        // Move pointer to the next unprocessed element
                        while (tracePtr < sourceCount && isTraced[tracePtr])
                        {
                            tracePtr++;
                        }
                    }
                }
            }

            // Add delete operations in reverse order to avoid index shifting issues
            for (int i = deleteSourceIndices.Count - 1; i >= 0; i--)
            {
                int deleteIdx = deleteSourceIndices[i];
                operations.Insert(0, HeckelOperation<T>.Delete(source[deleteIdx], deleteIdx));
            }

            return operations;
        }

        /// <summary>
        /// Applies a list of Heckel operations to the source collection to transform it accordingly
        /// </summary>
        /// <param name="source">The source collection to be modified. </param>
        /// <param name="operations">The list of Heckel operations to apply. </param>
        public static void ApplyHeckelOperations<T>(ObservableCollection<T> source, List<HeckelOperation<T>> operations)
        {
            // Execute delete operations in reverse order to avoid index shifting issues
            foreach (var op in operations.Where(op => op.Type == HeckelOperationType.Delete).Reverse())
            {
                if (op.SourceIndex >= 0 && op.SourceIndex < source.Count)
                {
                    source.RemoveAt(op.SourceIndex);
                }
            }

            // Execute move operations (based on the collection indices after deletions)
            foreach (var op in operations.Where(op => op.Type == HeckelOperationType.Move))
            {
                if (op.SourceIndex >= 0 && op.SourceIndex < source.Count &&
                    op.TargetIndex >= 0 && op.TargetIndex <= source.Count)
                {
                    source.Move(op.SourceIndex, op.TargetIndex);
                }
            }

            // Execute insert operations (based on the final collection indices)
            foreach (var op in operations.Where(op => op.Type == HeckelOperationType.Insert))
            {
                if (op.TargetIndex >= 0 && op.TargetIndex <= source.Count)
                {
                    source.Insert(op.TargetIndex, op.Item);
                }
            }
        }
    }
}
