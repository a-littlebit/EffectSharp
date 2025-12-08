using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace EffectSharp
{
    public abstract class FunctionCommandBase<TParam, TResult> : ICommand, IDisposable
    {
        private readonly Func<TParam, object, bool> _canExecute;
        private readonly bool _allowConcurrentExecution;

        private Func<object> _dependencyGetter;
        private readonly Effect _effect;
        private AtomicIntRef _executingCount = new AtomicIntRef(0);

        public event EventHandler CanExecuteChanged;
        public event EventHandler<FunctionCommandExecutionFailedEventArgs<TParam>> ExecutionFailed;

        public IReadOnlyRef<int> ExecutingCount => _executingCount;

        protected FunctionCommandBase(
            Func<TParam, object, bool> canExecute = null,
            Func<object> dependencySelector = null,
            bool allowConcurrentExecution = false)
        {
            _canExecute = canExecute ?? ((param, dep) => true);
            _allowConcurrentExecution = allowConcurrentExecution;

            if (dependencySelector != null)
            {
                var dependencyValue = Reactive.Computed(dependencySelector);
                _effect = Reactive.Watch(dependencyValue, (newValue, oldValue) =>
                {
                    RaiseCanExecuteChanged();
                }, new WatchOptions<object> { Immediate = true });
                _dependencyGetter = () => dependencyValue.Value;
            }
            else
            {
                _dependencyGetter = () => null;
            }
        }

        public virtual void RaiseCanExecuteChanged()
        {
            if (CanExecuteChanged != null)
            {
                TaskManager.EnqueueNotification(this, nameof(CanExecuteChanged), (args) =>
                {
                    CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                });
            }
        }

        protected virtual void OnExecutionFailed(FunctionCommandExecutionFailedEventArgs<TParam> args)
        {
            ExecutionFailed?.Invoke(this, args);
        }

        public bool CanExecute(object parameter)
        {
            if (parameter != null && !(parameter is TParam))
                return false;
            return CanExecute((TParam)parameter);
        }

        public bool CanExecute(TParam parameter)
        {
            if (!_allowConcurrentExecution && _executingCount.Value != 0)
                return false;
            var dependencyValue = _dependencyGetter();
            return _canExecute(parameter, dependencyValue);
        }

        protected object BeginExecution(TParam parameter)
        {
            var dependencyValue = _dependencyGetter();
            if (!_canExecute(parameter, dependencyValue))
                throw new FunctionCommandNotExecutableException();

            if (_allowConcurrentExecution)
            {
                _executingCount.Increment();
            }
            else if (_executingCount.CompareExchange(1, 0) != 0)
            {
                throw new FunctionCommandNotExecutableException();
            }
            else
            {
                RaiseCanExecuteChanged();
            }

            return dependencyValue;
        }

        protected void EndExecution(TParam parameter)
        {
            if (_allowConcurrentExecution)
            {
                _executingCount.Decrement();
            }
            else
            {
                _executingCount.Value = 0;
                RaiseCanExecuteChanged();
            }
        }

        public abstract void Execute(object parameter);

        public void Dispose()
        {
            _effect?.Dispose();
        }
    }

    public class FunctionCommand<TParam, TResult> : FunctionCommandBase<TParam, TResult>
    {
        private readonly Func<TParam, object, TResult> _execute;

        public FunctionCommand(
            Func<TParam, object, TResult> execute,
            Func<TParam, object, bool> canExecute = null,
            Func<object> dependencySelector = null,
            bool allowConcurrentExecution = false)
            : base(
                  canExecute,
                  dependencySelector,
                  allowConcurrentExecution)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public override void Execute(object parameter)
        {
            if (parameter != null && !(parameter is TParam))
                throw new ArgumentException($"Parameter must be of type {typeof(TParam).FullName}.", nameof(parameter));
            try
            {
                Execute((TParam)parameter);
            }
            catch (Exception ex)
            {
                OnExecutionFailed(new FunctionCommandExecutionFailedEventArgs<TParam>(ex, (TParam)parameter));
            }
        }

        public TResult Execute(TParam parameter)
        {
            var dependencyValue = BeginExecution(parameter);
            TResult result;
            try
            {
                result = _execute(parameter, dependencyValue);
            }
            catch (Exception)
            {
                EndExecution(parameter);
                throw;
            }
            EndExecution(parameter);
            return result;
        }
    }

    public class AsyncFunctionCommand<TParam, TResult> : FunctionCommandBase<TParam, TResult>
    {
        private readonly Func<TParam, object, CancellationToken, Task<TResult>> _executeAsync;
        private readonly TaskScheduler _executionScheduler;

        public TaskScheduler ExecutionScheduler => _executionScheduler;

        public AsyncFunctionCommand(
            Func<TParam, object, CancellationToken, Task<TResult>> executeAsync,
            Func<TParam, object, bool> canExecute = null,
            Func<object> dependencySelector = null,
            bool allowConcurrentExecution = false,
            TaskScheduler executionScheduler = null)
            : base(
                  canExecute,
                  dependencySelector,
                  allowConcurrentExecution)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _executionScheduler = executionScheduler;
        }
        public override async void Execute(object parameter)
        {
            if (parameter != null && !(parameter is TParam))
                throw new ArgumentException($"Parameter must be of type {typeof(TParam).FullName}.", nameof(parameter));
            try
            {
                await Execute((TParam)parameter);
            }
            catch (Exception ex)
            {
                OnExecutionFailed(new FunctionCommandExecutionFailedEventArgs<TParam>(ex, (TParam)parameter));
            }
        }

        public async Task<TResult> Execute(TParam parameter, CancellationToken cancellationToken = default)
        {
            var dependencyValue = BeginExecution(parameter);
            TResult result;
            try
            {
                if (_executionScheduler != null)
                {
                    result = await Task.Factory.StartNew(
                        () => _executeAsync(parameter, dependencyValue, cancellationToken),
                        cancellationToken,
                        TaskCreationOptions.DenyChildAttach,
                        _executionScheduler).Unwrap();
                }
                else
                {
                    result = await _executeAsync(parameter, dependencyValue, cancellationToken);
                }
            }
            catch (Exception)
            {
                EndExecution(parameter);
                throw;
            }
            EndExecution(parameter);
            return result;
        }
    }

    public class FunctionCommandExecutionFailedEventArgs<TParam> : EventArgs
    {
        public Exception Exception { get; }
        public TParam Parameter { get; }
        public FunctionCommandExecutionFailedEventArgs(Exception exception, TParam parameter)
        {
            Exception = exception;
            Parameter = parameter;
        }
    }

    public class FunctionCommandNotExecutableException : Exception
    {
        public FunctionCommandNotExecutableException()
            : base("Command cannot be executed in its current state.")
        {
        }
    }
}
