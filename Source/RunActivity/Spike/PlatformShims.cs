// SPIKE(linux): throwaway stand-ins for Windows desktop types used by RunActivity.
// These are deliberately placed in the original namespaces so that the spike needs
// few call-site edits. They are NOT a design; see reviews/linux-build/spike.md.

using System;
using System.Diagnostics;
using Microsoft.Xna.Framework.Input;

namespace System.Windows.Forms
{
    public enum MessageBoxButtons { OK, OKCancel, AbortRetryIgnore, YesNoCancel, YesNo, RetryCancel }
    public enum MessageBoxIcon { None, Error, Question, Warning, Information }
    public enum DialogResult { None, OK, Cancel, Abort, Retry, Ignore, Yes, No }

    public static class MessageBox
    {
        public static DialogResult Show(string text) => Show(text, "", MessageBoxButtons.OK, MessageBoxIcon.None);
        public static DialogResult Show(string text, string caption) => Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.None);
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons) => Show(text, caption, buttons, MessageBoxIcon.None);
        public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            Console.Error.WriteLine("[{0}] {1}: {2}", icon, caption, text);
            Trace.WriteLine(string.Format("MessageBox [{0}] {1}: {2}", icon, caption, text));
            var kind = icon == MessageBoxIcon.Error ? Orts.Viewer3D.Common.NativeMessageBox.Kind.Error
                : icon == MessageBoxIcon.Warning ? Orts.Viewer3D.Common.NativeMessageBox.Kind.Warning
                : Orts.Viewer3D.Common.NativeMessageBox.Kind.Information;
            try
            {
                if (buttons == MessageBoxButtons.OKCancel)
                    return Orts.Viewer3D.Common.NativeMessageBox.Show(kind, caption, text, "OK", "Cancel") == 0 ? DialogResult.OK : DialogResult.Cancel;
                Orts.Viewer3D.Common.NativeMessageBox.Show(kind, caption, text, "OK");
                return DialogResult.OK;
            }
            catch (Exception error) when (error is DllNotFoundException || error is EntryPointNotFoundException || error is InvalidOperationException)
            {
                // No dialog possible (for example no display): the console and log already have the message.
                return buttons == MessageBoxButtons.OK ? DialogResult.OK : DialogResult.Cancel;
            }
        }
    }

    public static class Application
    {
        public static string ProductName => ORTS.Common.ApplicationInfo.ProductName;
    }

    public static class SystemInformation
    {
        public static bool MouseButtonsSwapped => false;
        public static int HorizontalScrollBarHeight => 17;
    }

    public sealed class Cursor
    {
        public readonly MouseCursor MouseCursor;
        internal Cursor(MouseCursor mouseCursor) { MouseCursor = mouseCursor; }
    }

    public static class Cursors
    {
        public static readonly Cursor Default = new Cursor(MouseCursor.Arrow);
        public static readonly Cursor Hand = new Cursor(MouseCursor.Hand);
    }
}

namespace System.Drawing
{
    [Flags]
    public enum FontStyle { Regular = 0, Bold = 1, Italic = 2, Underline = 4, Strikeout = 8 }
}

namespace System.Media
{
    public sealed class SoundPlayer
    {
        public SoundPlayer(string soundLocation) { }
        public void LoadAsync() { }
        public void Play() { }
    }
}

namespace Orts.Viewer3D.Debugging
{
    // Compile-only stand-ins for the excluded WinForms map and sound debug windows.
    // The spike never creates them, so Program.MapForm/SoundDebugForm stay null.
    public sealed class SwitchWidget { public Orts.Formats.Msts.TrackNode Item; }
    public sealed class SignalWidget { public Orts.Formats.Msts.TrItem Item; }

    public sealed class MapViewer : IDisposable
    {
        public bool Enabled;
        public SwitchWidget switchPickedItem;
        public SignalWidget signalPickedItem;
        public Orts.Simulation.Physics.Train PickedTrain;
        public bool ClickedTrain;
        public void Hide() { }
        public void Dispose() { }
    }

    public sealed class SoundDebugForm : IDisposable
    {
        public void Hide() { }
        public void Dispose() { }
    }
}

namespace Orts.Viewer3D
{
    // SPIKE(linux): single seam for the future window/backend DPI service (review PR 13).
    static class Display
    {
        public static float DpiY => 96;
    }
}
