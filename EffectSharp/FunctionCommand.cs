using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace EffectSharp
{
    public abstract class FunctionCommandBase<TParam, TResult> : ICommand, IDisposable
    {
        private readonly Func<TParam, object, bool> _canExecute;
        private readonly bool _allowConcurrentExecution;

        private object _dependencyValue;
        private readonly Effect _effect;
        private AtomicIntRef _executingCount = new AtomicIntRef(0);

        public event EventHandler CanExecuteChanged;
        public event EventHandler<FunctionCommandExecutionFailed<TParam>> ExecutionFailed;

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
                Reactive.Watch(dependencySelector, (newValue, oldValue) =>
                {
                    Volatile.Write(ref _dependencyValue, newValue);
                    RaiseCanExecuteChanged();
                }, new WatchOptions<object> { Immediate = true });
            }
        }

        public virtual void RaiseCanExecuteChanged()
        {
            if (CanExecuteChanged != null)
            {
                TaskManager.EnqueueNotify(this, nameof(CanExecuteChanged), (args) =>
                {
                    CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                });
            }
        }

        protected virtual void OnExecutionFailed(FunctionCommandExecutionFailed<TParam> args)
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
            var dependencyValue = Volatile.Read(ref _dependencyValue);
            return _canExecute(parameter, dependencyValue);
        }

        protected object BeginExecution(TParam parameter)
        {
            var dependencyValue = Volatile.Read(ref _dependencyValue);
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

        protected void EndExecution(TParam parameter, Exception ex = null)
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
            if (ex != null)
            {
                OnExecutionFailed(new FunctionCommandExecutionFailed<TParam>(ex, parameter));
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
            catch (FunctionCommandNotExecutableException)
            {
                // Since the canExecuteChanged event may be raised asynchronously,
                // it's possible that the command is not executable at this point.
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
            catch (Exception ex)
            {
                EndExecution(parameter, ex);
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
            catch (FunctionCommandNotExecutableException)
            {
                // Since the canExecuteChanged event may be raised asynchronously,
                // it's possible that the command is not executable at this point.
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
            catch (Exception ex)
            {
                EndExecution(parameter, ex);
                throw;
            }
            return result;
        }
    }

    public class FunctionCommandExecutionFailed<TParam> : EventArgs
    {
        public Exception Exception { get; }
        public TParam Parameter { get; }
        public FunctionCommandExecutionFailed(Exception exception, TParam parameter)
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
