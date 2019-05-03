using System;
using System.Threading;

namespace Util.Concurrency
{
    public struct DisposableReadLock : IDisposable
    {
        readonly ReaderWriterLockSlim myLock;

        public unsafe DisposableReadLock(ReaderWriterLockSlim @lock)
        {
            *(DisposableReadLock*) ref this = new DisposableReadLock();
            myLock = @lock;
        }

        public void Dispose()
        {
            myLock?.ExitReadLock();
        }
    }

    public static class ReaderWriterLockSlimExtension
    {
        public static DisposableReadLock UsingReadLock(this ReaderWriterLockSlim thіs)
        {
            thіs.EnterReadLock();
            return new DisposableReadLock(thіs);
        }
    }
}
