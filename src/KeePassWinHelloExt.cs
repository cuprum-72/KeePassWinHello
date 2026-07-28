using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using KeePass.Forms;
using KeePass.Plugins;
using KeePass.UI;
using KeePassWinHello.Utilities;

namespace KeePassWinHello
{
    public class KeePassWinHelloExt : Plugin
    {
        private IPluginHost _host;
        private KeyManagerProvider _keyManagerProvider;
        private UIContextManager _uiContextManager;
        private IDisposable _uiContext;
        private readonly object _unlockMutex = new Object();

        public override Image SmallIcon
        {
            get
            {
                try
                {
                    var GetSmallIconSize = typeof(UIUtil).GetMethod("GetSmallIconSize", BindingFlags.Public | BindingFlags.Static);
                    var size = GetSmallIconSize != null ? (Size)GetSmallIconSize.Invoke(null, null) : new Size(16, 16);

                    return size.Height > 16
                        ? Properties.Resources.KPWH_32x32
                        : Properties.Resources.KPWH_16x16;
                }
                catch (Exception)
                {
                    return Properties.Resources.KPWH_16x16;
                }
            }
        }

        public override string UpdateUrl
        {
            get
            {
                return "https://raw.githubusercontent.com/<OWNER>/<REPOSITORY>/<BRANCH>/keepass.version";
            }
        }

        public override bool Initialize(IPluginHost host)
        {
            if (_host != null) { Debug.Assert(false); Terminate(); }
            if (host == null) { return false; }

            var mainDesktop = WinAPI.GetThreadDesktop(WinAPI.GetCurrentThreadId());
            _uiContextManager = new UIContextManager(mainDesktop, host.MainWindow);
            _uiContext = _uiContextManager.PushContext("KeePass: Main Window", host.MainWindow);

            Settings.Instance.Initialize(host.CustomConfig, _uiContextManager);

            _keyManagerProvider = new KeyManagerProvider(_uiContextManager);

            _host = host;
            _host.MainWindow.FileClosingPre += OnPreFileClosing;
            GlobalWindowManager.WindowAdded += OnWindowAdded;

            return true;
        }

        public override void Terminate()
        {
            if (_host == null)
                return;

            GlobalWindowManager.WindowAdded -= OnWindowAdded;
            _host.MainWindow.FileClosingPre -= OnPreFileClosing;

            _keyManagerProvider.Dispose();
            _keyManagerProvider = null;

            _uiContext.Dispose();
            _uiContext = null;
            _uiContextManager = null;

            _host = null;
        }

        private void OnPreFileClosing(object sender, FileClosingEventArgs e)
        {
            try
            {
                var keyManager = _keyManagerProvider.ObtainKeyManager();
                if (keyManager != null)
                    keyManager.OnDBClosing(sender, e);
            }
            catch (Exception ex)
            {
                _uiContextManager.CurrentContext.ShowError(ex);
            }
        }

        private void OnWindowAdded(object sender, GwmWindowEventArgs e)
        {
            try
            {
                var keyPromptForm = e.Form as KeyPromptForm;
                if (keyPromptForm != null)
                {
                    // On the secure desktop, preserve the existing behavior: the
                    // prompt must be restarted on the main desktop before Hello.
                    if (keyPromptForm.SecureDesktopMode)
                    {
                        HandleKeyPrompt(keyPromptForm);
                    }
                    else
                    {
                        HandleKeyPromptAfterShown(keyPromptForm);
                    }
                    return;
                }

                var optionsForm = e.Form as OptionsForm;
                if (optionsForm != null)
                {
                    var keyManager = _keyManagerProvider.ObtainKeyManager();
                    using (_uiContextManager.PushContext("Modifying KeePass settings", optionsForm))
                    {
                        OptionsPanel.OnOptionsLoad(optionsForm, keyManager, _uiContextManager);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _uiContextManager.CurrentContext.ShowError(ex);
            }
        }
        private void HandleKeyPromptAfterShown(KeyPromptForm keyPromptForm)
        {
            EventHandler shownHandler = null;
            shownHandler = delegate
            {
                keyPromptForm.Shown -= shownHandler;

                if (keyPromptForm.IsDisposed || !keyPromptForm.IsHandleCreated)
                    return;

                try
                {
                    // Run after the Shown event has completed, so the WinHello
                    // parent HWND is visible and has had a chance to become active.
                    keyPromptForm.BeginInvoke((MethodInvoker)delegate
                    {
                        HandleKeyPrompt(keyPromptForm);
                    });
                }
                catch (InvalidOperationException)
                {
                    // The form was closed while the callback was being queued.
                }
            };

            keyPromptForm.Shown += shownHandler;
        }

        private void HandleKeyPrompt(KeyPromptForm keyPromptForm)
        {
            if (keyPromptForm == null || keyPromptForm.IsDisposed ||
                _keyManagerProvider == null || _uiContextManager == null)
                return;

            try
            {
                var keyManager = _keyManagerProvider.ObtainKeyManager();
                if (keyManager == null)
                    return;

                using (_uiContextManager.PushContext("Unlocking a database", keyPromptForm))
                {
                    if (!keyPromptForm.SecureDesktopMode && keyPromptForm.Visible)
                    {
                        keyPromptForm.BringToFront();
                        keyPromptForm.Activate();
                    }

                    lock (_unlockMutex)
                        keyManager.OnKeyPrompt(keyPromptForm);
                }
            }
            catch (Exception ex)
            {
                var context = _uiContextManager != null ? _uiContextManager.CurrentContext : null;
                if (context != null)
                    context.ShowError(ex);
                else
                    Debug.Fail(ex.Message);
            }
        }

    }
}