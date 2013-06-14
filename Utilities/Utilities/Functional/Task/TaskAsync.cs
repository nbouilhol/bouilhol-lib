﻿using System;
using System.Diagnostics.Contracts;
using System.Threading;

namespace Utilities.Functional.Task
{
    public class TaskAsync : ITaskCancellable, IDisposable
    {
        private readonly System.Threading.Tasks.Task sysTask;
        private readonly CancellationTokenSource cancellationTokenSource;

        public TaskAsync(ITask task)
        {
            Contract.Requires(task != null);

            sysTask = new System.Threading.Tasks.Task(() => task.Do());
        }

        public TaskAsync(ITask task, CancellationTokenSource cancellationTokenSource)
        {
            Contract.Requires(task != null);
            Contract.Requires(cancellationTokenSource != null);

            this.cancellationTokenSource = cancellationTokenSource;
            CancellationToken token = cancellationTokenSource.Token;
            sysTask = new System.Threading.Tasks.Task(() =>
            {
                token.ThrowIfCancellationRequested();
                task.Do();
            }, token);
        }

        [ContractInvariantMethod]
        private void ObjectInvariant()
        {
            Contract.Invariant(sysTask != null);
        }

        public void Do()
        {
            sysTask.Start();
        }

        public void Cancel()
        {
            if (cancellationTokenSource != null) cancellationTokenSource.Cancel();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing) if (sysTask != null) sysTask.Dispose();
        }

        //~TaskAsync()
        //{
        //    Dispose(false);
        //}
    }
}