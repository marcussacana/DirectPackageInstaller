using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace DirectPackageInstaller
{
    public class ElevatedDragDropManager : IMessageFilter
    {
        private const uint WM_DROPFILES = 0x233;
        private const uint WM_COPYDATA = 0x4a;

        private const uint WM_COPYGLOBALDATA = 0x49;

        public static ElevatedDragDropManager Instance = new ElevatedDragDropManager();

        private readonly bool Is7OrHigher =
            Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor >= 1
            || Environment.OSVersion.Version.Major > 6;

        private readonly bool IsVistaOrHigher = Environment.OSVersion.Version.Major >= 6;

        protected ElevatedDragDropManager()
        {
            if (Program.IsUnix)
                return;

            Application.AddMessageFilter(this);
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == WM_DROPFILES)
            {
                this.HandleDragDropMessage(m);
                return true;
            }

            return false;
        }

        public event EventHandler<ElevatedDragDropArgs> ElevatedDragDrop;

        public void EnableDragDrop(IntPtr hWnd)
        {
            if (Program.IsUnix)
                return;

            if (Is7OrHigher)
            {
                var changeStruct = new CHANGEFILTERSTRUCT();
                changeStruct.cbSize = Convert.ToUInt32(Marshal.SizeOf(typeof(CHANGEFILTERSTRUCT)));
                ChangeWindowMessageFilterEx(
                    hWnd,
                    WM_DROPFILES,
                    ChangeWindowMessageFilterExAction.Allow,
                    ref changeStruct);
                ChangeWindowMessageFilterEx(
                    hWnd,
                    WM_COPYDATA,
                    ChangeWindowMessageFilterExAction.Allow,
                    ref changeStruct);
                ChangeWindowMessageFilterEx(
                    hWnd,
                    WM_COPYGLOBALDATA,
                    ChangeWindowMessageFilterExAction.Allow,
                    ref changeStruct);
            }
            else if (IsVistaOrHigher)
            {
                ChangeWindowMessageFilter(WM_DROPFILES, ChangeWindowMessageFilterFlags.Add);
                ChangeWindowMessageFilter(WM_COPYDATA, ChangeWindowMessageFilterFlags.Add);
                ChangeWindowMessageFilter(WM_COPYGLOBALDATA, ChangeWindowMessageFilterFlags.Add);
            }

            DragAcceptFiles(hWnd, true);
        }

        private void HandleDragDropMessage(Message m)
        {
            var sb = new StringBuilder(260);
            var numFiles = DragQueryFile(m.WParam, 0xffffffffu, sb, 0);
            var list = new List<string>();

            for (uint i = 0; i <= numFiles - 1; i++)
            {
                if (DragQueryFile(m.WParam, i, sb, Convert.ToUInt32(sb.Capacity) * 2) > 0)
                {
                    list.Add(sb.ToString());
                }
            }

            POINT p = default;
            DragQueryPoint(m.WParam, ref p);
            DragFinish(m.WParam);

            var args = new ElevatedDragDropArgs();
            args.HWnd = m.HWnd;
            args.Files = list;
            args.X = p.X;
            args.Y = p.Y;

            if (this.ElevatedDragDrop != null)
            {
                this.ElevatedDragDrop(this, args);
            }
        }

        #region "P/Invoke"

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeWindowMessageFilterEx(
            IntPtr hWnd, uint msg, ChangeWindowMessageFilterExAction action,
            ref CHANGEFILTERSTRUCT changeInfo);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ChangeWindowMessageFilter(
            uint msg, ChangeWindowMessageFilterFlags flags);

        [DllImport("shell32.dll")]
        private static extern void DragAcceptFiles(IntPtr hwnd, bool fAccept);

        [DllImport("shell32.dll")]
        private static extern uint DragQueryFile(
            IntPtr hDrop, uint iFile, [Out] StringBuilder lpszFile, uint cch);

        [DllImport("shell32.dll")]
        private static extern bool DragQueryPoint(IntPtr hDrop, ref POINT lppt);

        [DllImport("shell32.dll")]
        private static extern void DragFinish(IntPtr hDrop);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public readonly int X;

            public readonly int Y;

            public POINT(int newX, int newY)
            {
                X = newX;
                Y = newY;
            }

            public static implicit operator Point(POINT p)
            {
                return new Point(p.X, p.Y);
            }

            public static implicit operator POINT(Point p)
            {
                return new POINT(p.X, p.Y);
            }
        }

        private enum MessageFilterInfo : uint
        {
            None,
            AlreadyAllowed,
            AlreadyDisAllowed,
            AllowedHigher
        }

        private enum ChangeWindowMessageFilterExAction : uint
        {
            Reset,
            Allow,
            Disallow
        }

        private enum ChangeWindowMessageFilterFlags : uint
        {
            Add = 1,
            Remove = 2
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CHANGEFILTERSTRUCT
        {
            public uint cbSize;
            public readonly MessageFilterInfo ExtStatus;
        }

        #endregion
    }

    public class ElevatedDragDropArgs : EventArgs
    {
        public ElevatedDragDropArgs()
        {
            this.Files = new List<string>();
        }

        public IntPtr HWnd { get; set; }

        public List<string> Files { get; set; }

        public int X { get; set; }

        public int Y { get; set; }
    }
}
