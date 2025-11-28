using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace EffectSharp
{
    public static class ListSynchronizer
    {
        public static void SyncKeyed<T, TKey>(
            IList<T> newList,
            ObservableCollection<T> target,
            Func<T, TKey> keySelector,
            IEqualityComparer<TKey> equalityComparer = null)
        {
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));
            if (equalityComparer == null) equalityComparer = EqualityComparer<TKey>.Default;

            // key-index map for old items
            var oldKeyIndex = new Dictionary<TKey, int>();
            for (int i = 0; i < target.Count; i++)
            {
                var key = keySelector(target[i]);
                if (!oldKeyIndex.ContainsKey(key))
                    oldKeyIndex.Add(key, i);
            }

            // map new index to old index
            // exist[i] = old index, or -1 if it's new
            var newToOldIndex = new int[newList.Count];
            for (int i = 0; i < newList.Count; i++)
            {
                var key = keySelector(newList[i]);
                if (oldKeyIndex.TryGetValue(key, out var oldIndex))
                    newToOldIndex[i] = oldIndex;
                else
                    newToOldIndex[i] = -1; // mark for insert
            }

            // remove items not in newList
            var newKeys = new HashSet<TKey>(newList.Select(keySelector));
            for (int i = target.Count - 1; i >= 0; i--)
            {
                var key = keySelector(target[i]);
                if (!newKeys.Contains(key))
                    target.RemoveAt(i);
            }

            // insert/move items to match newList
            int j = 0;
            for (int i = 0; i < newList.Count; i++)
            {
                var item = newList[i];
                var key = keySelector(item);

                if (j < target.Count && equalityComparer.Equals(keySelector(target[j]), key))
                {
                    // item already in correct position
                    j++;
                    continue;
                }

                // Find existing item in later positions
                int existingIndex = -1;
                for (int k = j + 1; k < target.Count; k++)
                {
                    if (equalityComparer.Equals(keySelector(target[k]), key))
                    {
                        existingIndex = k;
                        break;
                    }
                }

                if (existingIndex >= 0)
                {
                    // Move existing item
                    target.Move(existingIndex, j);
                }
                else
                {
                    // Insert new item
                    target.Insert(j, item);
                }

                j++;
            }
        }

        public static void SyncUnkeyed<T>(
            IList<T> newList,
            ObservableCollection<T> target,
            IEqualityComparer<T> comparer = null)
        {
            if (comparer == null)
                comparer = EqualityComparer<T>.Default;

            // build old value -> index queue mapping
            var oldMap = new Dictionary<T, Queue<int>>(comparer);
            for (int i = 0; i < target.Count; i++)
            {
                if (!oldMap.TryGetValue(target[i], out var q))
                    oldMap[target[i]] = q = new Queue<int>();
                q.Enqueue(i);
            }

            // build new to old index mapping
            var newToOldIndex = new int[newList.Count];
            for (int i = 0; i < newList.Count; i++)
            {
                var v = newList[i];
                if (oldMap.TryGetValue(v, out var q) && q.Count > 0)
                    newToOldIndex[i] = q.Dequeue();
                else
                    newToOldIndex[i] = -1;
            }

            // remaining indices in oldMap queues are to be removed
            var toRemove = new List<int>();
            foreach (var pair in oldMap)
            {
                var q = pair.Value;
                while (q.Count > 0)
                    toRemove.Add(q.Dequeue());
            }

            // remove in reverse order
            toRemove.Sort();
            for (int i = toRemove.Count - 1; i >= 0; i--)
                target.RemoveAt(toRemove[i]);

            // insert/move to match newList
            int current = 0;
            for (int i = 0; i < newList.Count; i++)
            {
                var v = newList[i];
                int oldIndex = newToOldIndex[i];

                if (oldIndex >= 0)
                {
                    // existing item, maybe need move
                    // note: oldIndex may be stale due to prior inserts/moves
                    int realIndex = -1;
                    for (int k = current; k < target.Count; k++)
                    {
                        if (comparer.Equals(target[k], v))
                        {
                            realIndex = k;
                            break;
                        }
                    }

                    if (realIndex == -1)
                    {
                        // safety fallback: treat as insert
                        target.Insert(current, v);
                    }
                    else if (realIndex != current)
                    {
                        target.Move(realIndex, current);
                    }
                }
                else
                {
                    // new item
                    target.Insert(current, v);
                }

                current++;
            }
        }
    }
}
