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
        [ReactiveField(EqualsMethod = ReactiveFieldAttribute.NoEqualityComparison)]
        private AtomicInt _count = new AtomicInt(0);

        [FunctionCommand(CanExecute = nameof(CanIncrement))]
        public void Increment()
        {
            Count++;
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
