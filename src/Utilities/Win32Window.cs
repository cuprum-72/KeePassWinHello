using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace KeePassWinHello
{
    class Win32Window : IWin32Window
    {
        public IntPtr Handle { get; private set; }

        private Win32Window(IntPtr handle)
        {
            Handle = handle;
        }

        #region Creation

        /// <summary>
        /// Returns null if not found.
        /// </summary>
        public static Win32Window From(IntPtr handle)
        {
            return GetOrNull(new HWND(handle));
        }

        /// <summary>
        /// Throws an exception if not found.
        /// </summary>
        public static Win32Window Get(string @class, string name)
        {
            return Get(IntPtr.Zero, IntPtr.Zero, @class, name);
        }

        public static Win32Window Get(IntPtr parentHandle, IntPtr childAfter, string @class, string name)
        {
            var hwnd = WinAPI.FindWindowEx(parentHandle, childAfter, @class, name);
            return GetOrError(hwnd, "FindWindowEx");
        }

        public static Win32Window Get(string @class, string name, int timeoutMs)
        {
            var hwnd = FindWithRetry(IntPtr.Zero, IntPtr.Zero, @class, name, timeoutMs);
            return GetOrError(hwnd, "FindWindowEx");
        }

        /// <summary>
        /// Returns null if not found.
        /// </summary>
        public static Win32Window Find(string @class, string name)
        {
            return Find(IntPtr.Zero, IntPtr.Zero, @class, name);
        }

        public static Win32Window Find(IntPtr parentHandle, IntPtr childAfter, string @class, string name)
        {
            var hwnd = WinAPI.FindWindowEx(parentHandle, childAfter, @class, name);
            return GetOrNull(hwnd);
        }

        public static Win32Window Find(string @class, string name, int timeoutMs)
        {
            var hwnd = FindWithRetry(IntPtr.Zero, IntPtr.Zero, @class, name, timeoutMs);
            return GetOrNull(hwnd);
        }

        private static HWND FindWithRetry(IntPtr parentHandle, IntPtr childAfter,
            string targetWindowClass, string targetWindowTitle, int timeoutMs)
        {
            if (timeoutMs < 1)
                throw new ArgumentOutOfRangeException("timeoutMs");

            const int waitTimeMs = 25;
            var attemptsCount = Math.Max(1, timeoutMs / waitTimeMs);

            HWND targetWindowHandle = new HWND();
            for (int i = 0; i < attemptsCount && targetWindowHandle.Value == IntPtr.Zero; i++)
            {
                targetWindowHandle = WinAPI.FindWindowEx(parentHandle, childAfter,
                    targetWindowClass, targetWindowTitle);
                if (targetWindowHandle.Value == IntPtr.Zero)
                    Thread.Sleep(waitTimeMs);
            }

            return targetWindowHandle;
        }

        private static Win32Window GetOrError(HWND hwnd, string funcName)
        {
            hwnd.ThrowOnError(funcName);
            return new Win32Window(hwnd.Value);
        }

        public static Win32Window GetOrNull(HWND hwnd)
        {
            return hwnd.IsValid ? new Win32Window(hwnd.Value) : null;
        }

        #endregion Creation

        public void Close()
        {
            const int WM_CLOSE = 0x0010;
            WinAPI.SendMessage(Handle, WM_CLOSE, 0, 0);
        }

        private static class WinAPI
        {
            private const string User32 = "User32.dll";

            [DllImport(User32, SetLastError = true)]
            public static extern int SendMessage(IntPtr hWnd, Int32 msg, Int32 wParam, Int32 lParam);

            [DllImport(User32, SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern HWND FindWindowEx(IntPtr parentHandle, IntPtr childAfter,
                string lpClassName, string lpWindowName);
        }
    }
}
