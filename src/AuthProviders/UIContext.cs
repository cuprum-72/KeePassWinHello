using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace KeePassWinHello
{
    internal sealed class UIContext : IWin32Window
    {
        public string Message { get; private set; }
        public IWin32Window ParentWindow { get; private set; }
        public HWND ParentWindowHandle { get; private set; }

        IntPtr IWin32Window.Handle
        {
            get { return ParentWindowHandle.Value; }
        }

        public UIContext(string message, IWin32Window parentWindow)
        {
            if (parentWindow == null)
                throw new ArgumentNullException("parentWindow");

            Message = message;
            ParentWindow = parentWindow;
            ParentWindowHandle = new HWND(parentWindow.Handle);
        }
    }

    internal sealed class UIContextManager
    {
        private readonly LinkedList<UIContext> _contexts =
            new LinkedList<UIContext>();

        private readonly object _lock = new object();
        private readonly HDESK _mainDesktop;
        private readonly Form _mainWindow;

        public UIContextManager(HDESK mainDesktop, Form mainWindow)
        {
            if (mainWindow == null)
                throw new ArgumentNullException("mainWindow");

            _mainDesktop = mainDesktop;
            _mainWindow = mainWindow;
        }

        public UIContext CurrentContext
        {
            get
            {
                lock (_lock)
                {
                    LinkedListNode<UIContext> node = _contexts.First;
                    return node != null ? node.Value : null;
                }
            }
        }

        public HDESK MainDesktop
        {
            get { return _mainDesktop; }
        }

        public Form MainWindow
        {
            get { return _mainWindow; }
        }

        public IDisposable PushContext(
            string message,
            IWin32Window parentWindow)
        {
            UIContext context =
                new UIContext(message, parentWindow);

            lock (_lock)
            {
                _contexts.AddFirst(context);
            }

            return new Disposer(this, context);
        }

        private bool RemoveContext(UIContext context)
        {
            lock (_lock)
            {
                return _contexts.Remove(context);
            }
        }

        private sealed class Disposer : IDisposable
        {
            private readonly UIContextManager _contextManager;
            private readonly UIContext _context;
            private int _disposed;

            public Disposer(
                UIContextManager contextManager,
                UIContext context)
            {
                if (contextManager == null)
                    throw new ArgumentNullException("contextManager");

                if (context == null)
                    throw new ArgumentNullException("context");

                _contextManager = contextManager;
                _context = context;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                    return;

                bool removed =
                    _contextManager.RemoveContext(_context);

                Debug.Assert(removed);
            }
        }
    }
}