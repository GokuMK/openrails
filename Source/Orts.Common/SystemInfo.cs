// COPYRIGHT 2009 - 2023 by the Open Rails project.
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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;

namespace ORTS.Common
{
    public static class SystemInfo
    {
        static readonly Regex NameAndVersionRegex = new Regex("^(.*?) +([0-9.]+)$");
        static readonly NativeStructs.MemoryStatusExtended MemoryStatusExtended = new NativeStructs.MemoryStatusExtended()
        {
            Size = 64
        };

        static SystemInfo()
        {
            Application = new Platform
            {
                Name = ApplicationInfo.ProductName,
                Version = VersionInfo.VersionOrBuild,
                Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            };

            var runtime = NameAndVersionRegex.Match(RuntimeInformation.FrameworkDescription.Trim());
            Runtime = new Platform
            {
                Name = runtime.Groups[1].Value,
                Version = runtime.Groups[2].Value,
            };

            // SPIKE(linux): WMI, DXGI and D3D11 probing replaced by managed runtime data.
            OperatingSystem = new Platform
            {
                Name = RuntimeInformation.OSDescription,
                Version = Environment.OSVersion.Version.ToString(),
                Architecture = RuntimeInformation.OSArchitecture.ToString(),
                Language = CultureInfo.CurrentUICulture.IetfLanguageTag,
            };
            InstalledMemoryMB = (int)(GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024);
            CPUs.Add(new CPU { Name = RuntimeInformation.ProcessArchitecture.ToString(), ThreadCount = (uint)Environment.ProcessorCount });
        }

        public static readonly Platform Application;
        public static readonly Platform Runtime;
        public static readonly Platform OperatingSystem;
        public static readonly int InstalledMemoryMB;
        public static readonly List<CPU> CPUs = new List<CPU>();
        public static readonly List<GPU> GPUs = new List<GPU>();
        public static readonly List<string> Direct3DFeatureLevels = new List<string>();

        public static void WriteSystemDetails(TextWriter output)
        {
            output.WriteLine("Date/time   = {0} ({1:u})",
            DateTime.Now, DateTime.UtcNow);
            output.WriteLine("Application = {0} {1} ({2})", Application.Name, Application.Version, Application.Architecture);
            output.WriteLine("Runtime     = {0} {1}", Runtime.Name, Runtime.Version);
            output.WriteLine("System      = {0} {1} ({2}; {3}; {4})", OperatingSystem.Name, OperatingSystem.Version, OperatingSystem.Architecture, OperatingSystem.Language, string.Join(",", OperatingSystem.Languages ?? new string[0]));
            output.WriteLine("Memory      = {0:N0} MB", InstalledMemoryMB);
            foreach (var cpu in CPUs) output.WriteLine("CPU         = {0} ({1}; {2} threads; {3:N0} MHz)", cpu.Name, cpu.Manufacturer, cpu.ThreadCount, cpu.MaxClockMHz);
            foreach (var gpu in GPUs) output.WriteLine("GPU         = {0} ({1}; {2:N0} MB)", gpu.Name, gpu.Manufacturer, gpu.MemoryMB);
            output.WriteLine("Direct3D    = {0}", string.Join(",", Direct3DFeatureLevels));
        }

        public struct Platform
        {
            public string Name;
            public string Version;
            public string Architecture;
            public string Language;
            public string[] Languages;
        }

        public struct CPU
        {
            public string Name;
            public string Manufacturer;
            public uint ThreadCount;
            public uint MaxClockMHz;
        }

        public struct GPU
        {
            public string Name;
            public string Manufacturer;
            public uint MemoryMB;
        }

        static class NativeStructs
        {
            [StructLayout(LayoutKind.Sequential, Size = 64)]
            public class MemoryStatusExtended
            {
                public uint Size;
                public uint MemoryLoad;
                public ulong TotalPhysical;
                public ulong AvailablePhysical;
                public ulong TotalPageFile;
                public ulong AvailablePageFile;
                public ulong TotalVirtual;
                public ulong AvailableVirtual;
                public ulong AvailableExtendedVirtual;
            }
        }

        static class NativeMethods
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool GlobalMemoryStatusEx([In, Out] NativeStructs.MemoryStatusExtended buffer);

            public const uint D3D11_SDK_VERSION = 7;

            public const uint D3D_DRIVER_TYPE_HARDWARE = 1;

            public const uint D3D_FEATURE_LEVEL_9_1 = 0x9100;
            public const uint D3D_FEATURE_LEVEL_9_2 = 0x9200;
            public const uint D3D_FEATURE_LEVEL_9_3 = 0x9300;
            public const uint D3D_FEATURE_LEVEL_10_0 = 0xA000;
            public const uint D3D_FEATURE_LEVEL_10_1 = 0xA100;
            public const uint D3D_FEATURE_LEVEL_11_0 = 0xB000;
            public const uint D3D_FEATURE_LEVEL_11_1 = 0xB100;
            public const uint D3D_FEATURE_LEVEL_12_0 = 0xC000;
            public const uint D3D_FEATURE_LEVEL_12_1 = 0xC100;
            public const uint D3D_FEATURE_LEVEL_12_2 = 0xC200;

            [DllImport("d3d11.dll", ExactSpelling = true)]
            public static extern uint D3D11CreateDevice(
                IntPtr adapter,
                uint driverType,
                IntPtr software,
                uint flags,
                [In] uint[] featureLevels,
                int featureLevelCount,
                uint sdkVersion,
                IntPtr device,
                out uint featureLevel,
                IntPtr immediateContext
            );
        }
    }
}
