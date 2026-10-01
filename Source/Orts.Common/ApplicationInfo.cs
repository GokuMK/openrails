// COPYRIGHT 2009 - 2024 by the Open Rails project.
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
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace ORTS.Common
{
    public static class ApplicationInfo
    {
        public static string ProcessFile => Process.GetCurrentProcess().MainModule.FileName;
        // The application directory, also when started as "dotnet <app>.dll".
        public static string ProcessDirectory => Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        // Read from assembly metadata: a native app host outside Windows has no version resource.
        static Assembly EntryAssembly => Assembly.GetEntryAssembly();
        public static string ProductName => EntryAssembly?.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        public static string ApplicationName => EntryAssembly?.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
    }
}
