using System;
using System.Threading;
using System.Threading.Tasks;

namespace EffectSharp.Tests
{
    public class FunctionCommandTests
    {
        [Fact]
        public async Task FunctionCommand_ExecutesAction_And_TrackingCanExecute()
        {
            bool invoked = false;
            var canExecute = Reactive.Ref(true);
            var cmd = FunctionCommand.Create<int>((p) => { invoked = true; }, canExecute: () => canExecute.Value);

            Assert.True(cmd.CanExecute(0));
            cmd.Execute(0);

            Assert.True(invoked);

            bool canExecuteChangedRaised = false;
            cmd.CanExecuteChanged += (s, e) => canExecuteChangedRaised = true;
            canExecute.Value = false;

            await Reactive.NextTick();

            Assert.False(cmd.CanExecute(0));
            Assert.True(canExecuteChangedRaised);
        }

        [Fact]
        public void FunctionCommand_WhenCannotExecute_RaisesExecutionFailed()
        {
            Exception? observed = null;
            var cmd = FunctionCommand.Create<int>((p) => { }, (param) => false, allowConcurrentExecution: true);
            cmd.ExecutionFailed += (s, e) => observed = e.Exception;

            // Execute should not throw; it should raise ExecutionFailed with FunctionCommandNotExecutableException
            cmd.Execute((object)0);

            Assert.NotNull(observed);
            Assert.IsType<FunctionCommandNotExecutableException>(observed);
        }

        [Fact]
        public async Task AsyncFunctionCommand_UpdatesExecutingCount_And_BlocksConcurrentExecution()
        {
            var cmd = FunctionCommand.CreateFromTask<int>(async (p, ct) =>
            {
                var tcs = new TaskCompletionSource<bool>();
                using (ct.Register(() => tcs.SetResult(true)))
                {
                    await tcs.Task;
                }
            }, allowConcurrentExecution: false);

            // Start execution but do not await yet
            var cts = new CancellationTokenSource();
            var task = cmd.Execute(0, cts.Token);

            // While running, CanExecute should be false and executing count should be 1
            Assert.False(cmd.CanExecute(0));
            Assert.Equal(1, cmd.ExecutingCount.Value);

            // Cancel the running task
            cts.Cancel();
            await task;

            // After completion, executing count should be 0 and CanExecute true
            Assert.Equal(0, cmd.ExecutingCount.Value);
            Assert.True(cmd.CanExecute(0));
        }

        [Fact]
        public async Task AsyncFunctionCommand_WhenThrows_RaisesExecutionFailed()
        {
            var tcs = new TaskCompletionSource<Exception>();
            var cmd = FunctionCommand.CreateFromTask<int>(async (p, ct) =>
            {
                await Task.Yield();
                throw new InvalidOperationException("boom");
            }, allowConcurrentExecution: true);

            cmd.ExecutionFailed += (s, e) => tcs.TrySetResult(e.Exception);

            // Invoke via Execute(object) which is an async void; it will catch and raise ExecutionFailed
            cmd.Execute((object)0);

            var ex = await tcs.Task;
            Assert.IsType<InvalidOperationException>(ex);
        }
    }
}
