using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace MouseStudio
{
    internal sealed class MainForm : Form
    {
        private static readonly Color Bg = Color.FromArgb(245, 247, 250);
        private static readonly Color HeaderBg = Color.FromArgb(15, 23, 42);
        private static readonly Color Accent = Color.FromArgb(37, 99, 235);
        private static readonly Color Green = Color.FromArgb(22, 163, 74);
        private static readonly Color Red = Color.FromArgb(220, 38, 38);
        private static readonly Color Amber = Color.FromArgb(217, 119, 6);
        private static readonly Color TextDark = Color.FromArgb(15, 23, 42);
        private static readonly Color TextMuted = Color.FromArgb(100, 116, 139);
        private static readonly Color Border = Color.FromArgb(226, 232, 240);
        private static readonly Color Selection = Color.FromArgb(219, 234, 254);

        private readonly string projectsDir;
        private readonly string initialProjectPath;

        private Project project;
        private bool loadingProjects;
        private bool loadingProject;
        private bool refreshingSteps;
        private bool dirty;

        private bool recording;
        private bool pickMode;
        private bool playing;
        private bool paused;
        private bool moving;

        private MouseHook mouseHook;
        private OverlayForm pickOverlay;
        private Timer playTimer;
        private Timer pickTimer;

        private DateTime lastRecordedClick = DateTime.MinValue;
        private DateTime pickStarted;

        private Step pickStep;
        private Step moveStep;
        private Point moveFrom;
        private Point moveTo;
        private Point clickReturnPoint;
        private Point playbackInitialCursor;
        private DateTime moveStart;
        private DateTime waitUntil;
        private DateTime pauseStarted;
        private int moveDuration;
        private int playbackStepIndex;
        private long playbackLoopIndex;
        private bool inLoopWait;
        private List<Step> playbackSteps;
        private bool hotkeysRegistered;

        private ComboBox projectCombo;
        private Button btnNewProject;
        private Button btnSaveProject;
        private Button btnDeleteProject;
        private Button btnRecord;
        private Button btnPlay;
        private Button btnPause;
        private Button btnStop;

        private Panel settingsPanel;
        private NumericUpDown loopCountNum;
        private NumericUpDown loopIntervalNum;
        private NumericUpDown startDelayNum;
        private NumericUpDown defaultHoldNum;
        private CheckBox restoreCursorCheck;
        private CheckBox clearOnRecordCheck;

        private ListView stepList;
        private Label stepCountLabel;
        private Panel stepButtonsPanel;
        private RichTextBox logBox;
        private Label statusLabel;

        private Panel propertiesPanel;
        private Label selectedLabel;
        private Label typeLabel;
        private Panel rowXY;
        private Panel rowButton;
        private Panel rowMove;
        private Panel rowHold;
        private Panel rowWait;
        private NumericUpDown xNum;
        private NumericUpDown yNum;
        private ComboBox buttonCombo;
        private NumericUpDown moveNum;
        private NumericUpDown holdNum;
        private NumericUpDown waitNum;
        private Button btnApply;
        private Button btnTest;
        private Button btnPick;

        public MainForm(string initialProjectPath)
        {
            this.initialProjectPath = initialProjectPath;
            projectsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "projects");

            project = new Project();
            project.Name = "默认项目";
            project.FilePath = Path.Combine(projectsDir, "默认项目.json");

            Text = "Mouse Studio - 鼠标脚本工坊";
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.None;
            Size = new Size(1140, 760);
            MinimumSize = new Size(1100, 700);
            BackColor = Bg;
            DoubleBuffered = true;
            KeyPreview = true;

            BuildUi();
            ApplyDpiScale();
            InitializeTimers();
            InitializeProjects();

            FormClosing += MainFormClosing;
        }

        private static Font UIFont(float size)
        {
            return new Font("Segoe UI", size, FontStyle.Regular);
        }

        private static Font UIFont(float size, FontStyle style)
        {
            return new Font("Segoe UI", size, style);
        }

        private Label MakeLabel(string text, float size, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.ForeColor = color;
            label.Font = UIFont(size);
            return label;
        }

        private Label MakeBoldLabel(string text, float size, Color color)
        {
            Label label = MakeLabel(text, size, color);
            label.Font = UIFont(size, FontStyle.Bold);
            return label;
        }

        private Button MakeButton(string text, Color back, Color fore, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = width;
            button.Height = 32;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = back;
            button.ForeColor = fore;
            button.Font = UIFont(9F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.Margin = new Padding(4, 5, 4, 4);
            button.UseVisualStyleBackColor = false;
            AddHover(button, back);
            return button;
        }

        private Button MakeSecondaryButton(string text, int width)
        {
            Button button = MakeButton(text, Color.White, TextDark, width);
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Border;
            button.Font = UIFont(9F);
            return button;
        }

        private void AddHover(Button button, Color normal)
        {
            Color hover;

            if (normal == Color.White)
            {
                hover = Color.FromArgb(248, 250, 252);
            }
            else
            {
                hover = ControlPaint.Light(normal, 0.12F);
            }

            button.MouseEnter += delegate { button.BackColor = hover; };
            button.MouseLeave += delegate { button.BackColor = normal; };
        }

        private NumericUpDown MakeNumeric(decimal min, decimal max, decimal value, int decimals, int width)
        {
            NumericUpDown numeric = new NumericUpDown();
            numeric.Minimum = min;
            numeric.Maximum = max;
            numeric.Value = value;
            numeric.DecimalPlaces = decimals;
            numeric.Increment = decimals > 0 ? 0.5M : 1M;
            numeric.Width = width;
            numeric.Height = 28;
            numeric.Font = UIFont(9F);
            numeric.BorderStyle = BorderStyle.FixedSingle;
            numeric.TextAlign = HorizontalAlignment.Left;
            return numeric;
        }

        private Panel CreateCard()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Color.White;
            panel.Padding = new Padding(14);
            panel.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Border))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1);
                }
            };
            return panel;
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.BackColor = Bg;
            root.Margin = Padding.Empty;
            root.Padding = Padding.Empty;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildToolbar(), 0, 1);
            root.Controls.Add(BuildContent(), 0, 2);
            root.Controls.Add(BuildStatusBar(), 0, 3);

            Controls.Add(root);
        }

        private Control BuildHeader()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = HeaderBg;
            header.Margin = Padding.Empty;

            Panel accent = new Panel();
            accent.Dock = DockStyle.Bottom;
            accent.Height = 3;
            accent.BackColor = Accent;
            header.Controls.Add(accent);

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.RowCount = 1;
            layout.BackColor = HeaderBg;
            layout.Padding = new Padding(22, 8, 22, 6);
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            Panel titles = new Panel();
            titles.Dock = DockStyle.Fill;
            titles.BackColor = HeaderBg;

            Label title = new Label();
            title.Text = "Mouse Studio";
            title.AutoSize = true;
            title.Location = new Point(0, 2);
            title.ForeColor = Color.White;
            title.Font = UIFont(19F, FontStyle.Bold);
            titles.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "可视化鼠标脚本工坊 · 录制、编辑、循环播放";
            subtitle.AutoSize = true;
            subtitle.Location = new Point(2, 38);
            subtitle.ForeColor = Color.FromArgb(148, 163, 184);
            subtitle.Font = UIFont(9.5F);
            titles.Controls.Add(subtitle);

            Label hotkeys = new Label();
            hotkeys.Text = "F9  录制      F10  播放 / 停止      F11  暂停";
            hotkeys.AutoSize = true;
            hotkeys.Anchor = AnchorStyles.Right;
            hotkeys.TextAlign = ContentAlignment.MiddleRight;
            hotkeys.ForeColor = Color.FromArgb(148, 163, 184);
            hotkeys.Font = UIFont(9F);

            layout.Controls.Add(titles, 0, 0);
            layout.Controls.Add(hotkeys, 1, 0);
            header.Controls.Add(layout);

            return header;
        }

        private Control BuildToolbar()
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Fill;
            bar.BackColor = Color.White;
            bar.Padding = new Padding(14, 8, 14, 8);
            bar.Margin = Padding.Empty;

            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.FlowDirection = FlowDirection.LeftToRight;
            flow.WrapContents = false;
            flow.AutoScroll = true;
            flow.BackColor = Color.White;
            flow.Padding = Padding.Empty;

            Label projectLabel = MakeBoldLabel("项目", 9F, TextDark);
            projectLabel.Margin = new Padding(2, 10, 4, 0);
            flow.Controls.Add(projectLabel);

            projectCombo = new ComboBox();
            projectCombo.Width = 220;
            projectCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            projectCombo.Font = UIFont(9.5F);
            projectCombo.Margin = new Padding(0, 6, 4, 4);
            projectCombo.SelectedIndexChanged += ProjectComboChanged;
            flow.Controls.Add(projectCombo);

            btnNewProject = MakeSecondaryButton("新建", 60);
            btnNewProject.Click += NewProjectClicked;
            flow.Controls.Add(btnNewProject);

            btnSaveProject = MakeSecondaryButton("保存", 60);
            btnSaveProject.Click += SaveProjectClicked;
            flow.Controls.Add(btnSaveProject);

            btnDeleteProject = MakeSecondaryButton("删除", 60);
            btnDeleteProject.Click += DeleteProjectClicked;
            flow.Controls.Add(btnDeleteProject);

            Panel sep1 = new Panel();
            sep1.Width = 1;
            sep1.Height = 30;
            sep1.BackColor = Border;
            sep1.Margin = new Padding(10, 7, 10, 7);
            flow.Controls.Add(sep1);

            btnRecord = MakeButton("●  录制 (F9)", Red, Color.White, 128);
            btnRecord.Click += RecordClicked;
            flow.Controls.Add(btnRecord);

            btnPlay = MakeButton("▶  播放 (F10)", Green, Color.White, 128);
            btnPlay.Click += PlayClicked;
            flow.Controls.Add(btnPlay);

            btnPause = MakeButton("暂停 (F11)", Amber, Color.White, 128);
            btnPause.Click += PauseClicked;
            flow.Controls.Add(btnPause);

            btnStop = MakeButton("■  停止", Color.FromArgb(51, 65, 85), Color.White, 86);
            btnStop.Click += StopClicked;
            flow.Controls.Add(btnStop);

            bar.Controls.Add(flow);
            return bar;
        }

        private Control BuildContent()
        {
            TableLayoutPanel content = new TableLayoutPanel();
            content.Dock = DockStyle.Fill;
            content.ColumnCount = 3;
            content.RowCount = 1;
            content.BackColor = Bg;
            content.Padding = new Padding(14, 12, 14, 12);
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 248F));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 318F));

            Control left = BuildSettingsCard();
            left.Margin = new Padding(0, 0, 12, 0);

            Control center = BuildCenterArea();
            center.Margin = new Padding(0, 0, 12, 0);

            Control right = BuildPropertiesCard();
            right.Margin = Padding.Empty;

            content.Controls.Add(left, 0, 0);
            content.Controls.Add(center, 1, 0);
            content.Controls.Add(right, 2, 0);

            return content;
        }

        private Control BuildSettingsCard()
        {
            Panel card = CreateCard();
            settingsPanel = card;

            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            flow.AutoScroll = true;
            flow.BackColor = Color.White;
            flow.Padding = Padding.Empty;

            Label title = MakeBoldLabel("项目设置", 11.5F, TextDark);
            title.Margin = new Padding(0, 0, 0, 2);
            flow.Controls.Add(title);

            Label hint = MakeLabel("循环和点击的默认行为", 8.5F, TextMuted);
            hint.Margin = new Padding(0, 0, 0, 10);
            flow.Controls.Add(hint);

            TableLayoutPanel table = new TableLayoutPanel();
            table.Width = 218;
            table.ColumnCount = 2;
            table.RowCount = 0;
            table.AutoSize = true;
            table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            table.BackColor = Color.White;
            table.Margin = Padding.Empty;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));

            loopCountNum = MakeNumeric(0, 1000000, 0, 0, 58);
            AddSettingRow(table, "循环次数", loopCountNum, "0=∞");
            loopCountNum.ValueChanged += SettingChanged;

            loopIntervalNum = MakeNumeric(0, 86400, 0, 2, 68);
            AddSettingRow(table, "循环间隔", loopIntervalNum, "秒");
            loopIntervalNum.ValueChanged += SettingChanged;

            startDelayNum = MakeNumeric(0, 86400, 3, 2, 68);
            AddSettingRow(table, "开始等待", startDelayNum, "秒");
            startDelayNum.ValueChanged += SettingChanged;

            defaultHoldNum = MakeNumeric(0, 5000, 40, 0, 68);
            AddSettingRow(table, "点击按住", defaultHoldNum, "ms");
            defaultHoldNum.ValueChanged += SettingChanged;

            restoreCursorCheck = new CheckBox();
            restoreCursorCheck.Text = "点击后把鼠标还原";
            restoreCursorCheck.AutoSize = true;
            restoreCursorCheck.ForeColor = TextDark;
            restoreCursorCheck.Font = UIFont(9F);
            restoreCursorCheck.Margin = new Padding(0, 8, 0, 4);
            restoreCursorCheck.CheckedChanged += SettingChanged;
            table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(restoreCursorCheck, 0, table.RowCount - 1);
            table.SetColumnSpan(restoreCursorCheck, 2);

            clearOnRecordCheck = new CheckBox();
            clearOnRecordCheck.Text = "录制前清空现有步骤";
            clearOnRecordCheck.AutoSize = true;
            clearOnRecordCheck.Checked = true;
            clearOnRecordCheck.ForeColor = TextDark;
            clearOnRecordCheck.Font = UIFont(9F);
            clearOnRecordCheck.Margin = new Padding(0, 2, 0, 4);
            clearOnRecordCheck.CheckedChanged += SettingChanged;
            table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(clearOnRecordCheck, 0, table.RowCount - 1);
            table.SetColumnSpan(clearOnRecordCheck, 2);

            table.Height = table.GetPreferredSize(new Size(218, 0)).Height;
            flow.Controls.Add(table);

            Label recordHint = MakeLabel(
                "录制只记录点击，不记录鼠标移动轨迹。\n点击后会按位置生成 Click 步骤，\n两次点击之间的间隔会生成 Wait 步骤。",
                8.5F,
                TextMuted
            );
            recordHint.MaximumSize = new Size(218, 0);
            recordHint.Margin = new Padding(0, 14, 0, 0);
            flow.Controls.Add(recordHint);

            card.Controls.Add(flow);
            return card;
        }

        private void AddSettingRow(TableLayoutPanel table, string labelText, Control control, string suffix)
        {
            int row = table.RowCount;
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label label = MakeLabel(labelText, 9F, TextDark);
            label.Margin = new Padding(0, 6, 4, 4);
            table.Controls.Add(label, 0, row);

            Panel valuePanel = new Panel();
            valuePanel.Width = 90;
            valuePanel.Height = 30;
            valuePanel.Margin = new Padding(0, 2, 0, 2);
            valuePanel.BackColor = Color.White;

            control.Location = new Point(0, 0);
            valuePanel.Controls.Add(control);

            if (!string.IsNullOrEmpty(suffix))
            {
                Label suffixLabel = MakeLabel(suffix, 8F, TextMuted);
                suffixLabel.Location = new Point(control.Width + 4, 6);
                valuePanel.Controls.Add(suffixLabel);
            }

            table.Controls.Add(valuePanel, 1, row);
        }

        private Control BuildCenterArea()
        {
            TableLayoutPanel center = new TableLayoutPanel();
            center.Dock = DockStyle.Fill;
            center.ColumnCount = 1;
            center.RowCount = 2;
            center.BackColor = Bg;
            center.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            center.RowStyles.Add(new RowStyle(SizeType.Absolute, 148F));

            Panel stepsCard = CreateCard();
            stepsCard.Margin = new Padding(0, 0, 0, 10);

            TableLayoutPanel stepsLayout = new TableLayoutPanel();
            stepsLayout.Dock = DockStyle.Fill;
            stepsLayout.ColumnCount = 1;
            stepsLayout.RowCount = 3;
            stepsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            stepsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            stepsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            TableLayoutPanel stepsHeader = new TableLayoutPanel();
            stepsHeader.Dock = DockStyle.Fill;
            stepsHeader.ColumnCount = 2;
            stepsHeader.RowCount = 1;
            stepsHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            stepsHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            stepsHeader.Margin = Padding.Empty;

            Label stepsTitle = MakeBoldLabel("脚本步骤", 11.5F, TextDark);
            stepsTitle.Margin = new Padding(0, 2, 0, 0);
            stepsHeader.Controls.Add(stepsTitle, 0, 0);

            stepCountLabel = MakeLabel("0 步", 8.5F, TextMuted);
            stepCountLabel.Anchor = AnchorStyles.Right;
            stepCountLabel.Margin = new Padding(0, 5, 0, 0);
            stepsHeader.Controls.Add(stepCountLabel, 1, 0);
            stepsLayout.Controls.Add(stepsHeader, 0, 0);

            FlowLayoutPanel stepButtons = new FlowLayoutPanel();
            stepButtons.Dock = DockStyle.Fill;
            stepButtons.FlowDirection = FlowDirection.LeftToRight;
            stepButtons.WrapContents = false;
            stepButtons.AutoScroll = false;
            stepButtons.BackColor = Color.White;
            stepButtons.Margin = new Padding(0, 0, 0, 6);
            stepButtonsPanel = stepButtons;

            Button addClick = MakeSecondaryButton("＋点击", 72);
            addClick.Height = 30;
            addClick.Click += AddClickClicked;
            stepButtons.Controls.Add(addClick);

            Button addMove = MakeSecondaryButton("＋移动", 72);
            addMove.Height = 30;
            addMove.Click += AddMoveClicked;
            stepButtons.Controls.Add(addMove);

            Button addWait = MakeSecondaryButton("＋等待", 72);
            addWait.Height = 30;
            addWait.Click += AddWaitClicked;
            stepButtons.Controls.Add(addWait);

            Panel sep = new Panel();
            sep.Width = 1;
            sep.Height = 26;
            sep.BackColor = Border;
            sep.Margin = new Padding(6, 7, 6, 7);
            stepButtons.Controls.Add(sep);

            Button duplicate = MakeSecondaryButton("复制", 48);
            duplicate.Height = 30;
            duplicate.Click += DuplicateClicked;
            stepButtons.Controls.Add(duplicate);

            Button delete = MakeSecondaryButton("删除", 48);
            delete.Height = 30;
            delete.Click += DeleteStepClicked;
            stepButtons.Controls.Add(delete);

            Button up = MakeSecondaryButton("上移", 48);
            up.Height = 30;
            up.Click += MoveUpClicked;
            stepButtons.Controls.Add(up);

            Button down = MakeSecondaryButton("下移", 48);
            down.Height = 30;
            down.Click += MoveDownClicked;
            stepButtons.Controls.Add(down);

            stepsLayout.Controls.Add(stepButtons, 0, 1);

            stepList = new ListView();
            stepList.Dock = DockStyle.Fill;
            stepList.View = View.Details;
            stepList.FullRowSelect = true;
            stepList.HideSelection = false;
            stepList.MultiSelect = false;
            stepList.GridLines = false;
            stepList.BorderStyle = BorderStyle.None;
            stepList.Font = UIFont(9.5F);
            stepList.BackColor = Color.White;
            stepList.ForeColor = TextDark;
            stepList.Columns.Add("#", 30);
            stepList.Columns.Add("行为", 52);
            stepList.Columns.Add("坐标 / 参数", 135);
            stepList.Columns.Add("按键", 45);
            stepList.Columns.Add("耗时", 110);
            stepList.SelectedIndexChanged += StepSelectionChanged;
            stepList.DoubleClick += StepListDoubleClick;
            stepList.KeyDown += StepListKeyDown;
            stepsLayout.Controls.Add(stepList, 0, 2);

            stepsCard.Controls.Add(stepsLayout);
            center.Controls.Add(stepsCard, 0, 0);

            Panel logCard = CreateCard();
            logCard.Margin = Padding.Empty;

            TableLayoutPanel logLayout = new TableLayoutPanel();
            logLayout.Dock = DockStyle.Fill;
            logLayout.ColumnCount = 1;
            logLayout.RowCount = 2;
            logLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            logLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            TableLayoutPanel logHeader = new TableLayoutPanel();
            logHeader.Dock = DockStyle.Fill;
            logHeader.ColumnCount = 2;
            logHeader.RowCount = 1;
            logHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            logHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            Label logTitle = MakeBoldLabel("运行日志", 10F, TextDark);
            logTitle.Margin = new Padding(0, 2, 0, 0);
            logHeader.Controls.Add(logTitle, 0, 0);

            Button clearLog = MakeSecondaryButton("清空", 52);
            clearLog.Height = 26;
            clearLog.Margin = new Padding(0, 0, 0, 2);
            clearLog.Click += delegate { logBox.Clear(); };
            logHeader.Controls.Add(clearLog, 1, 0);
            logLayout.Controls.Add(logHeader, 0, 0);

            logBox = new RichTextBox();
            logBox.Dock = DockStyle.Fill;
            logBox.ReadOnly = true;
            logBox.BorderStyle = BorderStyle.None;
            logBox.BackColor = Color.FromArgb(248, 250, 252);
            logBox.ForeColor = TextDark;
            logBox.Font = new Font("Consolas", 9F);
            logBox.DetectUrls = false;
            logLayout.Controls.Add(logBox, 0, 1);

            logCard.Controls.Add(logLayout);
            center.Controls.Add(logCard, 0, 1);

            return center;
        }

        private Control BuildPropertiesCard()
        {
            Panel card = CreateCard();
            propertiesPanel = card;

            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            flow.AutoScroll = true;
            flow.BackColor = Color.White;
            flow.Padding = Padding.Empty;

            Label title = MakeBoldLabel("步骤属性", 11.5F, TextDark);
            title.Margin = new Padding(0, 0, 0, 2);
            flow.Controls.Add(title);

            selectedLabel = MakeLabel("未选择步骤", 8.5F, TextMuted);
            selectedLabel.Margin = new Padding(0, 0, 0, 6);
            flow.Controls.Add(selectedLabel);

            typeLabel = MakeBoldLabel("—", 10.5F, Accent);
            typeLabel.Margin = new Padding(0, 0, 0, 8);
            flow.Controls.Add(typeLabel);

            rowXY = new Panel();
            rowXY.Width = 280;
            rowXY.Height = 64;
            rowXY.BackColor = Color.White;
            rowXY.Margin = new Padding(0, 0, 0, 4);

            Label xyLabel = MakeLabel("目标坐标", 8.5F, TextMuted);
            xyLabel.Location = new Point(0, 2);
            rowXY.Controls.Add(xyLabel);

            xNum = MakeNumeric(-100000, 100000, 0, 0, 128);
            xNum.Location = new Point(0, 26);
            rowXY.Controls.Add(xNum);

            yNum = MakeNumeric(-100000, 100000, 0, 0, 128);
            yNum.Location = new Point(140, 26);
            rowXY.Controls.Add(yNum);

            flow.Controls.Add(rowXY);

            btnPick = MakeSecondaryButton("拾取坐标（右 Alt + 左键）", 280);
            btnPick.Height = 32;
            btnPick.Margin = new Padding(0, 0, 0, 8);
            btnPick.Click += PickClicked;
            flow.Controls.Add(btnPick);

            rowButton = MakeFieldRow("按键", null, "");
            buttonCombo = new ComboBox();
            buttonCombo.Width = 150;
            buttonCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            buttonCombo.Font = UIFont(9F);
            buttonCombo.Items.Add("左键");
            buttonCombo.Items.Add("右键");
            buttonCombo.Items.Add("中键");
            buttonCombo.SelectedIndex = 0;
            buttonCombo.Location = new Point(86, 2);
            ((Panel)rowButton).Controls.Add(buttonCombo);
            flow.Controls.Add(rowButton);

            rowMove = new Panel();
            rowMove.Width = 280;
            rowMove.Height = 34;
            rowMove.BackColor = Color.White;
            rowMove.Margin = new Padding(0, 0, 0, 2);
            Label moveLabel = MakeLabel("移动耗时", 9F, TextDark);
            moveLabel.Location = new Point(0, 6);
            rowMove.Controls.Add(moveLabel);
            moveNum = MakeNumeric(0, 60000, 0, 0, 110);
            moveNum.Location = new Point(86, 2);
            rowMove.Controls.Add(moveNum);
            Label moveSuffix = MakeLabel("ms", 8F, TextMuted);
            moveSuffix.Location = new Point(202, 8);
            rowMove.Controls.Add(moveSuffix);
            flow.Controls.Add(rowMove);

            rowHold = new Panel();
            rowHold.Width = 280;
            rowHold.Height = 34;
            rowHold.BackColor = Color.White;
            rowHold.Margin = new Padding(0, 0, 0, 2);
            Label holdLabel = MakeLabel("按住时长", 9F, TextDark);
            holdLabel.Location = new Point(0, 6);
            rowHold.Controls.Add(holdLabel);
            holdNum = MakeNumeric(0, 5000, 40, 0, 110);
            holdNum.Location = new Point(86, 2);
            rowHold.Controls.Add(holdNum);
            Label holdSuffix = MakeLabel("ms", 8F, TextMuted);
            holdSuffix.Location = new Point(202, 8);
            rowHold.Controls.Add(holdSuffix);
            flow.Controls.Add(rowHold);

            rowWait = new Panel();
            rowWait.Width = 280;
            rowWait.Height = 34;
            rowWait.BackColor = Color.White;
            rowWait.Margin = new Padding(0, 0, 0, 2);
            Label waitLabel = MakeLabel("等待时长", 9F, TextDark);
            waitLabel.Location = new Point(0, 6);
            rowWait.Controls.Add(waitLabel);
            waitNum = MakeNumeric(0, 86400, 1, 2, 110);
            waitNum.Location = new Point(86, 2);
            rowWait.Controls.Add(waitNum);
            Label waitSuffix = MakeLabel("秒", 8F, TextMuted);
            waitSuffix.Location = new Point(202, 8);
            rowWait.Controls.Add(waitSuffix);
            flow.Controls.Add(rowWait);

            btnApply = MakeButton("应用修改", Accent, Color.White, 280);
            btnApply.Height = 34;
            btnApply.Margin = new Padding(0, 10, 0, 4);
            btnApply.Click += ApplyClicked;
            flow.Controls.Add(btnApply);

            btnTest = MakeSecondaryButton("测试这一步", 280);
            btnTest.Height = 32;
            btnTest.Margin = new Padding(0, 0, 0, 8);
            btnTest.Click += TestStepClicked;
            flow.Controls.Add(btnTest);

            Label tip = MakeLabel(
                "提示：添加步骤后，在左侧列表选中它，\n在这里修改参数。双击列表也可以快速编辑。",
                8.5F,
                TextMuted
            );
            tip.MaximumSize = new Size(280, 0);
            tip.Margin = new Padding(0, 8, 0, 0);
            flow.Controls.Add(tip);

            card.Controls.Add(flow);
            ShowPropertiesFor(null);
            return card;
        }

        private Panel MakeFieldRow(string labelText, Control control, string suffix)
        {
            Panel panel = new Panel();
            panel.Width = 280;
            panel.Height = 34;
            panel.BackColor = Color.White;
            panel.Margin = new Padding(0, 0, 0, 2);

            Label label = MakeLabel(labelText, 9F, TextDark);
            label.Location = new Point(0, 6);
            panel.Controls.Add(label);

            if (control != null)
            {
                control.Location = new Point(86, 2);
                panel.Controls.Add(control);
            }

            if (!string.IsNullOrEmpty(suffix))
            {
                Label suffixLabel = MakeLabel(suffix, 8F, TextMuted);
                suffixLabel.Location = new Point(202, 8);
                panel.Controls.Add(suffixLabel);
            }

            return panel;
        }

        private Control BuildStatusBar()
        {
            Panel status = new Panel();
            status.Dock = DockStyle.Fill;
            status.BackColor = HeaderBg;
            status.Margin = Padding.Empty;

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Padding = new Padding(16, 0, 16, 0);
            statusLabel.ForeColor = Color.FromArgb(203, 213, 225);
            statusLabel.Font = UIFont(8.8F);
            status.Controls.Add(statusLabel);

            return status;
        }

        private void ApplyDpiScale()
        {
            float scale = Native.GetSystemScale();

            if (scale <= 1.01F)
            {
                return;
            }

            List<KeyValuePair<Control, Font>> fonts = new List<KeyValuePair<Control, Font>>();
            CaptureFonts(this, fonts);
            Scale(new SizeF(scale, scale));

            foreach (KeyValuePair<Control, Font> item in fonts)
            {
                if (!item.Key.IsDisposed)
                {
                    item.Key.Font = new Font(item.Value.FontFamily, item.Value.Size, item.Value.Style);
                }
            }

            Size = new Size(
                (int)Math.Round(1140 * scale),
                (int)Math.Round(760 * scale)
            );
            MinimumSize = new Size(
                (int)Math.Round(1100 * scale),
                (int)Math.Round(700 * scale)
            );

            if (stepList != null)
            {
                int[] widths = new int[] { 30, 52, 135, 45, 110 };

                for (int i = 0; i < stepList.Columns.Count && i < widths.Length; i++)
                {
                    stepList.Columns[i].Width = (int)Math.Round(widths[i] * scale);
                }
            }
        }

        private static void CaptureFonts(Control parent, List<KeyValuePair<Control, Font>> fonts)
        {
            fonts.Add(new KeyValuePair<Control, Font>(parent, parent.Font));

            foreach (Control child in parent.Controls)
            {
                CaptureFonts(child, fonts);
            }
        }

        private void InitializeTimers()
        {
            playTimer = new Timer();
            playTimer.Interval = 20;
            playTimer.Tick += PlayTimerTick;

            pickTimer = new Timer();
            pickTimer.Interval = 50;
            pickTimer.Tick += PickTimerTick;
        }

        private void InitializeProjects()
        {
            Directory.CreateDirectory(projectsDir);
            string[] files = Directory.GetFiles(projectsDir, "*.json");

            if (files.Length == 0)
            {
                Project defaultProject = new Project();
                defaultProject.Name = "默认项目";
                defaultProject.FilePath = Path.Combine(projectsDir, "默认项目.json");
                defaultProject.StartDelaySeconds = 3;
                defaultProject.Save();
                files = Directory.GetFiles(projectsDir, "*.json");
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            loadingProjects = true;
            projectCombo.Items.Clear();

            foreach (string file in files)
            {
                projectCombo.Items.Add(new ProjectChoice(Path.GetFileNameWithoutExtension(file), file));
            }

            loadingProjects = false;

            string selectPath = null;

            if (!string.IsNullOrEmpty(initialProjectPath) && File.Exists(initialProjectPath))
            {
                selectPath = Path.GetFullPath(initialProjectPath);
                bool found = false;

                foreach (object item in projectCombo.Items)
                {
                    ProjectChoice choice = item as ProjectChoice;

                    if (choice != null && string.Equals(choice.Path, selectPath, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    projectCombo.Items.Add(new ProjectChoice(Path.GetFileNameWithoutExtension(selectPath), selectPath));
                }
            }
            else if (projectCombo.Items.Count > 0)
            {
                ProjectChoice first = projectCombo.Items[0] as ProjectChoice;
                selectPath = first != null ? first.Path : null;
            }

            if (!string.IsNullOrEmpty(selectPath))
            {
                SelectProjectInCombo(selectPath);
                LoadProject(selectPath);
            }
            else
            {
                Log("没有可用的项目。");
            }
        }

        private void SelectProjectInCombo(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            for (int i = 0; i < projectCombo.Items.Count; i++)
            {
                ProjectChoice choice = projectCombo.Items[i] as ProjectChoice;

                if (choice != null && string.Equals(choice.Path, path, StringComparison.OrdinalIgnoreCase))
                {
                    loadingProjects = true;
                    projectCombo.SelectedIndex = i;
                    loadingProjects = false;
                    return;
                }
            }
        }

        private void ProjectComboChanged(object sender, EventArgs e)
        {
            if (loadingProjects)
            {
                return;
            }

            ProjectChoice choice = projectCombo.SelectedItem as ProjectChoice;

            if (choice == null)
            {
                return;
            }

            if (project != null && string.Equals(choice.Path, project.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!ConfirmDiscardChanges())
            {
                SelectProjectInCombo(project != null ? project.FilePath : null);
                return;
            }

            LoadProject(choice.Path);
        }

        private void LoadProject(string path)
        {
            try
            {
                loadingProject = true;
                project = Project.Load(path);
                UpdateSettingsFromProject();
                RefreshStepList(-1);
                dirty = false;
                loadingProject = false;
                SelectProjectInCombo(path);
                UpdateTitle();
                UpdateUiState();
                Log("已打开项目：" + project.Name);
            }
            catch (Exception ex)
            {
                loadingProject = false;
                MessageBox.Show(this, "打开项目失败：\r\n" + ex.Message, "Mouse Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log("打开项目失败：" + ex.Message);
            }
        }

        private void UpdateSettingsFromProject()
        {
            loadingProject = true;
            loopCountNum.Value = ClampDecimal(project.LoopCount, loopCountNum.Minimum, loopCountNum.Maximum);
            loopIntervalNum.Value = ClampDecimal((decimal)project.LoopIntervalSeconds, loopIntervalNum.Minimum, loopIntervalNum.Maximum);
            startDelayNum.Value = ClampDecimal((decimal)project.StartDelaySeconds, startDelayNum.Minimum, startDelayNum.Maximum);
            defaultHoldNum.Value = ClampDecimal(project.DefaultHoldMs, defaultHoldNum.Minimum, defaultHoldNum.Maximum);
            restoreCursorCheck.Checked = project.RestoreCursorAfterClick;
            loadingProject = false;
        }

        private void ReadSettingsIntoProject()
        {
            project.LoopCount = (int)loopCountNum.Value;
            project.LoopIntervalSeconds = (double)loopIntervalNum.Value;
            project.StartDelaySeconds = (double)startDelayNum.Value;
            project.DefaultHoldMs = (int)defaultHoldNum.Value;
            project.RestoreCursorAfterClick = restoreCursorCheck.Checked;
        }

        private static decimal ClampDecimal(decimal value, decimal min, decimal max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private void SettingChanged(object sender, EventArgs e)
        {
            if (!loadingProject)
            {
                MarkDirty();
            }
        }

        private void SaveProjectClicked(object sender, EventArgs e)
        {
            SaveProject();
        }

        private bool SaveProject()
        {
            if (project == null)
            {
                return false;
            }

            try
            {
                ReadSettingsIntoProject();
                project.Save();
                dirty = false;
                UpdateTitle();
                Log("项目已保存：" + project.FilePath);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "保存项目失败：\r\n" + ex.Message, "Mouse Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log("保存失败：" + ex.Message);
                return false;
            }
        }

        private void NewProjectClicked(object sender, EventArgs e)
        {
            if (playing || recording || pickMode)
            {
                return;
            }

            if (!ConfirmDiscardChanges())
            {
                return;
            }

            string name = Prompt.Show(this, "新建项目", "项目名称：", "新项目");

            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            string safeName = SanitizeFileName(name);
            string path = Path.Combine(projectsDir, safeName + ".json");

            if (File.Exists(path))
            {
                MessageBox.Show(this, "同名项目已经存在。", "Mouse Studio", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Project created = new Project();
            created.Name = name;
            created.FilePath = path;
            created.StartDelaySeconds = 3;
            created.Save();

            RefreshProjectList(path);
            Log("已新建项目：" + name);
        }

        private void DeleteProjectClicked(object sender, EventArgs e)
        {
            if (project == null || playing || recording || pickMode)
            {
                return;
            }

            DialogResult result = MessageBox.Show(
                this,
                "确定删除项目“" + project.Name + "”吗？\r\n项目文件会被删除。",
                "Mouse Studio",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (result != DialogResult.Yes)
            {
                return;
            }

            try
            {
                string deletedName = project.Name;
                string deletedPath = project.FilePath;
                File.Delete(deletedPath);
                Log("已删除项目：" + deletedName);

                string[] files = Directory.GetFiles(projectsDir, "*.json");

                if (files.Length == 0)
                {
                    Project defaultProject = new Project();
                    defaultProject.Name = "默认项目";
                    defaultProject.FilePath = Path.Combine(projectsDir, "默认项目.json");
                    defaultProject.StartDelaySeconds = 3;
                    defaultProject.Save();
                    files = Directory.GetFiles(projectsDir, "*.json");
                }

                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                RefreshProjectList(files[0]);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "删除项目失败：\r\n" + ex.Message, "Mouse Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RefreshProjectList(string selectPath)
        {
            string[] files = Directory.GetFiles(projectsDir, "*.json");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            loadingProjects = true;
            projectCombo.Items.Clear();

            foreach (string file in files)
            {
                projectCombo.Items.Add(new ProjectChoice(Path.GetFileNameWithoutExtension(file), file));
            }

            loadingProjects = false;
            SelectProjectInCombo(selectPath);

            if (projectCombo.SelectedIndex < 0 && projectCombo.Items.Count > 0)
            {
                loadingProjects = true;
                projectCombo.SelectedIndex = 0;
                loadingProjects = false;
            }

            ProjectChoice choice = projectCombo.SelectedItem as ProjectChoice;

            if (choice != null)
            {
                LoadProject(choice.Path);
            }
        }

        private static string SanitizeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder builder = new StringBuilder(name.Length);

            foreach (char c in name)
            {
                bool bad = false;

                foreach (char check in invalid)
                {
                    if (c == check)
                    {
                        bad = true;
                        break;
                    }
                }

                builder.Append(bad ? '_' : c);
            }

            string result = builder.ToString().Trim();

            if (result.Length == 0)
            {
                result = "项目";
            }

            return result;
        }

        private bool ConfirmDiscardChanges()
        {
            if (!dirty)
            {
                return true;
            }

            DialogResult result = MessageBox.Show(
                this,
                "当前项目有未保存的修改，要保存吗？",
                "Mouse Studio",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question
            );

            if (result == DialogResult.Cancel)
            {
                return false;
            }

            if (result == DialogResult.Yes)
            {
                return SaveProject();
            }

            return true;
        }

        private void MarkDirty()
        {
            if (loadingProject)
            {
                return;
            }

            dirty = true;
            UpdateTitle();
        }

        private void UpdateTitle()
        {
            string suffix = dirty ? " *" : "";

            if (recording)
            {
                suffix += "  [录制中]";
            }
            else if (playing)
            {
                suffix += "  [播放中]";
            }

            Text = "Mouse Studio - " + (project != null ? project.Name : "未命名项目") + suffix;
        }

        private void Log(string message)
        {
            if (logBox == null)
            {
                return;
            }

            string line = DateTime.Now.ToString("HH:mm:ss") + "  " + message + Environment.NewLine;
            logBox.AppendText(line);

            if (logBox.TextLength > 200000)
            {
                logBox.Text = logBox.Text.Substring(logBox.TextLength - 150000);
            }

            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
        }

        private void SetStatus(string text, Color color)
        {
            if (statusLabel == null)
            {
                return;
            }

            statusLabel.Text = text;
            statusLabel.BackColor = color;
        }

        private void UpdateUiState()
        {
            bool busy = playing || recording || pickMode;

            settingsPanel.Enabled = !busy;
            projectCombo.Enabled = !busy;
            btnNewProject.Enabled = !busy;
            btnSaveProject.Enabled = !busy;
            btnDeleteProject.Enabled = !busy;
            stepButtonsPanel.Enabled = !busy;
            propertiesPanel.Enabled = !busy;
            stepList.Enabled = !busy;

            btnRecord.Enabled = !playing && !pickMode;
            btnPlay.Enabled = !playing && !recording && !pickMode;
            btnPause.Enabled = playing;
            btnStop.Enabled = playing || recording || pickMode;

            btnRecord.Text = recording ? "■  停止录制 (F9)" : "●  录制 (F9)";
            btnPlay.Text = playing ? "▶  播放中 (F10)" : "▶  播放 (F10)";
            btnPause.Text = paused ? "继续 (F11)" : "暂停 (F11)";

            if (recording)
            {
                SetStatus("● 正在录制 · 在目标位置点击鼠标，按 F9 停止", Color.FromArgb(127, 29, 29));
            }
            else if (pickMode)
            {
                SetStatus("◎ 正在拾取坐标 · 按住右 Alt + 左键点击目标，Esc 取消", Color.FromArgb(30, 58, 138));
            }
            else if (playing)
            {
                UpdatePlaybackStatus();
            }
            else if (paused)
            {
                SetStatus("⏸ 已暂停 · 按 F11 继续", Color.FromArgb(120, 53, 15));
            }
            else
            {
                SetStatus("就绪 · 项目：" + project.Name + " · F9 录制 / F10 播放 / F11 暂停", HeaderBg);
            }

            UpdateTitle();
        }

        private void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (playing)
            {
                StopPlayback("窗口关闭");
            }

            if (recording)
            {
                StopRecording();
            }

            if (pickMode)
            {
                CancelPick();
            }

            if (!ConfirmDiscardChanges())
            {
                e.Cancel = true;
                return;
            }

            if (hotkeysRegistered)
            {
                Native.UnregisterHotKey(Handle, 1);
                Native.UnregisterHotKey(Handle, 2);
                Native.UnregisterHotKey(Handle, 3);
                hotkeysRegistered = false;
            }

            if (mouseHook != null)
            {
                mouseHook.Dispose();
                mouseHook = null;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            if (!hotkeysRegistered)
            {
                bool f9 = Native.RegisterHotKey(Handle, 1, 0x4000, 0x78);
                bool f10 = Native.RegisterHotKey(Handle, 2, 0x4000, 0x79);
                bool f11 = Native.RegisterHotKey(Handle, 3, 0x4000, 0x7A);
                hotkeysRegistered = true;

                if (!f9)
                {
                    Log("F9 全局热键注册失败，请使用界面按钮。");
                }

                if (!f10)
                {
                    Log("F10 全局热键注册失败，请使用界面按钮。");
                }

                if (!f11)
                {
                    Log("F11 全局热键注册失败，请使用界面按钮。");
                }
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                BeginInvoke(new Action(delegate { HotKeyPressed(id); }));
                return;
            }

            base.WndProc(ref m);
        }

        private void HotKeyPressed(int id)
        {
            if (id == 1)
            {
                ToggleRecording();
            }
            else if (id == 2)
            {
                TogglePlayback();
            }
            else if (id == 3)
            {
                TogglePause();
            }
        }

        private void AddClickClicked(object sender, EventArgs e)
        {
            if (playing || recording || pickMode)
            {
                return;
            }

            Point cursor = Cursor.Position;
            Step step = new Step();
            step.Type = "click";
            step.X = cursor.X;
            step.Y = cursor.Y;
            step.Button = "Left";
            step.MoveMs = 0;
            step.HoldMs = (int)defaultHoldNum.Value;
            AddStep(step);
        }

        private void AddMoveClicked(object sender, EventArgs e)
        {
            if (playing || recording || pickMode)
            {
                return;
            }

            Point cursor = Cursor.Position;
            Step step = new Step();
            step.Type = "move";
            step.X = cursor.X;
            step.Y = cursor.Y;
            step.MoveMs = 300;
            AddStep(step);
        }

        private void AddWaitClicked(object sender, EventArgs e)
        {
            if (playing || recording || pickMode)
            {
                return;
            }

            Step step = new Step();
            step.Type = "wait";
            step.Seconds = 1.0;
            AddStep(step);
        }

        private void AddStep(Step step)
        {
            if (project == null)
            {
                return;
            }

            project.Steps.Add(step);
            MarkDirty();
            RefreshStepList(project.Steps.Count - 1);
            Log("已添加" + step.TypeName + "步骤。");
        }

        private void RefreshStepList(int selectIndex)
        {
            if (stepList == null)
            {
                return;
            }

            refreshingSteps = true;
            stepList.BeginUpdate();
            stepList.Items.Clear();

            for (int i = 0; i < project.Steps.Count; i++)
            {
                Step step = project.Steps[i];
                ListViewItem item = new ListViewItem((i + 1).ToString(CultureInfo.InvariantCulture));
                item.SubItems.Add(step.TypeName);
                item.SubItems.Add(step.LocationText);
                item.SubItems.Add(step.ButtonName);
                item.SubItems.Add(step.DetailText);
                item.Tag = step;
                stepList.Items.Add(item);
            }

            stepList.EndUpdate();
            stepCountLabel.Text = project.Steps.Count.ToString(CultureInfo.InvariantCulture) + " 步";
            refreshingSteps = false;

            if (selectIndex >= 0 && selectIndex < stepList.Items.Count)
            {
                stepList.Items[selectIndex].Selected = true;
                stepList.Items[selectIndex].Focused = true;
                stepList.EnsureVisible(selectIndex);
                ShowPropertiesFor(project.Steps[selectIndex]);
            }
            else
            {
                stepList.SelectedItems.Clear();
                ShowPropertiesFor(null);
            }
        }

        private void StepSelectionChanged(object sender, EventArgs e)
        {
            if (refreshingSteps)
            {
                return;
            }

            ShowPropertiesFor(CurrentStep());
        }

        private Step CurrentStep()
        {
            if (stepList == null || stepList.SelectedIndices.Count == 0)
            {
                return null;
            }

            int index = stepList.SelectedIndices[0];

            if (index < 0 || index >= project.Steps.Count)
            {
                return null;
            }

            return project.Steps[index];
        }

        private int CurrentStepIndex()
        {
            if (stepList == null || stepList.SelectedIndices.Count == 0)
            {
                return -1;
            }

            return stepList.SelectedIndices[0];
        }

        private void ShowPropertiesFor(Step step)
        {
            if (step == null)
            {
                selectedLabel.Text = "未选择步骤";
                typeLabel.Text = "—";
                rowXY.Visible = false;
                rowButton.Visible = false;
                rowMove.Visible = false;
                rowHold.Visible = false;
                rowWait.Visible = false;
                btnPick.Enabled = false;
                btnApply.Enabled = false;
                btnTest.Enabled = false;
                return;
            }

            int index = project.Steps.IndexOf(step);
            selectedLabel.Text = "第 " + (index + 1).ToString(CultureInfo.InvariantCulture) + " 步 / 共 " +
                project.Steps.Count.ToString(CultureInfo.InvariantCulture) + " 步";
            typeLabel.Text = step.TypeName;

            rowXY.Visible = step.Type != "wait";
            rowButton.Visible = step.Type == "click";
            rowMove.Visible = step.Type != "wait";
            rowHold.Visible = step.Type == "click";
            rowWait.Visible = step.Type == "wait";
            btnPick.Enabled = step.Type != "wait";
            btnApply.Enabled = true;
            btnTest.Enabled = true;

            xNum.Value = ClampDecimal(step.X, xNum.Minimum, xNum.Maximum);
            yNum.Value = ClampDecimal(step.Y, yNum.Minimum, yNum.Maximum);
            moveNum.Value = ClampDecimal(step.MoveMs, moveNum.Minimum, moveNum.Maximum);
            holdNum.Value = ClampDecimal(step.HoldMs, holdNum.Minimum, holdNum.Maximum);
            waitNum.Value = ClampDecimal((decimal)step.Seconds, waitNum.Minimum, waitNum.Maximum);

            if (step.Button == "Right")
            {
                buttonCombo.SelectedIndex = 1;
            }
            else if (step.Button == "Middle")
            {
                buttonCombo.SelectedIndex = 2;
            }
            else
            {
                buttonCombo.SelectedIndex = 0;
            }
        }

        private void ApplyClicked(object sender, EventArgs e)
        {
            ApplyProperties();
        }

        private void ApplyProperties()
        {
            Step step = CurrentStep();

            if (step == null)
            {
                return;
            }

            if (step.Type == "wait")
            {
                step.Seconds = (double)waitNum.Value;
            }
            else
            {
                step.X = (int)xNum.Value;
                step.Y = (int)yNum.Value;
                step.MoveMs = (int)moveNum.Value;

                if (step.Type == "click")
                {
                    step.Button = ButtonValueFromText(buttonCombo.Text);
                    step.HoldMs = (int)holdNum.Value;
                }
            }

            int index = CurrentStepIndex();
            MarkDirty();
            RefreshStepList(index);
            Log("已修改第 " + (index + 1).ToString(CultureInfo.InvariantCulture) + " 步。");
        }

        private static string ButtonValueFromText(string text)
        {
            if (text == "右键")
            {
                return "Right";
            }

            if (text == "中键")
            {
                return "Middle";
            }

            return "Left";
        }

        private static string ButtonTextFromValue(string value)
        {
            if (value == "Right")
            {
                return "右键";
            }

            if (value == "Middle")
            {
                return "中键";
            }

            return "左键";
        }

        private void TestStepClicked(object sender, EventArgs e)
        {
            Step step = CurrentStep();

            if (step == null)
            {
                return;
            }

            if (step.Type == "wait")
            {
                Log("测试等待步骤：" + step.Seconds.ToString("0.0", CultureInfo.InvariantCulture) + " 秒（未实际等待）");
                return;
            }

            Point saved = Cursor.Position;

            try
            {
                Cursor.Position = new Point(step.X, step.Y);

                if (step.Type == "click")
                {
                    Native.Click(step.Button, step.HoldMs);
                    Log("测试点击：" + step.ButtonName + " (" + step.X.ToString(CultureInfo.InvariantCulture) + ", " +
                        step.Y.ToString(CultureInfo.InvariantCulture) + ")");
                }
                else
                {
                    Log("测试移动：(" + step.X.ToString(CultureInfo.InvariantCulture) + ", " +
                        step.Y.ToString(CultureInfo.InvariantCulture) + ")");
                }
            }
            finally
            {
                if (project.RestoreCursorAfterClick && step.Type == "click")
                {
                    Cursor.Position = saved;
                }
            }
        }

        private void DuplicateClicked(object sender, EventArgs e)
        {
            int index = CurrentStepIndex();

            if (index < 0 || playing || recording || pickMode)
            {
                return;
            }

            Step copy = project.Steps[index].Clone();
            project.Steps.Insert(index + 1, copy);
            MarkDirty();
            RefreshStepList(index + 1);
            Log("已复制步骤。");
        }

        private void DeleteStepClicked(object sender, EventArgs e)
        {
            int index = CurrentStepIndex();

            if (index < 0 || playing || recording || pickMode)
            {
                return;
            }

            project.Steps.RemoveAt(index);
            MarkDirty();

            int next = index;

            if (next >= project.Steps.Count)
            {
                next = project.Steps.Count - 1;
            }

            RefreshStepList(next);
            Log("已删除步骤。");
        }

        private void MoveUpClicked(object sender, EventArgs e)
        {
            int index = CurrentStepIndex();

            if (index <= 0 || playing || recording || pickMode)
            {
                return;
            }

            Step step = project.Steps[index];
            project.Steps.RemoveAt(index);
            project.Steps.Insert(index - 1, step);
            MarkDirty();
            RefreshStepList(index - 1);
        }

        private void MoveDownClicked(object sender, EventArgs e)
        {
            int index = CurrentStepIndex();

            if (index < 0 || index >= project.Steps.Count - 1 || playing || recording || pickMode)
            {
                return;
            }

            Step step = project.Steps[index];
            project.Steps.RemoveAt(index);
            project.Steps.Insert(index + 1, step);
            MarkDirty();
            RefreshStepList(index + 1);
        }

        private void StepListDoubleClick(object sender, EventArgs e)
        {
            Step step = CurrentStep();

            if (step == null)
            {
                return;
            }

            if (step.Type == "wait")
            {
                waitNum.Focus();
            }
            else
            {
                xNum.Focus();
                xNum.Select(0, xNum.Text.Length);
            }
        }

        private void StepListKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                DeleteStepClicked(sender, EventArgs.Empty);
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.D)
            {
                DuplicateClicked(sender, EventArgs.Empty);
                e.Handled = true;
            }
        }

        private void RecordClicked(object sender, EventArgs e)
        {
            ToggleRecording();
        }

        private void ToggleRecording()
        {
            if (pickMode)
            {
                return;
            }

            if (playing)
            {
                StopPlayback("开始录制");
            }

            if (recording)
            {
                StopRecording();
            }
            else
            {
                StartRecording();
            }
        }

        private void StartRecording()
        {
            if (project == null)
            {
                return;
            }

            if (clearOnRecordCheck.Checked && project.Steps.Count > 0)
            {
                DialogResult result = MessageBox.Show(
                    this,
                    "开始录制会清空当前项目的步骤，继续吗？",
                    "Mouse Studio",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result != DialogResult.Yes)
                {
                    return;
                }

                project.Steps.Clear();
                RefreshStepList(-1);
                MarkDirty();
            }

            recording = true;
            lastRecordedClick = DateTime.MinValue;

            if (!EnsureHook())
            {
                recording = false;
                return;
            }

            UpdateUiState();
            Log("开始录制：点击鼠标左键/右键/中键会把位置加入步骤。按 F9 停止。");
            System.Media.SystemSounds.Asterisk.Play();
        }

        private void StopRecording()
        {
            recording = false;
            UninstallHookIfIdle();
            UpdateUiState();
            Log("停止录制。");
            System.Media.SystemSounds.Asterisk.Play();
        }

        private bool EnsureHook()
        {
            if (mouseHook == null)
            {
                mouseHook = new MouseHook();
                mouseHook.MouseDown += HookMouseDown;
            }

            if (!mouseHook.Install())
            {
                MessageBox.Show(this, "无法安装鼠标监听，请尝试以管理员身份运行。", "Mouse Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
                mouseHook.Dispose();
                mouseHook = null;
                return false;
            }

            return true;
        }

        private void UninstallHookIfIdle()
        {
            if (mouseHook != null && !recording && !pickMode)
            {
                mouseHook.Dispose();
                mouseHook = null;
            }
        }

        private void HookMouseDown(int button, int x, int y)
        {
            if (pickMode)
            {
                if (button == 1 && Native.IsKeyDown(Native.VK_RMENU))
                {
                    BeginInvoke(new Action(delegate { CompletePick(x, y); }));
                }

                return;
            }

            if (!recording)
            {
                return;
            }

            if (Visible && Bounds.Contains(new Point(x, y)))
            {
                return;
            }

            BeginInvoke(new Action(delegate { RecordClick(button, x, y); }));
        }

        private void RecordClick(int button, int x, int y)
        {
            if (!recording || project == null)
            {
                return;
            }

            DateTime now = DateTime.Now;

            if (lastRecordedClick != DateTime.MinValue)
            {
                double gap = (now - lastRecordedClick).TotalSeconds;

                if (gap >= 0.2)
                {
                    Step wait = new Step();
                    wait.Type = "wait";
                    wait.Seconds = Math.Round(gap, 1);
                    project.Steps.Add(wait);
                }
            }

            Step click = new Step();
            click.Type = "click";
            click.X = x;
            click.Y = y;
            click.Button = ButtonFromInt(button);
            click.MoveMs = 0;
            click.HoldMs = (int)defaultHoldNum.Value;
            project.Steps.Add(click);

            lastRecordedClick = now;
            MarkDirty();
            RefreshStepList(project.Steps.Count - 1);
            Log("录制：" + click.ButtonName + " (" + x.ToString(CultureInfo.InvariantCulture) + ", " +
                y.ToString(CultureInfo.InvariantCulture) + ")");
        }

        private static string ButtonFromInt(int button)
        {
            if (button == 2)
            {
                return "Right";
            }

            if (button == 3)
            {
                return "Middle";
            }

            return "Left";
        }

        private void PickClicked(object sender, EventArgs e)
        {
            StartPick();
        }

        private void StartPick()
        {
            if (playing || recording || pickMode)
            {
                return;
            }

            Step step = CurrentStep();

            if (step == null)
            {
                MessageBox.Show(this, "请先在步骤列表中选择一个点击或移动步骤。", "Mouse Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (step.Type == "wait")
            {
                MessageBox.Show(this, "等待步骤没有坐标。", "Mouse Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            pickStep = step;
            pickMode = true;
            pickStarted = DateTime.Now;
            pickOverlay = new OverlayForm("按住右 Alt + 左键点击目标位置；按 Esc 取消");
            pickOverlay.Show();
            Hide();

            if (!EnsureHook())
            {
                CancelPick();
                return;
            }

            pickTimer.Start();
            UpdateUiState();
            Log("坐标拾取：按住右 Alt + 左键点击目标，Esc 取消。");
        }

        private void PickTimerTick(object sender, EventArgs e)
        {
            if (!pickMode)
            {
                pickTimer.Stop();
                return;
            }

            if (Native.IsKeyDown(Native.VK_ESCAPE))
            {
                CancelPick();
                return;
            }

            if ((DateTime.Now - pickStarted).TotalSeconds > 120)
            {
                CancelPick();
            }
        }

        private void CompletePick(int x, int y)
        {
            if (!pickMode)
            {
                return;
            }

            Step step = pickStep;
            StopPick();

            if (step == null)
            {
                return;
            }

            step.X = x;
            step.Y = y;
            int index = project.Steps.IndexOf(step);
            MarkDirty();
            RefreshStepList(index);
            Log("已拾取坐标：(" + x.ToString(CultureInfo.InvariantCulture) + ", " + y.ToString(CultureInfo.InvariantCulture) + ")");
        }

        private void CancelPick()
        {
            if (!pickMode)
            {
                return;
            }

            StopPick();
            Log("已取消坐标拾取。");
        }

        private void StopPick()
        {
            pickMode = false;
            pickTimer.Stop();

            if (pickOverlay != null)
            {
                pickOverlay.Close();
                pickOverlay.Dispose();
                pickOverlay = null;
            }

            UninstallHookIfIdle();
            Show();
            Activate();
            BringToFront();
            UpdateUiState();
        }

        private void PlayClicked(object sender, EventArgs e)
        {
            TogglePlayback();
        }

        private void TogglePlayback()
        {
            if (pickMode)
            {
                return;
            }

            if (recording)
            {
                StopRecording();
            }

            if (playing)
            {
                StopPlayback("用户停止");
            }
            else
            {
                StartPlayback();
            }
        }

        private void StartPlayback()
        {
            if (project == null)
            {
                return;
            }

            ReadSettingsIntoProject();

            if (project.Steps.Count == 0)
            {
                MessageBox.Show(this, "当前项目还没有步骤。先录制或添加步骤。", "Mouse Studio", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            playbackSteps = new List<Step>();

            foreach (Step step in project.Steps)
            {
                playbackSteps.Add(step.Clone());
            }

            playbackInitialCursor = Cursor.Position;
            playing = true;
            paused = false;
            moving = false;
            inLoopWait = false;
            playbackStepIndex = 0;
            playbackLoopIndex = 1;
            waitUntil = DateTime.Now.AddSeconds(project.StartDelaySeconds);
            playTimer.Start();
            UpdateUiState();
            HighlightStep(-1);
            Log("开始播放：共 " + playbackSteps.Count.ToString(CultureInfo.InvariantCulture) + " 步，循环 " +
                (project.LoopCount == 0 ? "∞" : project.LoopCount.ToString(CultureInfo.InvariantCulture)) +
                " 次。F10 停止，F11 暂停。");
        }

        private void StopPlayback(string reason)
        {
            if (!playing)
            {
                return;
            }

            playing = false;
            paused = false;
            moving = false;
            playTimer.Stop();

            try
            {
                Cursor.Position = playbackInitialCursor;
            }
            catch
            {
            }

            HighlightStep(-1);
            UpdateUiState();
            Log("停止播放：" + reason);
        }

        private void PlayTimerTick(object sender, EventArgs e)
        {
            if (!playing)
            {
                playTimer.Stop();
                return;
            }

            if (paused)
            {
                return;
            }

            DateTime now = DateTime.Now;

            if (moving)
            {
                UpdateMovement(now);
                return;
            }

            if (now < waitUntil)
            {
                return;
            }

            if (inLoopWait)
            {
                inLoopWait = false;

                if (project.LoopCount > 0 && playbackLoopIndex >= project.LoopCount)
                {
                    StopPlayback("全部循环完成");
                    return;
                }

                playbackLoopIndex++;
                playbackStepIndex = 0;
                Log("开始第 " + playbackLoopIndex.ToString(CultureInfo.InvariantCulture) + " 轮。");
            }

            if (playbackStepIndex >= playbackSteps.Count)
            {
                if (project.LoopCount > 0 && playbackLoopIndex >= project.LoopCount)
                {
                    StopPlayback("全部循环完成");
                    return;
                }

                inLoopWait = true;
                waitUntil = now.AddSeconds(project.LoopIntervalSeconds);
                Log("本轮完成，等待 " + project.LoopIntervalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " 秒。");
                UpdatePlaybackStatus();
                return;
            }

            Step step = playbackSteps[playbackStepIndex];

            if (step.Type == "wait")
            {
                if (step.Seconds > 0)
                {
                    waitUntil = now.AddSeconds(step.Seconds);
                }

                playbackStepIndex++;
                UpdatePlaybackStatus();
                return;
            }

            StartMovement(step, now);
        }

        private void StartMovement(Step step, DateTime now)
        {
            Point from = Cursor.Position;
            Point to = new Point(step.X, step.Y);

            if (step.Type == "click")
            {
                clickReturnPoint = from;
            }

            if (step.MoveMs <= 0 || (from.X == to.X && from.Y == to.Y))
            {
                Cursor.Position = to;
                FinishStep(step);
                return;
            }

            moveStep = step;
            moveFrom = from;
            moveTo = to;
            moveStart = now;
            moveDuration = step.MoveMs;
            moving = true;
            HighlightStep(playbackStepIndex);
            UpdatePlaybackStatus();
        }

        private void UpdateMovement(DateTime now)
        {
            double elapsed = (now - moveStart).TotalMilliseconds;
            double t = moveDuration <= 0 ? 1.0 : elapsed / moveDuration;

            if (t >= 1.0)
            {
                Cursor.Position = moveTo;
                moving = false;

                if (moveStep != null)
                {
                    FinishStep(moveStep);
                }

                return;
            }

            if (t < 0)
            {
                t = 0;
            }

            int x = moveFrom.X + (int)Math.Round((moveTo.X - moveFrom.X) * t);
            int y = moveFrom.Y + (int)Math.Round((moveTo.Y - moveFrom.Y) * t);
            Cursor.Position = new Point(x, y);
        }

        private void FinishStep(Step step)
        {
            if (step.Type == "click")
            {
                // Give Windows a moment to apply the cursor position before
                // injecting the click, otherwise the click can land at the
                // previous cursor position.
                System.Threading.Thread.Sleep(150);
                Native.Click(step.Button, step.HoldMs);

                if (project.RestoreCursorAfterClick)
                {
                    Cursor.Position = clickReturnPoint;
                }

                Log("点击：" + step.ButtonName + " (" + step.X.ToString(CultureInfo.InvariantCulture) + ", " +
                    step.Y.ToString(CultureInfo.InvariantCulture) + ")");
            }
            else
            {
                Log("移动：(" + step.X.ToString(CultureInfo.InvariantCulture) + ", " +
                    step.Y.ToString(CultureInfo.InvariantCulture) + ")");
            }

            moving = false;
            playbackStepIndex++;
            HighlightStep(-1);
            UpdatePlaybackStatus();
        }

        private void TogglePause()
        {
            if (!playing)
            {
                return;
            }

            if (paused)
            {
                TimeSpan delta = DateTime.Now - pauseStarted;
                waitUntil = waitUntil.Add(delta);

                if (moving)
                {
                    moveStart = moveStart.Add(delta);
                }

                paused = false;
            }
            else
            {
                paused = true;
                pauseStarted = DateTime.Now;
            }

            UpdateUiState();
        }

        private void PauseClicked(object sender, EventArgs e)
        {
            TogglePause();
        }

        private void StopClicked(object sender, EventArgs e)
        {
            if (pickMode)
            {
                CancelPick();
            }
            else if (recording)
            {
                StopRecording();
            }
            else if (playing)
            {
                StopPlayback("用户停止");
            }
        }

        private void UpdatePlaybackStatus()
        {
            if (!playing)
            {
                return;
            }

            int shownIndex = playbackStepIndex + 1;

            if (shownIndex > playbackSteps.Count)
            {
                shownIndex = playbackSteps.Count;
            }

            if (shownIndex < 1)
            {
                shownIndex = 1;
            }

            string loopText = project.LoopCount == 0 ? "∞" : project.LoopCount.ToString(CultureInfo.InvariantCulture);
            string text = "▶ 播放中 · 第 " + shownIndex.ToString(CultureInfo.InvariantCulture) + "/" +
                playbackSteps.Count.ToString(CultureInfo.InvariantCulture) + " 步 · 循环 " +
                playbackLoopIndex.ToString(CultureInfo.InvariantCulture) + "/" + loopText;

            if (paused)
            {
                text = "⏸ 已暂停 · 第 " + shownIndex.ToString(CultureInfo.InvariantCulture) + "/" +
                    playbackSteps.Count.ToString(CultureInfo.InvariantCulture) + " 步";
            }

            SetStatus(text, Color.FromArgb(20, 83, 45));
        }

        private void HighlightStep(int index)
        {
            if (stepList == null)
            {
                return;
            }

            for (int i = 0; i < stepList.Items.Count; i++)
            {
                stepList.Items[i].BackColor = Color.White;
                stepList.Items[i].ForeColor = TextDark;
            }

            if (index >= 0 && index < stepList.Items.Count)
            {
                stepList.Items[index].BackColor = Selection;
                stepList.Items[index].ForeColor = TextDark;
                stepList.EnsureVisible(index);
            }
        }
    }
}
