using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffectSharp.Tests
{
    public class EffectTests
    {
        [Fact]
        public async Task Effect_WhenDependencyChanges_ReExecutesAction()
        {
            // Arrange
            var reactiveValue = Reactive.Ref(1);
            int runCount = 0;
            var effect = new Effect(() =>
            {
                _ = reactiveValue.Value; // Track dependency
                runCount++;
            }, scheduler: eff => eff.Execute());
            // Initial execution
            Assert.Equal(1, runCount);
            // Act - change the reactive value
            reactiveValue.Value = 2;
            // Assert - effect should re-execute
            Assert.Equal(2, runCount);
            // Cleanup
            effect.Dispose();
        }

        [Fact]
        public void Effect_WhenCreatedWithLazy_DoesNotExecuteImmediately()
        {
            // Arrange
            int runCount = 0;
            // Act
            var effect = new Effect(() =>
            {
                runCount++;
            }, lazy: true, scheduler: eff => eff.Execute());
            // Assert
            Assert.Equal(0, runCount);
            // Trigger execution
            effect.Execute();
            // Assert after execution
            Assert.Equal(1, runCount);
            // Cleanup
            effect.Dispose();
        }

        [Fact]
        public void Effect_Dispose_PreventsFurtherExecutions()
        {
            // Arrange
            int runCount = 0;
            var effect = new Effect(() =>
            {
                runCount++;
            }, scheduler: eff => eff.Execute());
            // Act
            effect.Dispose();
            // Simulate a dependency change that would normally trigger the effect
            effect.Execute();
            // Assert
            Assert.Equal(1, runCount); // Should not have incremented after dispose
        }

        [Fact]
        public void Effect_WhenSchedulerSpecified_UseSpecifiedScheduler()
        {
            // Arrange
            int runCount = 0;
            Effect? scheduledEffect = null;
            var effect = new Effect(() =>
            {
                runCount++;
            }, scheduler: eff => scheduledEffect = eff);
            Assert.Equal(1, runCount);
            effect.ScheduleExecution();
            // Assert
            Assert.Equal(1, runCount); // Should not have run as scheduler did not execute it
            Assert.Equal(effect, scheduledEffect);
            // Cleanup
            effect.Dispose();
        }

        [Fact]
        public void Effect_Untracked_DoesNotTrackDependencies()
        {
            // Arrange
            var reactiveValue = Reactive.Ref(1);
            int runCount = 0;
            var effect = new Effect(() =>
            {
                Effect.Untracked(() =>
                {
                    _ = reactiveValue.Value; // This should not track the dependency
                });
                runCount++;
            }, scheduler: eff => eff.Execute());
            // Initial execution
            Assert.Equal(1, runCount);
            // Act - change the reactive value
            reactiveValue.Value = 2;
            // Assert - effect should NOT re-execute
            Assert.Equal(1, runCount);
            // Cleanup
            effect.Dispose();
        }

        [Fact]
        public void Effect_WhenRecursivelyExecuted_OnlyTracksOuterDependencies()
        {
            // Arrange
            var reactiveValue = Reactive.Ref(1);
            var anotherReactiveValue = Reactive.Ref(10);
            int runCount = 0;
            var effect = new Effect(() =>
            {
                runCount++;
                // Recursive execution
                if (reactiveValue.Value < 3)
                {
                    if (reactiveValue.Value > 1)
                        _ = anotherReactiveValue.Value;
                    reactiveValue.Value++;
                }
            }, scheduler: eff => eff.Execute());
            // Initial execution
            Assert.Equal(3, runCount); // Should have executed 3 times due to recursion
            // Change inner dependency
            anotherReactiveValue.Value = 20;
            Assert.Equal(3, runCount); // Should NOT re-execute as it's not tracked
            // Cleanup
            effect.Dispose();
        }

        [Fact]
        public void Effect_WhenCallingLockedApiDuringExecution_DoesNotDeadlock()
        {
            var eff = new Effect(() => Effect.Current.Stop());
            eff.Dispose();
        }
    }
}
