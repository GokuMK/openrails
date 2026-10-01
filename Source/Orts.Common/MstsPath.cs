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

using System.IO;

namespace ORTS.Common
{
    /// <summary>
    /// Converts relative paths written in MSTS/Open Rails content, which use
    /// backslash separators, to the host syntax. This is format compatibility
    /// only: it never searches the filesystem or changes the case of a name.
    /// </summary>
    public static class MstsPath
    {
        public static string ToNative(string contentPath)
        {
            if (string.IsNullOrEmpty(contentPath) || Path.DirectorySeparatorChar == '\\')
                return contentPath;
            return contentPath.Replace('\\', Path.DirectorySeparatorChar);
        }
    }
}
