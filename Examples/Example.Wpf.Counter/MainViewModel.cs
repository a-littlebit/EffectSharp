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
        private int _count = 0;

        [ReactiveField]
        private int _restoreCount = 0;

        private readonly SynchronizationContext sync = SynchronizationContext.Current!;

        [Computed]
        public int CurrentCount()
        {
            return Count + RestoreCount;
        }

        [Computed]
        public string ComputeDisplayCount()
        {
            return $"Current Count: {ComputedCurrentCount}";
        }

        [Watch(Properties = [nameof(Count)])]
        public void OnDisplayCountChanged(int newCount, int oldCount)
        {
            _ = RestoreLater(2000, oldCount - newCount);
        }

        public async Task RestoreLater(int delay, int setp)
        {
            await Task.Delay(delay).ConfigureAwait(false);
            sync.Post(_ =>
            {
                RestoreCount += setp;
            }, null);
        }

        [FunctionCommand(CanExecute = nameof(CanIncrement), AllowConcurrentExecution = false)]
        public async Task Increment()
        {
            sync.Post(_ =>
            {
                Count++;
            }, null);
            await Task.Delay(200).ConfigureAwait(false);
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
