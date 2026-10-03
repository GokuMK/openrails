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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Orts.Viewer3D.Common
{
    /// <summary>
    /// Shows a native modal message box: through SDL2 (which MonoGame DesktopGL ships), and on Linux desktops where
    /// SDL cannot (its message boxes need zenity) through kdialog or xmessage.
    /// </summary>
    /// <remarks>
    /// Every method opens its own dialog window, so this may be called from any thread and before or after the game
    /// window exists. The calling thread blocks until the user closes the dialog.
    /// </remarks>
    public static class NativeMessageBox
    {
        public enum Kind : uint
        {
            Error = 0x10,
            Warning = 0x20,
            Information = 0x40,
        }

        const uint ReturnKeyDefault = 0x1;
        const uint EscapeKeyDefault = 0x2;

        [StructLayout(LayoutKind.Sequential)]
        struct ButtonData
        {
            public uint Flags;
            public int ButtonId;
            public IntPtr Text;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MessageBoxData
        {
            public uint Flags;
            public IntPtr Window;
            public IntPtr Title;
            public IntPtr Message;
            public int NumButtons;
            public IntPtr Buttons;
            public IntPtr ColorScheme;
        }

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        static extern int SDL_ShowMessageBox(ref MessageBoxData messageBoxData, out int buttonId);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr SDL_GetError();

        /// <summary>
        /// Shows the message box and returns the index of the chosen button, or -1 when the dialog was closed
        /// without a choice.
        /// </summary>
        /// <exception cref="InvalidOperationException">No dialog could be shown (for example no display).</exception>
        public static int Show(Kind kind, string title, string message, params string[] buttons)
        {
            try
            {
                return ShowSdl(kind, title, message, buttons);
            }
            catch (Exception error) when (OperatingSystem.IsLinux() && (error is InvalidOperationException || error is DllNotFoundException || error is EntryPointNotFoundException))
            {
                return ShowWithTool(kind, title, message, buttons);
            }
        }

        static int ShowSdl(Kind kind, string title, string message, string[] buttons)
        {
            var strings = new IntPtr[2 + buttons.Length];
            var buttonData = new ButtonData[buttons.Length];
            var buttonHandle = default(GCHandle);
            try
            {
                strings[0] = Marshal.StringToCoTaskMemUTF8(title ?? "");
                strings[1] = Marshal.StringToCoTaskMemUTF8(message ?? "");
                for (var i = 0; i < buttons.Length; i++)
                {
                    strings[2 + i] = Marshal.StringToCoTaskMemUTF8(buttons[i]);
                    buttonData[i] = new ButtonData
                    {
                        // SDL lists buttons in reverse order on some platforms; ids keep the meaning stable.
                        Flags = (i == 0 ? ReturnKeyDefault : 0) | (i == buttons.Length - 1 ? EscapeKeyDefault : 0),
                        ButtonId = i,
                        Text = strings[2 + i],
                    };
                }
                buttonHandle = GCHandle.Alloc(buttonData, GCHandleType.Pinned);
                var data = new MessageBoxData
                {
                    Flags = (uint)kind,
                    Title = strings[0],
                    Message = strings[1],
                    NumButtons = buttons.Length,
                    Buttons = buttonHandle.AddrOfPinnedObject(),
                };
                if (SDL_ShowMessageBox(ref data, out var buttonId) != 0)
                    throw new InvalidOperationException("SDL could not show the message box: " + Marshal.PtrToStringUTF8(SDL_GetError()));
                return buttonId;
            }
            finally
            {
                if (buttonHandle.IsAllocated)
                    buttonHandle.Free();
                foreach (var pointer in strings)
                    if (pointer != IntPtr.Zero)
                        Marshal.FreeCoTaskMem(pointer);
            }
        }
    
        // Linux desktop tools, in order of preference. Button 0 is the default (OK), the last is Cancel.
        static int ShowWithTool(Kind kind, string title, string message, string[] buttons)
        {
            var cancellable = buttons.Length > 1;
            if (TryRun("kdialog", cancellable
                    ? new[] { "--title", title, "--warningcontinuecancel", message, "--continue-label", buttons[0], "--cancel-label", buttons.Last() }
                    : new[] { "--title", title, kind == Kind.Error ? "--error" : kind == Kind.Warning ? "--sorry" : "--msgbox", message },
                    out var exitCode))
                return exitCode == 0 ? 0 : buttons.Length - 1;
            if (TryRun("xmessage", new[] { "-center", "-title", title, "-buttons", string.Join(",", buttons.Select((text, index) => $"{text}:{index + 100}")), message }, out exitCode))
                return exitCode >= 100 && exitCode < 100 + buttons.Length ? exitCode - 100 : buttons.Length - 1;
            throw new InvalidOperationException("No message box tool (kdialog, xmessage) is available.");
        }

        static bool TryRun(string tool, string[] arguments, out int exitCode)
        {
            exitCode = -1;
            var path = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
                .Select(directory => Path.Combine(directory, tool)).FirstOrDefault(File.Exists);
            if (path == null)
                return false;
            var startInfo = new ProcessStartInfo(path) { UseShellExecute = false };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);
            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit();
                exitCode = process.ExitCode;
            }
            return true;
        }
    }
}
