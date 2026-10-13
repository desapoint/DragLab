#if UNITY_STANDALONE_WIN && DEVELOPMENT_BUILD && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DragLab.Runtime
{
    // Opt-in development verification only. Never send key-downs to another application's window.
    internal sealed class WindowsKeyboardProbe : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] struct Keyboard { public ushort key, scan; public uint flags, time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] struct Mouse { public int x, y; public uint data, flags, time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Explicit)] struct Payload { [FieldOffset(0)] public Keyboard keyboard; [FieldOffset(0)] public Mouse mouse; }
        [StructLayout(LayoutKind.Sequential)] struct InputEvent { public uint type; public Payload payload; }
        [DllImport("user32.dll")] static extern uint SendInput(uint count, InputEvent[] inputs, int size);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
        [StructLayout(LayoutKind.Sequential)] struct Point { public int x, y; }
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window, ref Point point);
        [DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
        delegate bool WindowCallback(IntPtr window, IntPtr data);
        [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback, IntPtr data);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] static extern bool AttachThreadInput(uint from, uint to, bool attach);
        [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr window);
        [DllImport("user32.dll")] static extern IntPtr SetFocus(IntPtr window);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        readonly HashSet<ushort> held = new HashSet<ushort>();
        readonly HashSet<int> mouseHeld = new HashSet<int>();
        readonly uint processId;
        public WindowsKeyboardProbe()
        {
            var process = System.Diagnostics.Process.GetCurrentProcess(); processId = (uint)process.Id;
            IntPtr window = IntPtr.Zero;
            EnumWindows((candidate, data) => {
                GetWindowThreadProcessId(candidate, out uint owner);
                if (owner != processId || !IsWindowVisible(candidate)) return true;
                window = candidate; return false;
            }, IntPtr.Zero);
            if (window == IntPtr.Zero) throw new InvalidOperationException("Keyboard probe could not find its visible player window.");
            uint currentThread = GetCurrentThreadId();
            uint foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out uint unused);
            bool attached = currentThread != foregroundThread && AttachThreadInput(currentThread, foregroundThread, true);
            try { BringWindowToTop(window); SetForegroundWindow(window); SetFocus(window); }
            finally { if (attached) AttachThreadInput(currentThread, foregroundThread, false); }
        }
        public void Key(ushort key, bool down)
        {
            if (down)
            {
                OwnForeground();
            }
            var input = new InputEvent { type = 1, payload = new Payload { keyboard = new Keyboard { key = key, flags = down ? 0u : 2u } } };
            if (SendInput(1, new[] { input }, Marshal.SizeOf(typeof(InputEvent))) != 1)
                throw new InvalidOperationException("Windows keyboard event was not accepted.");
            if (down) held.Add(key); else held.Remove(key);
        }
        public void Dispose()
        {
            foreach (ushort key in new List<ushort>(held)) Key(key, false);
            foreach (int button in new List<int>(mouseHeld))
            {
                var release = new InputEvent { type = 0, payload = new Payload { mouse = new Mouse { flags = MouseFlag(button, false) } } };
                SendInput(1, new[] { release }, Marshal.SizeOf(typeof(InputEvent))); mouseHeld.Remove(button);
            }
        }
        static uint MouseFlag(int button, bool down) { return button == 0 ? (down ? 2u : 4u) : button == 1 ? (down ? 8u : 16u) : (down ? 32u : 64u); }
        public void MouseButtonAt(int x, int y, bool down, int button = 0)
        {
            if (button < 0 || button > 2) throw new ArgumentOutOfRangeException("button");
            MoveMouseAt(x, y);
            var input = new InputEvent { type = 0, payload = new Payload { mouse = new Mouse { flags = MouseFlag(button, down) } } };
            if (SendInput(1, new[] { input }, Marshal.SizeOf(typeof(InputEvent))) != 1) throw new InvalidOperationException("Mouse event was not accepted.");
            if (down) mouseHeld.Add(button); else mouseHeld.Remove(button);
        }
        public void MoveMouseAt(int x, int y)
        {
            IntPtr window = OwnForeground();
            var point = new Point { x = x, y = y };
            if (!ClientToScreen(window, ref point) || !SetCursorPos(point.x, point.y)) throw new InvalidOperationException("Mouse probe could not locate its own client area.");
        }
        IntPtr OwnForeground()
        {
            IntPtr current=GetForegroundWindow();GetWindowThreadProcessId(current,out uint owner);if(owner==processId)return current;
            IntPtr own=IntPtr.Zero;EnumWindows((candidate,data)=>{GetWindowThreadProcessId(candidate,out uint pid);if(pid!=processId||!IsWindowVisible(candidate))return true;own=candidate;return false;},IntPtr.Zero);
            if(own==IntPtr.Zero)throw new InvalidOperationException("Input probe has no visible player window.");
            uint thread=GetCurrentThreadId(),foregroundThread=GetWindowThreadProcessId(current,out uint unused);bool attached=foregroundThread!=0&&thread!=foregroundThread&&AttachThreadInput(thread,foregroundThread,true);
            try{BringWindowToTop(own);SetForegroundWindow(own);SetFocus(own);}finally{if(attached)AttachThreadInput(thread,foregroundThread,false);}
            current=GetForegroundWindow();GetWindowThreadProcessId(current,out owner);if(owner!=processId)throw new InvalidOperationException("Input probe could not focus its own player window; no input sent.");return current;
        }
        public void ScrollAt(int x,int y,int notches)
        {
            MoveMouseAt(x,y);
            var input=new InputEvent{type=0,payload=new Payload{mouse=new Mouse{flags=0x800,data=unchecked((uint)(notches*120))}}};
            if(SendInput(1,new[]{input},Marshal.SizeOf(typeof(InputEvent)))!=1)throw new InvalidOperationException("Mouse wheel event was not accepted.");
        }
    }
}
#endif
