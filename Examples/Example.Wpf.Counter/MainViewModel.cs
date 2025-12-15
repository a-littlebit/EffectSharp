using EffectSharp;
using EffectSharp.SourceGenerators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Example.Wpf.Counter
{
    [ReactiveModel]
    public partial class MainViewModel
    {
        [ReactiveField]
        private AtomicInt _count = new AtomicInt(0);

        [Computed]
        public string ComputeDisplayCount()
        {
            return $"Current Count: {Count}";
        }

        public int IncrementStep { get; } = 2;

        [FunctionCommand(CanExecute = nameof(CanIncrement), AllowConcurrentExecution = false, ExecutionScheduler = "TaskScheduler.Default")]
        public async Task<int> Increment(int? count = 1, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!count.HasValue) return Count;

            var newCount = Count + count.Value;
            Count = newCount;

            await Task.Delay(500, cancellationToken);
            return newCount;
        }

        public bool CanIncrement()
        {
            return Count < 10;
        }

        public MainViewModel()
        {
            InitializeReactiveModel();
        }
    }
}
