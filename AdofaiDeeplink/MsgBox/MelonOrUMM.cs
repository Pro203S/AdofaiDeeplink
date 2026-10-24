using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AdofaiDeeplink.MsgBox
{
    public class MelonOrUmm
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

        /// <summary>
        /// Yes = Melon, No = UMM
        /// </summary>
        /// <returns></returns>
        public static DialogResult Show()
        {
            try
            {
                _hookProc = HookCallback;
                _hookHandle = SetWindowsHookEx(WH_CBT, _hookProc, IntPtr.Zero, GetCurrentThreadId());

                return MessageBox.Show(
                        "A Dance of Fire and Ice에서 사용하는 모드 로더를 선택해주세요." +
                        "\n다르게 선택하면 모드 적용이 정상적으로 되지 않을 수 있습니다." +
                        "\n[Melon]은 MelonLoader, [UMM]은 UnityModManager입니다." +
                        "\n" +
                        "\nPlease select the mod loader used by A Dance of Fire and Ice." +
                        "\nSelecting the wrong mod loader may prevent the mod from working properly." +
                        "\n[Melon] refers to MelonLoader, and [UMM] refers to UnityModManager.",
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
                    SetWindowText(hYes, "Melon");

                if (hNo != IntPtr.Zero)
                    SetWindowText(hNo, "UMM");
            }

            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }
    }
}
