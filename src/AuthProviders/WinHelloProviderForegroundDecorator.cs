using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace KeePassWinHello
{
    internal sealed class WinHelloProviderForegroundDecorator : IAuthProvider
    {
        private readonly IAuthProvider _winHelloProvider;
        private readonly UIContextManager _uiContextManager;

        public WinHelloProviderForegroundDecorator(
            IAuthProvider provider,
            UIContextManager uiContextManager)
        {
            if (provider == null)
                throw new ArgumentNullException("provider");

            if (uiContextManager == null)
                throw new ArgumentNullException("uiContextManager");

            _winHelloProvider = provider;
            _uiContextManager = uiContextManager;
        }

        public AuthCacheType CurrentCacheType
        {
            get
            {
                return _winHelloProvider.CurrentCacheType;
            }
        }

        public void ClaimCurrentCacheType(AuthCacheType newType)
        {
            _winHelloProvider.ClaimCurrentCacheType(newType);
        }

        public byte[] Encrypt(byte[] data)
        {
            return _winHelloProvider.Encrypt(data);
        }

        public byte[] PromptToDecrypt(byte[] data)
        {
            ActivateCurrentParentWindowSafe();

            byte[] result =
                _winHelloProvider.PromptToDecrypt(data);

            QueueKeePassMainWindowActivationSafe();

            return result;
        }

        private void ActivateCurrentParentWindowSafe()
        {
            try
            {
                UIContext context =
                    _uiContextManager.CurrentContext;

                Form parentForm =
                    context != null
                        ? context.ParentWindow as Form
                        : null;

                if (parentForm == null ||
                    parentForm.IsDisposed ||
                    !parentForm.IsHandleCreated ||
                    !parentForm.Visible)
                {
                    return;
                }

                parentForm.BringToFront();
                parentForm.Activate();
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.Message);
            }
        }

        private void QueueKeePassMainWindowActivationSafe()
        {
            try
            {
                Form mainWindow =
                    _uiContextManager.MainWindow;

                if (mainWindow == null ||
                    mainWindow.IsDisposed ||
                    !mainWindow.IsHandleCreated)
                {
                    return;
                }

                mainWindow.BeginInvoke(
                    (MethodInvoker)delegate
                    {
                        try
                        {
                            if (mainWindow.IsDisposed ||
                                !mainWindow.IsHandleCreated ||
                                !mainWindow.Visible ||
                                mainWindow.WindowState ==
                                    FormWindowState.Minimized)
                            {
                                return;
                            }

                            mainWindow.BringToFront();
                            mainWindow.Activate();
                        }
                        catch (Exception ex)
                        {
                            Debug.Fail(ex.Message);
                        }
                    });
            }
            catch (InvalidOperationException ex)
            {
                Debug.Fail(ex.Message);
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.Message);
            }
        }
    }
}