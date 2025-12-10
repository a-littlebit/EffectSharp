using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EffectSharp.Tests
{
    /// <summary>
    /// Stress tests for TaskBatcher.
    ///
    /// These tests focus on correctness under concurrency rather than performance:
    /// - no deadlocks
    /// - no lost items
    /// - correct behavior under cancellation, disposal, and reentrancy
    /// </summary>
    public class TaskBatcherStressTests
    {
        private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);

        private static TaskBatcher<int> CreateBatcher(
            Action<List<int>> processor)
        {
            // Use the default scheduler, consistent with TaskManager usage
            return new TaskBatcher<int>(
                processor,
                int.MaxValue,
                TaskScheduler.Default);
        }

        /// <summary>
        /// Core stress test:
        /// - multiple concurrent producers enqueue items
        /// - FlushAsync is invoked randomly and concurrently
        /// 
        /// Invariants:
        /// - no deadlocks
        /// - no lost items
        /// - no duplicate processing
        /// </summary>
        [Fact]
        public async Task Concurrent_Enqueue_And_Flush_Should_Not_Lose_Items()
        {
            using var cts = new CancellationTokenSource(TestTimeout);

            const int producerCount = 8;
            const int itemsPerProducer = 500;

            var processed = 0;

            using var batcher = CreateBatcher(batch =>
            {
                Interlocked.Add(ref processed, batch.Count);
            });

            var producers = Enumerable.Range(0, producerCount)
                .Select(_ => Task.Run(async () =>
                {
                    for (int i = 0; i < itemsPerProducer; i++)
                    {
                        batcher.Enqueue(1);

                        // Random flush to introduce scheduling races
                        if (Random.Shared.Next(10) == 0)
                            await batcher.FlushAsync();

                        await Task.Yield();
                    }
                }, cts.Token))
                .ToArray();

            await Task.WhenAll(producers);
            await batcher.FlushAsync(cts.Token);

            Assert.Equal(producerCount * itemsPerProducer, Volatile.Read(ref processed));
        }

        /// <summary>
        /// FlushAsync with cancellation:
        /// - FlushAsync must not hang when cancellation is requested
        /// - An OperationCanceledException is expected
        /// </summary>
        [Fact]
        public async Task Flush_With_Cancellation_Should_Not_Hang()
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

            using var batcher = CreateBatcher(batch =>
            {
                // Simulate slow batch processing
                Thread.SpinWait(50_000);
            });

            for (int i = 0; i < 200; i++)
                batcher.Enqueue(i);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => batcher.NextTick(cts.Token));
        }

        /// <summary>
        /// Concurrent Dispose:
        /// - Dispose is called while the batcher is in use
        /// - Must not deadlock
        /// - No unexpected exceptions should be thrown
        /// </summary>
        [Fact]
        public async Task Dispose_During_Concurrent_Use_Should_Not_Deadlock()
        {
            using var cts = new CancellationTokenSource(TestTimeout);

            var batcher = CreateBatcher(_ => { });

            var work = Task.Run(async () =>
            {
                for (int i = 0; i < 500; i++)
                {
                    batcher.Enqueue(i);
                    await Task.Yield();
                }
            }, cts.Token);

            await Task.Delay(20, cts.Token);
            batcher.Dispose();

            await work; // Should not hang
        }

        /// <summary>
        /// Exception handling in batch processor:
        /// - Exceptions should propagate to FlushAsync
        /// - Subsequent batches should still be processed correctly
        /// </summary>
        [Fact]
        public async Task Exception_In_BatchProcessor_Should_Not_Break_Future_Batches()
        {
            var callCount = 0;
            var processed = new ConcurrentBag<int>();

            using var batcher = CreateBatcher(batch =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("boom");

                foreach (var item in batch)
                    processed.Add(item);
            });

            batcher.Enqueue(1);
            await Assert.ThrowsAsync<InvalidOperationException>(() => batcher.FlushAsync());

            batcher.Enqueue(2);
            await batcher.FlushAsync();

            Assert.Contains(2, processed);
        }

        /// <summary>
        /// Reentrancy scenario:
        /// - Enqueue is called from within the batch processor
        /// - Must not deadlock or corrupt internal state
        /// </summary>
        [Fact]
        public async Task Reentrant_Enqueue_From_BatchProcessor_Should_Not_Deadlock()
        {
            var processed = 0;
            TaskBatcher<int>? batcher = null;

            batcher = CreateBatcher(batch =>
            {
                Interlocked.Add(ref processed, batch.Count);

                // Simulate effect / watcher triggering new work
                if (processed < 100)
                    batcher!.Enqueue(1);
            });

            using (batcher)
            {
                batcher.Enqueue(1);
                for (int i = 0; i < 100; i++)
                {
                    await batcher.FlushAsync();
                }
            }

            Assert.True(processed >= 100);
        }


        /// <summary>
        /// Randomized stress test:
        /// - multiple concurrent workers
        /// - random FlushAsync calls
        /// - repeated iterations to expose rare race conditions
        /// </summary>
        [Fact]
        public async Task Randomized_Stress_Should_Be_Stable()
        {
            using var cts = new CancellationTokenSource(TestTimeout);

            for (int iteration = 0; iteration < 5; iteration++)
            {
                var processed = 0;

                using var batcher = CreateBatcher(batch =>
                {
                    Interlocked.Add(ref processed, batch.Count);
                });

                var workers = Enumerable.Range(0, 6)
                    .Select(_ => Task.Run(async () =>
                    {
                        for (int i = 0; i < 100; i++)
                        {
                            batcher.Enqueue(1);

                            if (Random.Shared.Next(5) == 0)
                                await batcher.FlushAsync();

                            await Task.Delay(Random.Shared.Next(1, 3));
                        }
                    }, cts.Token))
                    .ToArray();

                await Task.WhenAll(workers);
                await batcher.FlushAsync(cts.Token);

                Assert.True(processed > 0);
            }
        }
    }
}
