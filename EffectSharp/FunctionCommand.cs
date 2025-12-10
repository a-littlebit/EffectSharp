using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace EffectSharp
{
    public interface IReactiveCommand<TParam> : ICommand
    {
        IReadOnlyRef<int> ExecutingCount { get; }
        event EventHandler<FunctionCommandExecutionFailedEventArgs<TParam>> ExecutionFailed;

        bool CanExecute(TParam parameter);
        void RaiseCanExecuteChanged();
    }

    public interface IFunctionCommand<TParam, TResult> : IReactiveCommand<TParam>
    {
        TResult Execute(TParam parameter);
    }

    public interface IFunctionCommand<TParam> : IFunctionCommand<TParam, bool>
    {
    }

    public interface IAsyncFunctionCommand<TParam, TResult> : IReactiveCommand<TParam>
    {
        Task<TResult> Execute(TParam parameter, CancellationToken cancellationToken = default);
    }

    public interface IAsyncFunctionCommand<TParam> : IAsyncFunctionCommand<TParam, bool>
    {
    }

    public abstract class FunctionCommandBase<TParam, TDependency, TResult> : IReactiveCommand<TParam>, ICommand, IDisposable
    {
        private readonly Func<TParam, TDependency, bool> _canExecute;
        private readonly bool _allowConcurrentExecution;

        private readonly Func<TDependency> _dependencyGetter;
        private readonly Effect _effect;
        private AtomicIntRef _executingCount = new AtomicIntRef(0);

        public event EventHandler CanExecuteChanged;
        public event EventHandler<FunctionCommandExecutionFailedEventArgs<TParam>> ExecutionFailed;

        public IReadOnlyRef<int> ExecutingCount => _executingCount;

        protected FunctionCommandBase(
            Func<TParam, TDependency, bool> canExecute = null,
            Func<TDependency> dependencySelector = null,
            bool allowConcurrentExecution = false)
        {
            _canExecute = canExecute ?? ((param, dep) => true);
            _allowConcurrentExecution = allowConcurrentExecution;
            ExecutionFailed += FunctionCommand.TraceExecutionFailure;

            if (dependencySelector != null)
            {
                var dependencyValue = Reactive.Computed(dependencySelector);
                _effect = Reactive.Watch(dependencyValue, (newValue, oldValue) =>
                {
                    RaiseCanExecuteChanged();
                }, new WatchOptions<TDependency> { Immediate = true, Scheduler = eff => eff.Execute() });
                _dependencyGetter = () => dependencyValue.Value;
            }
            else
            {
                _dependencyGetter = () => default;
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

        protected TDependency BeginExecution(TParam parameter)
        {
            var dependencyValue = _dependencyGetter();
            if (!_canExecute(parameter, dependencyValue))
                throw new FunctionCommandNotExecutableException();

            if (_allowConcurrentExecution)
            {
                _executingCount.Increment();
            }
            else if (!_executingCount.CompareExchange(1, 0))
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

    public class FunctionCommand<TParam, TDependency, TResult> : FunctionCommandBase<TParam, TDependency, TResult>, IFunctionCommand<TParam, TResult>
    {
        private readonly Func<TParam, TDependency, TResult> _execute;

        public FunctionCommand(
            Func<TParam, TDependency, TResult> execute,
            Func<TParam, TDependency, bool> canExecute = null,
            Func<TDependency> dependencySelector = null,
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

    public class AsyncFunctionCommand<TParam, TDependency, TResult> : FunctionCommandBase<TParam, TDependency, TResult>, IAsyncFunctionCommand<TParam, TResult>
    {
        private readonly Func<TParam, TDependency, CancellationToken, Task<TResult>> _executeAsync;
        private readonly TaskScheduler _executionScheduler;

        public TaskScheduler ExecutionScheduler => _executionScheduler;

        public AsyncFunctionCommand(
            Func<TParam, TDependency, CancellationToken, Task<TResult>> executeAsync,
            Func<TParam, TDependency, bool> canExecute = null,
            Func<TDependency> dependencySelector = null,
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

    public class FunctionCommand<TParam> : FunctionCommand<TParam, bool, bool>, IFunctionCommand<TParam>
    {
        public FunctionCommand(
            Func<TParam, bool, bool> execute,
            Func<TParam, bool, bool> canExecute = null,
            Func<bool> dependencySelector = null,
            bool allowConcurrentExecution = false)
            : base(
                  execute,
                  canExecute,
                  dependencySelector,
                  allowConcurrentExecution)
        {
        }
    }

    public class AsyncFunctionCommand<TParam> : AsyncFunctionCommand<TParam, bool, bool>, IAsyncFunctionCommand<TParam>
    {
        public AsyncFunctionCommand(
            Func<TParam, bool, CancellationToken, Task<bool>> executeAsync,
            Func<TParam, bool, bool> canExecute = null,
            Func<bool> dependencySelector = null,
            bool allowConcurrentExecution = false,
            TaskScheduler executionScheduler = null)
            : base(
                  executeAsync,
                  canExecute,
                  dependencySelector,
                  allowConcurrentExecution,
                  executionScheduler)
        {
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

    public static class FunctionCommand
    {
        /// <summary>
        /// Creates a new instance of a function-based command with the specified execution,
        /// logic and dependency tracking.
        /// </summary>
        /// <remarks>
        /// <paramref name="canExecute"/> in this function does not track dependencies. To make the command reactive,
        /// use <paramref name="dependencySelector"/> to provide a function that returns the dependencies to track.
        /// </remarks>
        /// <typeparam name="TParam">The type of the command parameter.</typeparam>
        /// <typeparam name="TResult">The type of the command result.</typeparam>
        /// <typeparam name="TDependency">The type of the dependencies to track.</typeparam>
        /// <param name="execute">The function to execute when the command is invoked.</param>
        /// <param name="canExecute">A function to determine whether the command can execute.</param>
        /// <param name="dependencySelector">A function to select dependencies to track for re-evaluating command executability.</param>
        /// <param name="allowConcurrentExecution">Indicates whether concurrent executions of the command are allowed.</param>
        /// <returns>A new instance of <see cref="FunctionCommand{TParam, TDependency, TResult}"/>.</returns>
        public static FunctionCommand<TParam, TDependency, TResult> Create<TParam, TDependency, TResult>(
            Func<TParam, TDependency, TResult> execute,
            Func<TParam, TDependency, bool> canExecute,
            Func<TDependency> dependencySelector,
            bool allowConcurrentExecution = true)
        {
            return new FunctionCommand<TParam, TDependency, TResult>(
                execute,
                canExecute,
                dependencySelector,
                allowConcurrentExecution);
        }

        /// <summary>
        /// Creates a new instance of a function-based command with the specified execution and optional validation
        /// logic.
        /// </summary>
        /// <remarks>
        /// <paramref name="canExecute"/> in this function will track dependencies and make the command reactive.
        /// </remarks>
        /// <typeparam name="TParam">The type of the command parameter.</typeparam>
        /// <typeparam name="TResult">The type of the command result.</typeparam>
        /// <param name="execute">The function to execute when the command is invoked.</param>
        /// <param name="canExecute">An optional function to determine whether the command can execute. If not provided, the command is always executable.</param>
        /// <param name="allowConcurrentExecution">Indicates whether concurrent executions of the command are allowed.</param>
        /// <returns>A new instance of <see cref="FunctionCommand{TParam, TDependency, TResult}"/>.</returns>
        public static IFunctionCommand<TParam, TResult> Create<TParam, TResult>(
            Func<TParam, TResult> execute,
            Func<bool> canExecute = null,
            bool allowConcurrentExecution = true)
        {
            if (canExecute == null)
            {
                canExecute = () => true;
            }

            return new FunctionCommand<TParam, bool, TResult>(
                (param, can) => execute(param),
                (param, can) => can,
                canExecute,
                allowConcurrentExecution);
        }

        /// <summary>
        /// Creates a new instance of a function-based command with the specified execution and optional validation
        /// logic.
        /// </summary>
        /// <remarks>
        /// <paramref name="canExecute"/> in this function will track dependencies and make the command reactive.
        /// </remarks>
        /// <typeparam name="TParam">The type of the command parameter.</typeparam>
        /// <param name="execute">The action to execute when the command is invoked.</param>
        /// <param name="canExecute">An optional function to determine whether the command can execute. If not provided, the command is always executable.</param>
        /// <param name="allowConcurrentExecution">Indicates whether concurrent executions of the command are allowed.</param>
        /// <returns>A new instance of <see cref="FunctionCommand{TParam, TDependency, TResult}"/>.</returns>
        public static IFunctionCommand<TParam> Create<TParam>(
            Action<TParam> execute,
            Func<bool> canExecute = null,
            bool allowConcurrentExecution = true)
        {
            if (canExecute == null)
            {
                canExecute = () => true;
            }
            return new FunctionCommand<TParam>(
                (param, can) =>
                {
                    execute(param);
                    return true;
                },
                (param, can) => can,
                canExecute,
                allowConcurrentExecution);
        }

        /// <summary>
        /// Creates a new instance of a function-based command with the specified execution and validation logic.
        /// </summary>
        /// <remarks>
        /// This overload does not track dependencies. To make the command reactive, use the overload that accepts
        /// a dependency selector.
        /// </remarks>
        /// <typeparam name="TParam">The type of the command parameter.</typeparam>
        /// <typeparam name="TResult">The type of the command result.</typeparam>
        /// <param name="execute">The function to execute when the command is invoked.</param>
        /// <param name="canExecute">A function to determine whether the command can execute.</param>
        /// <param name="allowConcurrentExecution">Indicates whether concurrent executions of the command are allowed.</param>
        /// <returns>A new instance of <see cref="FunctionCommand{TParam, TDependency, TResult}"/>.</returns>
        public static IFunctionCommand<TParam, TResult> Create<TParam, TResult>(
            Func<TParam, TResult> execute,
            Func<TParam, bool> canExecute,
            bool allowConcurrentExecution = true)
        {
            return new FunctionCommand<TParam, bool, TResult>(
                (param, can) => execute(param),
                (param, can) => canExecute(param),
                () => true,
                allowConcurrentExecution);
        }

        /// <summary>
        /// Creates a new instance of a function-based command with the specified execution and validation
        /// logic.
        /// </summary>
        /// <remarks>
        /// This overload does not track dependencies. To make the command reactive, use the overload that accepts
        /// a dependency selector.
        /// </remarks>
        /// <typeparam name="TParam">The type of the command parameter.</typeparam>
        /// <param name="execute">The action to execute when the command is invoked.</param>
        /// <param name="canExecute">A function to determine whether the command can execute.</param>
        /// <param name="allowConcurrentExecution">Indicates whether concurrent executions of the command are allowed.</param>
        /// <returns>A new instance of <see cref="FunctionCommand{TParam, TDependency, TResult}"/>.</returns>
        public static IFunctionCommand<TParam> Create<TParam>(
            Action<TParam> execute,
            Func<TParam, bool> canExecute,
            bool allowConcurrentExecution = true)
        {
            return new FunctionCommand<TParam>(
                (param, can) =>
                {
                    execute(param);
                    return true;
                },
                (param, can) => canExecute(param),
                () => true,
                allowConcurrentExecution);
        }

        /// <summary>
        /// Creates a new instance of an asynchronous function-based command with the specified execution,
        /// validation logic and dependency tracking.
        /// </summary>
        /// <remarks>
        /// <paramref name="canExecute"/> in this function does not track dependencies. To make the command reactive,
        /// use <paramref name="dependencySelector"/> to provide a function that returns the dependencies to track.
        /// </remarks>
        /// <typeparam name="TParam">The type of the command parameter.</typeparam>
        /// <typeparam name="TResult">The type of the command result.</typeparam>
        /// <typeparam name="TDependency">The type of the dependencies to track.</typeparam>
        /// <param name="executeAsync">The asynchronous function to execute when the command is invoked.</param>
        /// <param name="canExecute">A function to determine whether the command can execute.</param>
        /// <param name="dependencySelector">A function to select dependencies to track for re-evaluating command executability.</param>
        /// <param name="allowConcurrentExecution">Indicates whether concurrent executions of the command are allowed.</param>
        /// <param name="executionScheduler">An optional task scheduler to control the context in which the command executes.</param>
        /// <returns>A new instance of <see cref="AsyncFunctionCommand{TParam, TDependency, TResult}"/>.</returns>
        public static AsyncFunctionCommand<TParam, TDependency, TResult> CreateFromTask<TParam, TDependency, TResult>(
            Func<TParam, TDependency, CancellationToken, Task<TResult>> executeAsync,
            Func<TParam, TDependency, bool> canExecute,
            Func<TDependency> dependencySelector,
            bool allowConcurrentExecution = true,
            TaskScheduler executionScheduler = null)
        {
            return new AsyncFunctionCommand<TParam, TDependency, TResult>(
                executeAsync,
                canExecute,
                dependencySelector,
                allowConcurrentExecution,
                executionScheduler);
        }

        /// <summary>
        /// Creates a new instance of an asynchronous function-based command with the specified execution and optional
        /// validation logic.
        /// </summary>
        /// <remarks>
        /// <paramref name="canExecute"/> in this function will track dependencies and make the command reactive.
        /// </remarks>
        /// <typeparam name="TParam">The type of the command parameter.</typeparam>
        /// <typeparam name="TResult">The type of the command result.</typeparam>
        /// <param name="executeAsync">The asynchronous function to execute when the command is invoked.</param>
        /// <param name="canExecute">An optional function to determine whether the command can execute. If not provided, the command is always executable.</param>
        /// <param name="allowConcurrentExecution">Indicates whether concurrent executions of the command are allowed.</param>
        /// <param name="executionScheduler">An optional task scheduler to control the context in which the command executes.</param>
        /// <returns>A new instance of <see cref="AsyncFunctionCommand{TParam, TDependency, TResult}"/>.</returns>
        public static IAsyncFunctionCommand<TParam, TResult> CreateFromTask<TParam, TResult>(
            Func<TParam, CancellationToken, Task<TResult>> executeAsync,
            Func<bool> canExecute = null,
            bool allowConcurrentExecution = true,
            TaskScheduler executionScheduler = null)
        {
            if (canExecute == null)
            {
                canExecute = () => true;
            }
            return new AsyncFunctionCommand<TParam, bool, TResult>(
                (param, can, token) => executeAsync(param, token),
                (param, can) => can,
                canExecute,
                allowConcurrentExecution,
                executionScheduler);
        }

        /// <summary>
        /// Creates a new instance of an asynchronous function-based command with the specified execution and optional
        /// validation logic.
        /// </summary>
        /// <remarks>
        /// <paramref name="canExecute"/> in this function will track dependencies and make the command reactive.
        /// </remarks>
        /// <typeparam name="TParam">The type of the command parameter.</typeparam>
        /// <param name="executeAsync">The asynchronous function to execute when the command is invoked.</param>
        /// <param name="canExecute">An optional function to determine whether the command can execute. If not provided, the command is always executable.</param>
        /// <param name="allowConcurrentExecution">Indicates whether concurrent executions of the command are allowed.</param>
        /// <param name="executionScheduler">An optional task scheduler to control the context in which the command executes.</param>
        /// <returns>A new instance of <see cref="AsyncFunctionCommand{TParam, TDependency, TResult}"/>.</returns>
        public static IAsyncFunctionCommand<TParam> CreateFromTask<TParam>(
            Func<TParam, CancellationToken, Task> executeAsync,
            Func<bool> canExecute = null,
            bool allowConcurrentExecution = true,
            TaskScheduler executionScheduler = null)
        {
            if (canExecute == null)
            {
                canExecute = () => true;
            }
            return new AsyncFunctionCommand<TParam>(
                async (param, can, token) =>
                {
                    await executeAsync(param, token);
                    return true;
                },
                (param, can) => can,
                canExecute,
                allowConcurrentExecution,
                executionScheduler);
        }

        /// <summary>
        /// Creates a new instance of an asynchronous function-based command with the specified execution and validation
        /// logic.
        /// </summary>
        /// <remarks>
        /// This overload does not track dependencies. To make the command reactive, use the overload that accepts
        /// a dependency selector.
        /// </remarks>
        /// <typeparam name="TParam">The type of the command parameter.</typeparam>
        /// <typeparam name="TResult">The type of the command result.</typeparam>
        /// <param name="executeAsync">The asynchronous function to execute when the command is invoked.</param>
        /// <param name="canExecute">A function to determine whether the command can execute.</param>
        /// <param name="allowConcurrentExecution">Indicates whether concurrent executions of the command are allowed.</param>
        /// <param name="executionScheduler">An optional task scheduler to control the context in which the command executes.</param>
        /// <returns>A new instance of <see cref="AsyncFunctionCommand{TParam, TDependency, TResult}"/>.</returns>
        public static IAsyncFunctionCommand<TParam, TResult> CreateFromTask<TParam, TResult>(
            Func<TParam, CancellationToken, Task<TResult>> executeAsync,
            Func<TParam, bool> canExecute,
            bool allowConcurrentExecution = true,
            TaskScheduler executionScheduler = null)
        {
            return new AsyncFunctionCommand<TParam, bool, TResult>(
                (param, can, token) => executeAsync(param, token),
                (param, can) => canExecute(param),
                () => true,
                allowConcurrentExecution,
                executionScheduler);
        }

        /// <summary>
        /// Creates a new instance of an asynchronous function-based command with the specified execution and validation
        /// logic.
        /// </summary>
        /// <remarks>
        /// This overload does not track dependencies. To make the command reactive, use the overload that accepts
        /// a dependency selector.
        /// </remarks>
        /// <typeparam name="TParam">The type of the command parameter.</typeparam>
        /// <param name="executeAsync">The asynchronous function to execute when the command is invoked.</param>
        /// <param name="canExecute">A function to determine whether the command can execute.</param>
        /// <param name="allowConcurrentExecution">Indicates whether concurrent executions of the command are allowed.</param>
        /// <param name="executionScheduler">An optional task scheduler to control the context in which the command executes.</param>
        /// <returns>A new instance of <see cref="AsyncFunctionCommand{TParam, TDependency, TResult}"/>.</returns>
        public static IAsyncFunctionCommand<TParam> CreateFromTask<TParam>(
            Func<TParam, CancellationToken, Task> executeAsync,
            Func<TParam, bool> canExecute,
            bool allowConcurrentExecution = true,
            TaskScheduler executionScheduler = null)
        {
            return new AsyncFunctionCommand<TParam>(
                async (param, can, token) =>
                {
                    await executeAsync(param, token);
                    return true;
                },
                (param, can) => canExecute(param),
                () => true,
                allowConcurrentExecution,
                executionScheduler);
        }

        public static void TraceExecutionFailure<TParam>(object sender, FunctionCommandExecutionFailedEventArgs<TParam> eventArgs)
        {
            System.Diagnostics.Trace.TraceError($"FunctionCommand {sender.GetHashCode()} execution failed. Parameter: {eventArgs.Parameter}, Exception: {eventArgs.Exception}");
        }
    }
}
