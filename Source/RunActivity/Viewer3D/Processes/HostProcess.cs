// COPYRIGHT 2022 by the Open Rails project.
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
using System.Linq;
using System.Threading;
using Orts.Processes;
using ORTS.Common;

namespace Orts.Viewer3D.Processes
{
    /// <summary>
    /// The host process is used to collect and provide details about the
    /// host environment and the application's consumption of resources,
    /// such as CPU, GPU, and memory usage.
    /// </summary>
    public class HostProcess
    {
        public int ProcessorCount { get; } = System.Environment.ProcessorCount;
        public float CLRMemoryAllocatedBytesPerSec { get; private set; }
        public float CPUMemoryPrivate { get; private set; }
        public float CPUMemoryWorkingSet { get; private set; }
        public float CPUMemoryWorkingSetPrivate { get; private set; }
        public float CPUMemoryVirtual { get; private set; }
        public ulong CPUMemoryVirtualLimit { get; private set; }
        public float GPUMemoryCommitted { get; private set; }
        public float GPUMemoryDedicated { get; private set; }
        public float GPUMemoryShared { get; private set; }

        // SPIKE(linux): Windows performance counters replaced by managed process and GC metrics.
        long LastAllocatedBytes;
        DateTime LastAllocatedTime;

        readonly Profiler Profiler = new Profiler("Host");
        readonly ProcessState State = new ProcessState("Host");
        readonly Game Game;
        readonly Thread Thread;

        private const int SleepTime = 10000;

        public HostProcess(Game game)
        {
            Debug.Assert(GC.MaxGeneration == 2, "Runtime is expected to have a MaxGeneration of 2.");

            Game = game;
            Thread = new Thread(HostThread);
        }

        public void Start()
        {
            Thread.Start();
        }

        public void Stop()
        {
            State.SignalTerminate();
        }

        [ThreadName("Host")]
        void HostThread()
        {
            Profiler.SetThread();

            CPUMemoryVirtualLimit = (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            LastAllocatedBytes = GC.GetTotalAllocatedBytes();
            LastAllocatedTime = DateTime.UtcNow;

            while (true)
            {
                State.Sleep(SleepTime);
                if (State.Terminated)
                    break;
                if (!DoHost())
                    return;
            }
        }

        [CallOnThread("Host")]
        bool DoHost()
        {
            if (Debugger.IsAttached)
            {
                Host();
            }
            else
            {
                try
                {
                    Host();
                }
                catch (Exception error)
                {
                    // We ignore all errors because the data is non-critical
                    // Trace.WriteLine(error);
                    Game.ProcessReportError(error);
                }
            }
            return true;
        }

        [CallOnThread("Host")]
        void Host()
        {
            Profiler.Start();
            try
            {
                using (var process = Process.GetCurrentProcess())
                {
                    CPUMemoryPrivate = process.PrivateMemorySize64;
                    CPUMemoryWorkingSet = process.WorkingSet64;
                    CPUMemoryWorkingSetPrivate = process.PrivateMemorySize64;
                    CPUMemoryVirtual = process.VirtualMemorySize64;
                }

                var allocatedBytes = GC.GetTotalAllocatedBytes();
                var allocatedTime = DateTime.UtcNow;
                var seconds = (allocatedTime - LastAllocatedTime).TotalSeconds;
                if (seconds > 0)
                    CLRMemoryAllocatedBytesPerSec = (float)((allocatedBytes - LastAllocatedBytes) / seconds);
                LastAllocatedBytes = allocatedBytes;
                LastAllocatedTime = allocatedTime;
            }
            finally
            {
                Profiler.Stop();
            }
        }
    }
}
