// COPYRIGHT 2026 by the Open Rails project.
//
// This file is part of Open Rails.
//
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace ORTS.Common
{
    /// <summary>
    /// Runs graphics resource work on the render thread when the graphics backend requires it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MonoGame WindowsDX allows graphics resources to be created on any thread, so work runs inline on the
    /// calling thread. MonoGame DesktopGL marshals every resource operation to the main thread and only serves
    /// that queue between game ticks, which deadlocks when the render thread is waiting for the thread that
    /// creates the resource. With a queued dispatcher, callers submit whole units of work (for example every
    /// buffer of a terrain tile) and the render thread runs them once per frame and while it waits for other
    /// processes, so MonoGame sees the work on its own thread.
    /// </para>
    /// <para>
    /// Callers should prepare CPU-side data on their own thread and submit one unit of work per logical object,
    /// not one per buffer.
    /// </para>
    /// </remarks>
    public static class GpuDispatcher
    {
        sealed class WorkItem
        {
            public Action Work;
            public ManualResetEventSlim Done;
            public ExceptionDispatchInfo Error;
        }

        static readonly Queue<WorkItem> Queue = new Queue<WorkItem>();
        static readonly AutoResetEvent QueueSignal = new AutoResetEvent(false);
        static int RenderThreadId;
        static bool Queued;

        /// <summary>
        /// Gets whether work from other threads is queued to the render thread.
        /// </summary>
        public static bool IsQueued => Queued;

        /// <summary>
        /// Gets whether the calling thread is the render thread.
        /// </summary>
        public static bool IsRenderThread => Thread.CurrentThread.ManagedThreadId == RenderThreadId;

        /// <summary>
        /// Sets the calling thread as the render thread and selects inline or queued dispatch.
        /// </summary>
        /// <param name="queued"><c>true</c> when the graphics backend requires resources to be created on the render thread.</param>
        public static void Initialize(bool queued)
        {
            RenderThreadId = Thread.CurrentThread.ManagedThreadId;
            Queued = queued;
        }

        /// <summary>
        /// Runs <paramref name="work"/> on the render thread (or inline) and waits for it to finish.
        /// </summary>
        public static void Invoke(Action work)
        {
            if (!Queued || IsRenderThread)
            {
                work();
                return;
            }

            var item = new WorkItem { Work = work, Done = new ManualResetEventSlim(false) };
            Enqueue(item);
            item.Done.Wait();
            item.Done.Dispose();
            item.Error?.Throw();
        }

        /// <summary>
        /// Runs <paramref name="work"/> on the render thread (or inline) and returns its result.
        /// </summary>
        public static T Invoke<T>(Func<T> work)
        {
            var result = default(T);
            Invoke(() => { result = work(); });
            return result;
        }

        [ThreadStatic]
        static List<Action> CurrentBatch;

        /// <summary>
        /// Starts collecting <see cref="Batched"/> work on the calling thread; disposing the scope runs all of it
        /// as one unit on the render thread. Nested scopes join the outermost one.
        /// </summary>
        /// <example>
        /// <code>
        /// using (GpuDispatcher.BeginBatch())
        /// {
        ///     // Constructors that call GpuDispatcher.Batched for their buffers.
        /// }
        /// </code>
        /// </example>
        public static BatchScope BeginBatch()
        {
            if (CurrentBatch != null || !Queued || IsRenderThread)
                return new BatchScope(null);
            CurrentBatch = new List<Action>();
            return new BatchScope(CurrentBatch);
        }

        /// <summary>
        /// Runs <paramref name="work"/> as part of the current batch, or like <see cref="Invoke(Action)"/> when no batch is open.
        /// </summary>
        /// <remarks>
        /// Batched work runs later, in submission order, when the scope ends. Code in the same scope must not read the
        /// resources it creates; submit such reads as batched work too.
        /// </remarks>
        public static void Batched(Action work)
        {
            if (CurrentBatch != null)
                CurrentBatch.Add(work);
            else
                Invoke(work);
        }

        /// <summary>
        /// A batch started by <see cref="BeginBatch"/>.
        /// </summary>
        public readonly struct BatchScope : IDisposable
        {
            readonly List<Action> Batch;

            internal BatchScope(List<Action> batch)
            {
                Batch = batch;
            }

            public void Dispose()
            {
                if (Batch == null)
                    return;
                CurrentBatch = null;
                if (Batch.Count > 0)
                {
                    var batch = Batch;
                    Invoke(() =>
                    {
                        foreach (var work in batch)
                            work();
                    });
                }
            }
        }

        /// <summary>
        /// Queues <paramref name="work"/> to the render thread (or runs it inline) without waiting.
        /// </summary>
        public static void Post(Action work)
        {
            if (!Queued || IsRenderThread)
            {
                work();
                return;
            }

            Enqueue(new WorkItem { Work = work });
        }

        static void Enqueue(WorkItem item)
        {
            lock (Queue)
                Queue.Enqueue(item);
            QueueSignal.Set();
        }

        /// <summary>
        /// Runs queued work on the render thread until the queue is empty or <paramref name="budget"/> has elapsed.
        /// </summary>
        [CallOnThread("Render")]
        public static void RunPending(TimeSpan budget)
        {
            if (!Queued)
                return;
            Debug.Assert(IsRenderThread, "GpuDispatcher.RunPending must be called on the render thread.");

            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                WorkItem item;
                lock (Queue)
                {
                    if (Queue.Count == 0)
                        return;
                    item = Queue.Dequeue();
                }

                try
                {
                    item.Work();
                }
                catch (Exception error)
                {
                    if (item.Done == null)
                        Trace.WriteLine(error);
                    else
                        item.Error = ExceptionDispatchInfo.Capture(error);
                }
                item.Done?.Set();

                if (stopwatch.Elapsed >= budget)
                {
                    // Make sure the next wait or frame picks up the remainder.
                    QueueSignal.Set();
                    return;
                }
            }
        }

        /// <summary>
        /// Runs queued work on the render thread as it arrives, for up to <paramref name="duration"/>.
        /// </summary>
        /// <remarks>
        /// Used while loading, when the render thread has little to draw and the loader submits many units of work.
        /// The render thread sleeps on the queue signal between units.
        /// </remarks>
        [CallOnThread("Render")]
        public static void ServeFor(TimeSpan duration)
        {
            if (!Queued)
                return;

            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                var remaining = duration - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                    return;
                RunPending(remaining);
                remaining = duration - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero || !QueueSignal.WaitOne(remaining))
                    return;
            }
        }

        /// <summary>
        /// Gets the handle signalled when work is queued, for callers that wait with <see cref="WaitAny"/>.
        /// </summary>
        public static WaitHandle QueueWaitHandle => QueueSignal;

        /// <summary>
        /// Waits for any of <paramref name="handles"/>; on the render thread, runs queued work while waiting.
        /// </summary>
        /// <param name="handles">The handles to wait for.</param>
        /// <param name="handlesAndQueue"><paramref name="handles"/> followed by <see cref="QueueWaitHandle"/>.</param>
        /// <returns>The index of the handle in <paramref name="handles"/> that was signalled.</returns>
        public static int WaitAny(WaitHandle[] handles, WaitHandle[] handlesAndQueue)
        {
            Debug.Assert(handlesAndQueue.Length == handles.Length + 1 && handlesAndQueue[handles.Length] == QueueSignal);
            if (!Queued || !IsRenderThread)
                return WaitHandle.WaitAny(handles);

            while (true)
            {
                var index = WaitHandle.WaitAny(handlesAndQueue);
                if (index < handles.Length)
                    return index;
                RunPending(TimeSpan.MaxValue);
            }
        }
    }
}
