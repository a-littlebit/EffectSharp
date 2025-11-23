using System;
using System.Collections.Generic;
using System.Linq;

namespace EffectSharp
{
    public static class SequenceDiffer
    {
        /// <summary>
        /// Compute the differences between two sequences and return a series of edit operations.
        /// </summary>
        /// <typrparam name="T">The type of elements in the sequences.</typrparam>
        /// <param name="oldSequence">Old sequence.</param>
        /// <param name="newSequence">New sequence.</param>
        /// <param name="areEqual">A function to determine if two elements are equal.</param>
        /// <param name="reverse">If true, the edit operations will be in reverse order.</param>
        /// <returns>A list of edit operations to transform oldSequence into newSequence.</returns>
        public static List<EditOperation<T>> ComputeDiff<T>(
            IEnumerable<T> oldSequence,
            IEnumerable<T> newSequence,
            Func<T, T, bool> areEqual,
            bool reverse = false)
        {
            var oldList = oldSequence.ToList();
            var newList = newSequence.ToList();

            int n = oldList.Count;
            int m = newList.Count;

            // Create a DP table where dp[i][j] represents the length of LCS of oldList[0..i-1] and newList[0..j-1]
            int[,] dp = new int[n + 1, m + 1];

            // Fill the DP table
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    if (areEqual(oldList[i - 1], newList[j - 1]))
                    {
                        dp[i, j] = dp[i - 1, j - 1] + 1;
                    }
                    else
                    {
                        dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                    }
                }
            }

            // Backtrack the DP table to generate edit operations
            List<EditOperation<T>> edits = new List<EditOperation<T>>();
            int x = n, y = m;

            while (x > 0 || y > 0)
            {
                if (x > 0 && y > 0 && areEqual(oldList[x - 1], newList[y - 1]))
                {
                    // Move diagonally
                    x--;
                    y--;
                }
                else
                {
                    // Determine whether to delete from oldList or insert into newList
                    bool delete = (x > 0) && (y == 0 || dp[x - 1, y] >= dp[x, y - 1]);

                    if (delete)
                    {
                        // Delete from old sequence (at position x-1)
                        edits.Add(new EditOperation<T> { Type = EditType.Delete, Item = oldList[x - 1], Index = x - 1 });
                        x--;
                    }
                    else
                    {
                        // Insert into new sequence (at position y-1)
                        edits.Add(new EditOperation<T> { Type = EditType.Insert, Item = newList[y - 1], Index = x });
                        y--;
                    }
                }
            }

            if (!reverse)
            {
                // Since we backtracked from the end, we need to reverse the list to get the correct order of operations
                edits.Reverse();
            }
            return edits;
        }
    }

    /// <summary>
    /// Represents an edit operation.
    /// </summary>
    /// <typeparam name="T">The type of the element involved in the operation.</typeparam>
    public class EditOperation<T>
    {
        /// <summary>
        /// The type of edit operation.
        /// </summary>
        public EditType Type { get; set; }

        /// <summary>
        /// The element involved in the operation.
        /// </summary>
        public T Item { get; set; }

        /// <summary>
        /// The index where the operation occurs.
        /// For Delete, this is the index of the element in the old sequence.
        /// For Insert, this is the index at which the element should be inserted into the new sequence.
        /// </summary>
        public int Index { get; set; }
    }

    /// <summary>
    /// Types of edit operations.
    /// </summary>
    public enum EditType
    {
        Delete,
        Insert
    }
}
