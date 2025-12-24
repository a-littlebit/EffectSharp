using EffectSharp;
using EffectSharp.SourceGenerators;
using System.Windows;

namespace Example.Wpf.Counter
{
    [ReactiveModel]
    public partial class MainViewModel : IDisposable
    {
        [Deep]
        public AtomicIntRef IncrementCount { get; } = new AtomicIntRef(0);

        [Deep]
        public AtomicIntRef RestoreCount { get; } = new AtomicIntRef(0);

        [Computed]
        public int ComputeCount() => IncrementCount.Value + RestoreCount.Value;

        [Computed]
        public string ComputeDisplayCount() => $"Current Count: {Count}";

        [FunctionCommand(CanExecute = nameof(ComputeIsThrottlingIntervalValid), AllowConcurrentExecution = false)]
        public async Task Increment()
        {
            IncrementCount.Increment();
            await Task.Delay(ThrottlingInterval).ConfigureAwait(false);
        }

        [ReactiveField]
        private string _throttlingIntervalInput = "200";

        [Computed]
        public int ComputeThrottlingInterval()
        {
            int interval = 0;
            int.TryParse(ThrottlingIntervalInput, out interval);
            return Math.Max(0, interval);
        }

        [Computed]
        public bool ComputeIsThrottlingIntervalValid() => int.TryParse(ThrottlingIntervalInput, out var interval) && interval >= 0;

        [Computed]
        public Visibility ComputeErrorInfoVisibility() => ComputeIsThrottlingIntervalValid() ? Visibility.Collapsed : Visibility.Visible;

        [Watch($"{nameof(IncrementCount)}.Value")]
        public void OnDisplayCountChanged(int newCount, int oldCount)
        {
            _ = RestoreLater(2000, oldCount - newCount);
        }

        public async Task RestoreLater(int delay, int setp)
        {
            await Task.Delay(delay).ConfigureAwait(false);
            RestoreCount.Add(setp);
        }

        [ReactiveField]
        private int _maxCount = 0;

        [Watch(nameof(Count))]
        public void OnCountChanged(int newCount, int oldCount)
        {
            if (newCount < oldCount && oldCount > MaxCount)
            {
                MaxCount = oldCount;
            }
        }

        public ReactiveCollection<CountRecord> CountRecords { get; } = [];

        [Watch(nameof(MaxCount))]
        public void OnMaxCountChanged(int newMaxCount)
        {
            CountRecords.Add(new CountRecord
            {
                Count = newMaxCount,
                Timestamp = DateTime.Now,
            });
        }

        [ReactiveField]
        private bool _orderByDescending = false;

        [ComputedList]
        public List<CountRecord> ComputeDisplayCountRecords()
            => OrderByDescending
                ? CountRecords.OrderByDescending(r => r.Count).ToList()
                : CountRecords.ToList();

        public MainViewModel() => InitializeReactiveModel();

        public void Dispose() => DisposeReactiveModel();
    }

    public class CountRecord
    {
        public int Count { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
