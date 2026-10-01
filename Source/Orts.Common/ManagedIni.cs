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
using System.IO;
using System.Linq;
using System.Text;

namespace ORTS.Common
{
    /// <summary>
    /// Managed replacement for the Win32 private-profile (INI) functions used by
    /// <see cref="SettingsStoreLocalIni"/>. It follows the Win32 behaviour that
    /// Open Rails relies on: section and key names are case-insensitive, names and
    /// values are trimmed, one pair of surrounding quotes is removed from values,
    /// a <c>null</c> value deletes the key, and missing sections and keys are appended.
    /// </summary>
    public static class ManagedIni
    {
        static readonly object Lock = new object();

        static List<string> ReadLines(string filePath)
        {
            return File.Exists(filePath) ? File.ReadAllLines(filePath).ToList() : new List<string>();
        }

        static bool IsSectionHeader(string line, out string name)
        {
            var trimmed = line.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '[')
            {
                var end = trimmed.IndexOf(']');
                if (end > 0)
                {
                    name = trimmed.Substring(1, end - 1).Trim();
                    return true;
                }
            }
            name = null;
            return false;
        }

        static bool IsKeyValue(string line, out string key, out string value)
        {
            var trimmed = line.TrimStart();
            var equals = trimmed.IndexOf('=');
            if (trimmed.Length == 0 || trimmed[0] == ';' || equals <= 0)
            {
                key = value = null;
                return false;
            }
            key = trimmed.Substring(0, equals).Trim();
            value = trimmed.Substring(equals + 1).Trim();
            return true;
        }

        static string Unquote(string value)
        {
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[value.Length - 1] == value[0])
                return value.Substring(1, value.Length - 2);
            return value;
        }

        /// <summary>
        /// Finds the line range of a section: the header index and the index after its last line.
        /// </summary>
        static bool FindSection(List<string> lines, string section, out int header, out int end)
        {
            header = end = -1;
            for (var i = 0; i < lines.Count; i++)
            {
                if (!IsSectionHeader(lines[i], out var name))
                    continue;
                if (header >= 0)
                {
                    end = i;
                    return true;
                }
                if (string.Equals(name, section, StringComparison.OrdinalIgnoreCase))
                    header = i;
            }
            end = lines.Count;
            return header >= 0;
        }

        public static string[] GetSectionNames(string filePath)
        {
            lock (Lock)
            {
                var names = new List<string>();
                foreach (var line in ReadLines(filePath))
                    if (IsSectionHeader(line, out var name) && !names.Contains(name, StringComparer.OrdinalIgnoreCase))
                        names.Add(name);
                return names.ToArray();
            }
        }

        /// <summary>
        /// Returns the key names in a section, in file order.
        /// </summary>
        public static string[] GetKeyNames(string filePath, string section)
        {
            lock (Lock)
            {
                var lines = ReadLines(filePath);
                if (!FindSection(lines, section, out var header, out var end))
                    return new string[0];
                var keys = new List<string>();
                for (var i = header + 1; i < end; i++)
                    if (IsKeyValue(lines[i], out var key, out _))
                        keys.Add(key);
                return keys.ToArray();
            }
        }

        /// <summary>
        /// Returns the value of a key, or <c>null</c> when the section or key does not exist.
        /// </summary>
        public static string GetString(string filePath, string section, string key)
        {
            lock (Lock)
            {
                var lines = ReadLines(filePath);
                if (!FindSection(lines, section, out var header, out var end))
                    return null;
                for (var i = header + 1; i < end; i++)
                    if (IsKeyValue(lines[i], out var name, out var value) && string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                        return Unquote(value);
                return null;
            }
        }

        /// <summary>
        /// Writes a key; a <c>null</c> <paramref name="value"/> deletes it.
        /// </summary>
        public static void WriteString(string filePath, string section, string key, string value)
        {
            lock (Lock)
            {
                var lines = ReadLines(filePath);
                if (!FindSection(lines, section, out var header, out var end))
                {
                    if (value == null)
                        return;
                    lines.Add("[" + section + "]");
                    lines.Add(key + "=" + value);
                }
                else
                {
                    var existing = -1;
                    for (var i = header + 1; i < end; i++)
                    {
                        if (IsKeyValue(lines[i], out var name, out _) && string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                        {
                            existing = i;
                            break;
                        }
                    }
                    if (value == null)
                    {
                        if (existing < 0)
                            return;
                        lines.RemoveAt(existing);
                    }
                    else if (existing >= 0)
                    {
                        lines[existing] = key + "=" + value;
                    }
                    else
                    {
                        // Insert after the last non-blank line of the section.
                        var insert = end;
                        while (insert > header + 1 && string.IsNullOrWhiteSpace(lines[insert - 1]))
                            insert--;
                        lines.Insert(insert, key + "=" + value);
                    }
                }
                File.WriteAllLines(filePath, lines, new UTF8Encoding(false));
            }
        }
    }
}
