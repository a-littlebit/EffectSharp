using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace EffectSharp
{
    /// <summary>
    /// Reactive command interface with execution tracking and failure notification.
    /// </summary>
    /// <typeparam name="TParam">Command parameter type.</typeparam>
    public interface IReactiveCommand<TParam> : ICommand
    {
        /// <summary>
        /// Read-only count of ongoing command executions.
        /// </summary>
        IReadOnlyRef<int> ExecutingCount { get; }
        /// <summary>
        /// Raised when command execution throws an exception.
        /// </summary>
        event EventHandler<FunctionCommandExecutionFailedEventArgs<TParam>> ExecutionFailed;

        /// <summary>
        /// Returns whether the command can execute with the given parameter.
        /// </summary>
        bool CanExecute(TParam parameter);
        /// <summary>
        /// Raises <see cref="ICommand.CanExecuteChanged"/> to notify UI of executability changes.
        /// </summary>
        void RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Synchronous function command.
    /// </summary>
    public interface IFunctionCommand<TParam, TResult> : IReactiveCommand<TParam>
    {
        /// <summary>
        /// Executes the command and returns the result.
        /// </summary>
        TResult Execute(TParam parameter);
    }

    public interface IFunctionCommand<TParam> : IFunctionCommand<TParam, bool>
    {
    }

    /// <summary>
    /// Asynchronous function command.
    /// </summary>
    public interface IAsyncFunctionCommand<TParam, TResult> : IReactiveCommand<TParam>
    {
        /// <summary>
        /// Asynchronously executes the command and returns the result.
        /// </summary>
        Task<TResult> Execute(TParam parameter, CancellationToken cancellationToken = default);
    }

    public interface IAsyncFunctionCommand<TParam> : IAsyncFunctionCommand<TParam, bool>
    {
    }

    /// <summary>
    /// Base implementation for reactive commands with dependency-driven executability and execution tracking.
    /// </summary>
    public abstract class FunctionCommandBase<TParam, TDependency, TResult> : IReactiveCommand<TParam>, ICommand, IDisposable
    {
        private readonly Func<TParam, TDependency, bool> _canExecute;
        private readonly bool _allowConcurrentExecution;

        private readonly Func<TDependency> _dependencyGetter;
        private readonly Effect _effect;
        private AtomicIntRef _executingCount = new AtomicIntRef(0);

        /// <summary>
        /// Raised when command executability changes.
        /// </summary>
        public event EventHandler CanExecuteChanged;
        /// <summary>
        /// Raised when execution fails with an exception.
        /// </summary>
        public event EventHandler<FunctionCommandExecutionFailedEventArgs<TParam>> ExecutionFailed;

        /// <summary>
        /// Read-only count of ongoing executions.
        /// </summary>
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
                }, immediate: true, scheduler: eff => eff.Execute());
                _dependencyGetter = () => dependencyValue.Value;
            }
            else
            {
                _dependencyGetter = () => default;
            }
        }

        /// <summary>
        /// Casts and validates the command parameter.
        /// </summary>
        protected TParam CastParameter(object parameter)
        {
            if (parameter != null && !(parameter is TParam))
                throw new ArgumentException($"Parameter must be of type {typeof(TParam).FullName}.", nameof(parameter));

            if (parameter == null && default(TParam) != null)
                throw new ArgumentNullException(nameof(parameter), $"Parameter of type {typeof(TParam).FullName} cannot be null.");

            return (TParam)parameter;
        }

        /// <summary>
        /// Notifies listeners that the result of <see cref="CanExecute(TParam)"/> may have changed.
        /// </summary>
        public virtual void RaiseCanExecuteChanged()
        {
            if (CanExecuteChanged != null)
            {
                TaskManager.QueueNotification(this, nameof(CanExecuteChanged), (args) =>
                {
                    CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                });
            }
        }

        /// <summary>
        /// Invokes the <see cref="ExecutionFailed"/> event.
        /// </summary>
        protected virtual void OnExecutionFailed(FunctionCommandExecutionFailedEventArgs<TParam> args)
        {
            ExecutionFailed?.Invoke(this, args);
        }

        public bool CanExecute(object parameter)
        {
            return CanExecute(CastParameter(parameter));
        }

        /// <summary>
        /// Returns whether the command can execute given the parameter and current dependency value.
        /// </summary>
        public bool CanExecute(TParam parameter)
        {
            if (!_allowConcurrentExecution && _executingCount.Value != 0)
                return false;
            var dependencyValue = _dependencyGetter();
            return _canExecute(parameter, dependencyValue);
        }

        /// <summary>
        /// Begins execution: validates executability and updates execution counters.
        /// </summary>
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

        /// <summary>
        /// Ends execution: updates execution counters and notifies executability changes.
        /// </summary>
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

        /// <summary>
        /// Non-generic ICommand entry point.
        /// </summary>
        public abstract void Execute(object parameter);

        /// <summary>
        /// Disposes internal reactive resources.
        /// </summary>
        public void Dispose()
        {
            _effect?.Dispose();
        }
    }

    /// <summary>
    /// Synchronous reactive function command.
    /// </summary>
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
            try
            {
                Execute(CastParameter(parameter));
            }
            catch (Exception ex)
            {
                OnExecutionFailed(new FunctionCommandExecutionFailedEventArgs<TParam>(ex, (TParam)parameter));
            }
        }

        /// <summary>
        /// Executes the command and returns the result.
        /// </summary>
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

    /// <summary>
    /// Asynchronous reactive function command.
    /// </summary>
    public class AsyncFunctionCommand<TParam, TDependency, TResult> : FunctionCommandBase<TParam, TDependency, TResult>, IAsyncFunctionCommand<TParam, TResult>
    {
        private readonly Func<TParam, TDependency, CancellationToken, Task<TResult>> _executeAsync;
        private readonly TaskScheduler _executionScheduler;

        /// <summary>
        /// Optional task scheduler used to run the asynchronous execution.
        /// </summary>
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
            try
            {
                await Execute(CastParameter(parameter));
            }
            catch (Exception ex)
            {
                OnExecutionFailed(new FunctionCommandExecutionFailedEventArgs<TParam>(ex, (TParam)parameter));
            }
        }

        /// <summary>
        /// Asynchronously executes the command and returns the result.
        /// </summary>
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

    /// <summary>
    /// Synchronous reactive function command with boolean dependency.
    /// </summary>
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

    /// <summary>
    /// Asynchronous reactive function command with boolean dependency.
    /// </summary>
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

    /// <summary>
    /// Event args carrying execution failure details for a command.
    /// </summary>
    public class FunctionCommandExecutionFailedEventArgs<TParam> : EventArgs
    {
        /// <summary>
        /// The exception thrown during execution.
        /// </summary>
        public Exception Exception { get; }
        /// <summary>
        /// The parameter used for the failed execution.
        /// </summary>
        public TParam Parameter { get; }
        public FunctionCommandExecutionFailedEventArgs(Exception exception, TParam parameter)
        {
            Exception = exception;
            Parameter = parameter;
        }
    }

    /// <summary>
    /// Exception thrown when a command is invoked while not executable.
    /// </summary>
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
