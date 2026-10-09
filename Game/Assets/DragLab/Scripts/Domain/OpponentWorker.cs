using System;
using System.Threading;

namespace DragLab.Domain
{
    // One persistent worker; the caller joins each fixed tick before reading the opponent.
    // Only the already-created domain simulation runs here. No Unity APIs or player assets.
    public sealed class OpponentWorker : IDisposable
    {
        readonly AutoResetEvent start = new AutoResetEvent(false), finished = new AutoResetEvent(false);
        readonly Thread thread;
        Action work;
        Exception failure;
        bool stopping, pending;
        public OpponentWorker()
        {
            thread = new Thread(Run) { IsBackground = true, Name = "DragLab opponent physics" };
            thread.Start();
        }
        public void Begin(Action action)
        {
            if (pending || stopping) throw new InvalidOperationException("Opponent tick must be joined before another begins.");
            failure = null; work = action; pending = true; start.Set();
        }
        public void Join()
        {
            if (!pending) return;
            finished.WaitOne(); pending = false;
            if (failure != null) throw new InvalidOperationException("Opponent simulation failed.", failure);
        }
        void Run()
        {
            while (true) {
                start.WaitOne(); if (stopping) return;
                try { work(); } catch (Exception e) { failure = e; }
                finally { work = null; finished.Set(); }
            }
        }
        public void Dispose()
        {
            if (stopping) return;
            try { Join(); } finally { stopping = true; start.Set(); thread.Join(); start.Dispose(); finished.Dispose(); }
        }
    }
}
