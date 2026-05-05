using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AdofaiDeeplink
{
    public class CustomMsgBox
    {
        private const int WH_CBT = 5;
        private const int HCBT_ACTIVATE = 5;

        private const int IDYES = 6;
        private const int IDNO = 7;

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static HookProc _hookProc;
        private static IntPtr _hookHandle;

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDlgItem(IntPtr hDlg, int nIDDlgItem);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool SetWindowText(IntPtr hWnd, string lpString);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        public static DialogResult Show()
        {
            try
            {
                _hookProc = HookCallback;
                _hookHandle = SetWindowsHookEx(WH_CBT, _hookProc, IntPtr.Zero, GetCurrentThreadId());

                return MessageBox.Show(
                        "이미 AdofaiDeeplink가 설치되어있습니다." +
                        "\n작업을 선택해주세요." +
                        "\n" +
                        "\nAdofaiDeeplink is already installed." +
                        "\nPlease choose an action.",
                        "AdofaiDeeplink",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);
            }
            finally
            {
                UnhookWindowsHookEx(_hookHandle);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == HCBT_ACTIVATE)
            {
                IntPtr hWnd = wParam;

                IntPtr hYes = GetDlgItem(hWnd, IDYES);
                IntPtr hNo = GetDlgItem(hWnd, IDNO);

                if (hYes != IntPtr.Zero)
                    SetWindowText(hYes, "Reinstall");

                if (hNo != IntPtr.Zero)
                    SetWindowText(hNo, "Remove");
            }

            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }
    }
}
