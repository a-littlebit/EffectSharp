using System;
using System.Threading;
using System.Threading.Tasks;

namespace EffectSharp
{
    /// <summary>
    /// Async reentrant lock using explicit Scope passing.
    /// </summary>
    public sealed class AsyncLock
    {
        private readonly SemaphoreSlim _semaphore;

        public AsyncLock()
        {
            _semaphore = new SemaphoreSlim(1, 1);
        }

        /// <summary>
        /// Enter the lock.
        /// If <paramref name="existingScope"/> is null -> acquire new lock and return a new Scope.
        /// If <paramref name="existingScope"/> is non-null -> attempt to increment its refcount (reenter).
        /// </summary>
        public async Task<Scope> EnterAsync(
            Scope existingScope = null,
            CancellationToken cancellationToken = default)
        {
            if (existingScope != null)
            {
                // validate scope belongs to this lock
                if (!ReferenceEquals(existingScope.Owner, this))
                    throw new InvalidOperationException("The provided scope does not belong to this AsyncLock.");

                // attempt to increment refcount atomically, but fail if it is already 0 (released)
                existingScope.IncrementIfHeldOrThrow();
                return existingScope;
            }

            // acquire new ownership
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new Scope(this);
        }

        /// <summary>
        /// Synchronous enter.
        /// If <paramref name="existingScope"/> is null, blocks until the lock is acquired.
        /// If non-null, performs a reentrant increment.
        /// </summary>
        public Scope Enter(Scope existingScope = null)
        {
            if (existingScope != null)
            {
                if (!ReferenceEquals(existingScope.Owner, this))
                    throw new InvalidOperationException("The provided scope does not belong to this AsyncLock.");

                existingScope.IncrementIfHeldOrThrow();
                return existingScope;
            }

            // Block until the semaphore can be acquired
            _semaphore.Wait();
            return new Scope(this);
        }

        /// <summary>
        /// Attempts to synchronously enter the lock.  
        /// If <paramref name="scope"/> is null, attempts a non-blocking acquisition.
        /// If non-null, attempts a reentrant increment.
        /// </summary>
        /// <returns>true if lock/reentry succeeded; false if lock is taken by another thread.</returns>
        public bool TryEnter(ref Scope scope)
        {
            if (scope != null)
            {
                // ensure scope belongs to this lock
                if (!ReferenceEquals(scope.Owner, this))
                    throw new InvalidOperationException("Scope does not belong to this AsyncLock.");

                // reentrant increment (may throw if scope already released)
                scope.IncrementIfHeldOrThrow();
                return true;
            }

            // Non-blocking attempt to acquire semaphore
            if (_semaphore.Wait(0))
            {
                scope = new Scope(this);
                return true;
            }

            // lock already held by someone else; no change
            return false;
        }

        /// <summary>
        /// Attempts to synchronously acquire the lock without blocking.
        /// This method does NOT support reentrancy and does not take an
        /// existing Scope; it either acquires a brand-new ownership scope
        /// or immediately returns <c>null</c>.
        ///
        /// Use this when you do not have (or do not want) an existing Scope,
        /// and only want a quick "try-lock" check.
        /// 
        /// Returns:
        ///   A new <see cref="Scope"/> instance if the lock was acquired;
        ///   otherwise, <c>null</c>.
        /// </summary>
        public Scope TryEnter()
        {
            // Attempt non-blocking acquisition.
            if (_semaphore.Wait(0))
            {
                // Successfully acquired the lock.
                return new Scope(this);
            }

            // Lock already held by another thread; cannot acquire.
            return null;
        }

        private void Exit()
        {
            _semaphore.Release();
        }

        /// <summary>
        /// Represents ownership of the lock and supports explicit reentrancy.
        /// Disposal decrements the refcount; when it reaches zero, the lock is released.
        /// </summary>
        public sealed class Scope : IDisposable
        {
            // ref count: number of outstanding "Enters" on this scope.
            // invariant: refCount >= 0. 0 means fully released.
            private int _refCount; // starts at 1 for a newly created scope
            internal AsyncLock Owner;

            internal Scope(AsyncLock owner)
            {
                Owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _refCount = 1;
            }

            /// <summary>
            /// Returns true if scope currently holds the lock (refCount > 0).
            /// </summary>
            public bool IsHeld => Volatile.Read(ref _refCount) > 0;

            /// <summary>
            /// Atomically increment refcount if currently > 0; otherwise throw.
            /// This implements safe reenter semantics.
            /// </summary>
            internal void IncrementIfHeldOrThrow()
            {
                while (true)
                {
                    int current = Volatile.Read(ref _refCount);
                    if (current <= 0)
                    {
                        // scope already released
                        throw new ObjectDisposedException(nameof(Scope), "Cannot reenter a scope that has been released.");
                    }

                    // try to CAS current -> current + 1
                    if (Interlocked.CompareExchange(ref _refCount, current + 1, current) == current)
                    {
                        // successfully increased
                        return;
                    }

                    // otherwise, retry
                }
            }

            /// <summary>
            /// Dispose decrements the refcount. When it reaches zero, the lock is released.
            /// Multiple Dispose calls are safe (idempotent).
            /// </summary>
            public void Dispose()
            {
                // We do NOT use a disposed flag to block repeated Dispose() calls from
                // doing anything harmful; instead we atomically decrement refCount and
                // ensure only the transition to zero triggers a single Exit().
                var newRefCount = Interlocked.Decrement(ref _refCount);
                if (newRefCount == 0)
                {
                    // last Dispose, release the lock
                    Owner.Exit();
                    Owner = null;
                }
                else if (newRefCount < 0)
                {
                    // Dispose called too many times
                    Interlocked.Increment(ref _refCount); // revert decrement
                    throw new ObjectDisposedException(nameof(Scope), "Scope has already been fully released.");
                }
            }
        }
    }
}
