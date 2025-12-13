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
        private int _count;


        public ICommand IncrementCommand { get; }

        public MainViewModel()
        {
            IncrementCommand = FunctionCommand.Create<object>(_ => Count++);
        }
    }
}
