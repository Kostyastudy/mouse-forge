using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace MouseForge
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            string errorLog = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MouseForge-error.log");

            try
            {
                File.Delete(errorLog);
            }
            catch
            {
            }

            try
            {
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
                {
                    WriteCrashLog(errorLog, e.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    WriteCrashLog(errorLog, e.ExceptionObject as Exception);
                };

                try
                {
                    Native.SetProcessDPIAware();
                }
                catch
                {
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                string initialProject = args.Length > 0 ? args[0] : null;
                Application.Run(new MainForm(initialProject));
            }
            catch (Exception ex)
            {
                WriteCrashLog(errorLog, ex);
            }
        }

        private static void WriteCrashLog(string path, Exception ex)
        {
            try
            {
                string text = ex == null ? "Unknown error" : ex.ToString();
                File.WriteAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + text, Encoding.UTF8);
            }
            catch
            {
            }

            try
            {
                MessageBox.Show(
                    ex == null ? "Unknown error" : ex.Message,
                    "MouseForge 启动失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            catch
            {
            }
        }
    }

    internal static class Native
    {
        public const int WH_MOUSE_LL = 14;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_RBUTTONDOWN = 0x0204;
        public const int WM_MBUTTONDOWN = 0x0207;
        public const int WM_HOTKEY = 0x0312;

        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP = 0x0004;
        public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        public const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        public const uint MOUSEEVENTF_MIDDLEUP = 0x0040;

        public const int VK_RMENU = 0xA5;
        public const int VK_ESCAPE = 0x1B;

        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hDC, int index);

        public static float GetSystemScale()
        {
            IntPtr dc = GetDC(IntPtr.Zero);

            try
            {
                int dpi = GetDeviceCaps(dc, 88);
                return dpi <= 0 ? 1.0F : dpi / 96.0F;
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, dc);
            }
        }

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int virtualKey);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc callback, IntPtr module, uint threadId);

        [DllImport("user32.dll")]
        public static extern bool UnhookWindowsHookEx(IntPtr hookId);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hookId, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string moduleName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public static bool IsKeyDown(int virtualKey)
        {
            return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
        }

        public static void Click(string button, int holdMs)
        {
            uint down;
            uint up;

            if (string.Equals(button, "Right", StringComparison.OrdinalIgnoreCase))
            {
                down = MOUSEEVENTF_RIGHTDOWN;
                up = MOUSEEVENTF_RIGHTUP;
            }
            else if (string.Equals(button, "Middle", StringComparison.OrdinalIgnoreCase))
            {
                down = MOUSEEVENTF_MIDDLEDOWN;
                up = MOUSEEVENTF_MIDDLEUP;
            }
            else
            {
                down = MOUSEEVENTF_LEFTDOWN;
                up = MOUSEEVENTF_LEFTUP;
            }

            mouse_event(down, 0, 0, 0, UIntPtr.Zero);

            if (holdMs > 0)
            {
                System.Threading.Thread.Sleep(holdMs);
            }

            mouse_event(up, 0, 0, 0, UIntPtr.Zero);
        }
    }

    internal sealed class MouseHook : IDisposable
    {
        private IntPtr hookId = IntPtr.Zero;
        private Native.LowLevelMouseProc callback;

        public event Action<int, int, int> MouseDown;

        public bool IsInstalled
        {
            get { return hookId != IntPtr.Zero; }
        }

        public bool Install()
        {
            if (hookId != IntPtr.Zero)
            {
                return true;
            }

            callback = HookCallback;
            hookId = Native.SetWindowsHookEx(
                Native.WH_MOUSE_LL,
                callback,
                Native.GetModuleHandle(null),
                0
            );

            return hookId != IntPtr.Zero;
        }

        public void Uninstall()
        {
            if (hookId != IntPtr.Zero)
            {
                Native.UnhookWindowsHookEx(hookId);
                hookId = IntPtr.Zero;
            }

            callback = null;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int message = wParam.ToInt32();
                int button = 0;

                if (message == Native.WM_LBUTTONDOWN)
                {
                    button = 1;
                }
                else if (message == Native.WM_RBUTTONDOWN)
                {
                    button = 2;
                }
                else if (message == Native.WM_MBUTTONDOWN)
                {
                    button = 3;
                }

                if (button != 0)
                {
                    Native.MSLLHOOKSTRUCT data = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(
                        lParam,
                        typeof(Native.MSLLHOOKSTRUCT)
                    );

                    Native.POINT point;

                    if (!Native.GetCursorPos(out point))
                    {
                        point = data.pt;
                    }

                    Action<int, int, int> handler = MouseDown;

                    if (handler != null)
                    {
                        try
                        {
                            handler(button, point.X, point.Y);
                        }
                        catch
                        {
                        }
                    }
                }
            }

            return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            Uninstall();
        }
    }

    internal sealed class Step
    {
        public string Type = "click";
        public int X;
        public int Y;
        public string Button = "Left";
        public int MoveMs;
        public int HoldMs = 40;
        public double Seconds = 1.0;

        public Step Clone()
        {
            return (Step)MemberwiseClone();
        }

        public string TypeName
        {
            get
            {
                if (Type == "move")
                {
                    return "移动";
                }

                if (Type == "wait")
                {
                    return "等待";
                }

                return "点击";
            }
        }

        public string ButtonName
        {
            get
            {
                if (Type != "click")
                {
                    return "—";
                }

                if (Button == "Right")
                {
                    return "右键";
                }

                if (Button == "Middle")
                {
                    return "中键";
                }

                return "左键";
            }
        }

        public string LocationText
        {
            get
            {
                if (Type == "wait")
                {
                    return "—";
                }

                return "(" + X.ToString(CultureInfo.InvariantCulture) + ", " +
                    Y.ToString(CultureInfo.InvariantCulture) + ")";
            }
        }

        public string DetailText
        {
            get
            {
                if (Type == "wait")
                {
                    return Seconds.ToString("0.0", CultureInfo.InvariantCulture) + " 秒";
                }

                if (Type == "move")
                {
                    return "移 " + MoveMs.ToString(CultureInfo.InvariantCulture) + " ms";
                }

                return "移 " + MoveMs.ToString(CultureInfo.InvariantCulture) + " / 按 " +
                    HoldMs.ToString(CultureInfo.InvariantCulture) + " ms";
            }
        }

        public Dictionary<string, object> ToDictionary()
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            data["type"] = Type;
            data["x"] = X;
            data["y"] = Y;
            data["button"] = Button;
            data["moveMs"] = MoveMs;
            data["holdMs"] = HoldMs;
            data["seconds"] = Seconds;
            return data;
        }

        public static Step FromDictionary(Dictionary<string, object> data)
        {
            Step step = new Step();
            step.Type = Project.GetString(data, "type", "click");
            step.X = Project.GetInt(data, "x", 0);
            step.Y = Project.GetInt(data, "y", 0);
            step.Button = Project.GetString(data, "button", "Left");
            step.MoveMs = Project.GetInt(data, "moveMs", 0);
            step.HoldMs = Project.GetInt(data, "holdMs", 40);
            step.Seconds = Project.GetDouble(data, "seconds", 1.0);
            return step;
        }
    }

    internal sealed class Project
    {
        public string Name = "未命名项目";
        public int LoopCount;
        public double LoopIntervalSeconds;
        public double StartDelaySeconds = 3.0;
        public bool RestoreCursorAfterClick = true;
        public int DefaultHoldMs = 40;
        public List<Step> Steps = new List<Step>();
        public string FilePath = "";

        public static Project Load(string path)
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;

            Dictionary<string, object> data = serializer.Deserialize<Dictionary<string, object>>(json);

            if (data == null)
            {
                throw new InvalidDataException("项目 JSON 为空。");
            }

            Project project = new Project();
            project.FilePath = path;
            project.Name = GetString(data, "name", Path.GetFileNameWithoutExtension(path));
            project.LoopCount = GetInt(data, "loopCount", 0);
            project.LoopIntervalSeconds = GetDouble(data, "loopIntervalSeconds", 0);
            project.StartDelaySeconds = GetDouble(data, "startDelaySeconds", 3.0);
            project.RestoreCursorAfterClick = GetBool(data, "restoreCursorAfterClick", true);
            project.DefaultHoldMs = GetInt(data, "defaultHoldMs", 40);

            object stepsObject;

            if (data.TryGetValue("steps", out stepsObject) && stepsObject != null)
            {
                IEnumerable sequence = stepsObject as IEnumerable;

                if (sequence != null)
                {
                    foreach (object item in sequence)
                    {
                        Dictionary<string, object> stepData = item as Dictionary<string, object>;

                        if (stepData != null)
                        {
                            project.Steps.Add(Step.FromDictionary(stepData));
                        }
                    }
                }
            }

            return project;
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(FilePath))
            {
                throw new InvalidOperationException("项目没有保存路径。");
            }

            Dictionary<string, object> data = new Dictionary<string, object>();
            data["name"] = Name;
            data["loopCount"] = LoopCount;
            data["loopIntervalSeconds"] = LoopIntervalSeconds;
            data["startDelaySeconds"] = StartDelaySeconds;
            data["restoreCursorAfterClick"] = RestoreCursorAfterClick;
            data["defaultHoldMs"] = DefaultHoldMs;

            List<object> stepList = new List<object>();

            foreach (Step step in Steps)
            {
                stepList.Add(step.ToDictionary());
            }

            data["steps"] = stepList;

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            string json = serializer.Serialize(data);

            File.WriteAllText(FilePath, json, new UTF8Encoding(false));
        }

        public static string GetString(Dictionary<string, object> data, string key, string fallback)
        {
            object value;

            if (data.TryGetValue(key, out value) && value != null)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            return fallback;
        }

        public static int GetInt(Dictionary<string, object> data, string key, int fallback)
        {
            object value;

            if (data.TryGetValue(key, out value) && value != null)
            {
                try
                {
                    return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                }
            }

            return fallback;
        }

        public static double GetDouble(Dictionary<string, object> data, string key, double fallback)
        {
            object value;

            if (data.TryGetValue(key, out value) && value != null)
            {
                try
                {
                    return Convert.ToDouble(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                }
            }

            return fallback;
        }

        public static bool GetBool(Dictionary<string, object> data, string key, bool fallback)
        {
            object value;

            if (data.TryGetValue(key, out value) && value != null)
            {
                try
                {
                    return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                }
            }

            return fallback;
        }
    }

    internal sealed class ProjectChoice
    {
        public string Name;
        public string Path;

        public ProjectChoice(string name, string path)
        {
            Name = name;
            Path = path;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    internal static class Prompt
    {
        public static string Show(IWin32Window owner, string title, string label, string initialValue)
        {
            using (Form form = new Form())
            {
                form.Text = title;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterParent;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ClientSize = new Size(420, 150);
                form.BackColor = Color.White;
                form.Font = new Font("Segoe UI", 9F);

                Label caption = new Label();
                caption.Text = label;
                caption.Location = new Point(18, 18);
                caption.AutoSize = true;
                form.Controls.Add(caption);

                TextBox input = new TextBox();
                input.Text = initialValue;
                input.Location = new Point(18, 46);
                input.Width = 380;
                input.BorderStyle = BorderStyle.FixedSingle;
                form.Controls.Add(input);

                Button ok = new Button();
                ok.Text = "确定";
                ok.DialogResult = DialogResult.OK;
                ok.Location = new Point(232, 96);
                ok.Size = new Size(78, 30);
                ok.FlatStyle = FlatStyle.Flat;
                ok.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
                form.Controls.Add(ok);

                Button cancel = new Button();
                cancel.Text = "取消";
                cancel.DialogResult = DialogResult.Cancel;
                cancel.Location = new Point(320, 96);
                cancel.Size = new Size(78, 30);
                cancel.FlatStyle = FlatStyle.Flat;
                cancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
                form.Controls.Add(cancel);

                form.AcceptButton = ok;
                form.CancelButton = cancel;
                input.SelectAll();

                if (form.ShowDialog(owner) == DialogResult.OK)
                {
                    return input.Text.Trim();
                }

                return null;
            }
        }
    }

    internal sealed class OverlayForm : Form
    {
        public OverlayForm(string text)
        {
            Text = "MouseForge";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(640, 64);
            BackColor = Color.FromArgb(15, 23, 42);
            TopMost = true;
            ShowInTaskbar = false;
            Opacity = 0.96;

            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + 56);

            Label label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleCenter;
            label.ForeColor = Color.White;
            label.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            Controls.Add(label);
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x20;        // WS_EX_TRANSPARENT
                cp.ExStyle |= 0x80000;     // WS_EX_LAYERED
                cp.ExStyle |= 0x80;        // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x08000000;  // WS_EX_NOACTIVATE
                return cp;
            }
        }
    }
}
