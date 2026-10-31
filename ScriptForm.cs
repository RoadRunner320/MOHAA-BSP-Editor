using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace MohaaBspEditor;

/// <summary>
/// Visual builder for .scr files: threads (main:, Tanim: ...) -> list of actions -> finished script.
/// Opens existing .scr files and saves in Windows-1251 (like the original maps).
/// </summary>
public sealed class ScriptForm : Form
{
    private ScriptProject _proj = new();
    private ScriptThread _thread;
    private string _path;
    private bool _loading;

    private readonly ListBox _threads = new();
    private readonly ListBox _actions = new();
    private readonly ComboBox _typeBox = new();
    private readonly FlowLayoutPanel _argPanel = new();
    private readonly TextBox _preview = new();
    private readonly TextBox _header = new();

    // localizable controls
    private readonly Label _threadsLabel = new();
    private readonly Button _btnAddThread = new(), _btnRename = new(), _btnDelThread = new();
    private readonly Button _btnUp = new(), _btnDown = new(), _btnDup = new(), _btnDelAction = new(), _btnAddAction = new();
    private readonly TabPage _tabPreview = new(), _tabHeader = new();
    private readonly ToolStripMenuItem _mFile = new(), _miNew = new(), _miOpen = new(), _miSave = new(), _miSaveAs = new();

    public ScriptForm()
    {
        Width = 1200;
        Height = 720;
        StartPosition = FormStartPosition.CenterParent;
        BuildUi();
        NewProject();
        ApplyLanguage();

        Loc.Changed += ApplyLanguage;
        FormClosed += (_, _) => Loc.Changed -= ApplyLanguage;
    }

    // ---------------------------------------------------------------- UI

    private void BuildUi()
    {
        // --- column 1: threads
        _threads.Dock = DockStyle.Fill;
        _threads.SelectedIndexChanged += (_, _) => OnThreadSelected();

        Wire(_btnAddThread, (_, _) => AddThread());
        Wire(_btnRename, (_, _) => RenameThread());
        Wire(_btnDelThread, (_, _) => DeleteThread());

        var thBtns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 34, FlowDirection = FlowDirection.LeftToRight };
        thBtns.Controls.Add(_btnAddThread);
        thBtns.Controls.Add(_btnRename);
        thBtns.Controls.Add(_btnDelThread);

        _threadsLabel.Dock = DockStyle.Top;
        _threadsLabel.Height = 20;

        var colThreads = new Panel { Dock = DockStyle.Fill };
        colThreads.Controls.Add(_threads);
        colThreads.Controls.Add(thBtns);
        colThreads.Controls.Add(_threadsLabel);

        // --- column 2: actions
        _actions.Dock = DockStyle.Fill;
        _actions.HorizontalScrollbar = true;
        _actions.SelectedIndexChanged += (_, _) => OnActionSelected();

        Wire(_btnUp, (_, _) => MoveAction(-1));
        Wire(_btnDown, (_, _) => MoveAction(+1));
        Wire(_btnDup, (_, _) => DuplicateAction());
        Wire(_btnDelAction, (_, _) => DeleteAction());

        var actTop = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34 };
        actTop.Controls.Add(_btnUp);
        actTop.Controls.Add(_btnDown);
        actTop.Controls.Add(_btnDup);
        actTop.Controls.Add(_btnDelAction);

        _argPanel.Dock = DockStyle.Bottom;
        _argPanel.Height = 200;
        _argPanel.FlowDirection = FlowDirection.TopDown;
        _argPanel.WrapContents = false;
        _argPanel.AutoScroll = true;

        _typeBox.Dock = DockStyle.Fill;
        _typeBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _typeBox.DropDownWidth = 420;

        Wire(_btnAddAction, (_, _) => AddAction());
        _btnAddAction.Dock = DockStyle.Right;
        _btnAddAction.Width = 150;

        var addRow = new Panel { Dock = DockStyle.Bottom, Height = 30 };
        addRow.Controls.Add(_typeBox);
        addRow.Controls.Add(_btnAddAction);

        var colActions = new Panel { Dock = DockStyle.Fill };
        colActions.Controls.Add(_actions);
        colActions.Controls.Add(addRow);
        colActions.Controls.Add(_argPanel);
        colActions.Controls.Add(actTop);

        // --- column 3: preview / header
        _preview.Dock = DockStyle.Fill;
        _preview.Multiline = true;
        _preview.ReadOnly = true;
        _preview.ScrollBars = ScrollBars.Both;
        _preview.WordWrap = false;
        _preview.Font = new Font("Consolas", 9.5f);

        _header.Dock = DockStyle.Fill;
        _header.Multiline = true;
        _header.ScrollBars = ScrollBars.Both;
        _header.WordWrap = false;
        _header.Font = new Font("Consolas", 9.5f);
        _header.TextChanged += (_, _) =>
        {
            if (_loading) return;
            _proj.Header = _header.Text;
            RefreshPreview();
        };

        _tabPreview.Controls.Add(_preview);
        _tabHeader.Controls.Add(_header);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(_tabPreview);
        tabs.TabPages.Add(_tabHeader);

        // --- grid
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        grid.Controls.Add(colThreads, 0, 0);
        grid.Controls.Add(colActions, 1, 0);
        grid.Controls.Add(tabs, 2, 0);

        // --- menu
        var menu = new MenuStrip();
        _miNew.ShortcutKeys = Keys.Control | Keys.N;
        _miNew.Click += (_, _) => NewProject();
        _miOpen.ShortcutKeys = Keys.Control | Keys.O;
        _miOpen.Click += (_, _) => OpenScr();
        _miSave.ShortcutKeys = Keys.Control | Keys.S;
        _miSave.Click += (_, _) => Save(false);
        _miSaveAs.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
        _miSaveAs.Click += (_, _) => Save(true);
        _mFile.DropDownItems.Add(_miNew);
        _mFile.DropDownItems.Add(_miOpen);
        _mFile.DropDownItems.Add(_miSave);
        _mFile.DropDownItems.Add(_miSaveAs);
        menu.Items.Add(_mFile);

        Controls.Add(grid);
        Controls.Add(menu);
        MainMenuStrip = menu;
    }

    private static void Wire(Button b, EventHandler onClick)
    {
        b.AutoSize = true;
        b.Height = 28;
        b.Click += onClick;
    }

    /// <summary>Sets every user-visible string for the current language.</summary>
    private void ApplyLanguage()
    {
        _mFile.Text = Loc.T("menu.file");
        _miNew.Text = Loc.T("sf.new");
        _miOpen.Text = Loc.T("sf.open");
        _miSave.Text = Loc.T("menu.save");
        _miSaveAs.Text = Loc.T("menu.saveas");

        _threadsLabel.Text = Loc.T("sf.threads");
        _btnAddThread.Text = Loc.T("sf.addthread");
        _btnRename.Text = Loc.T("sf.rename");
        _btnDelThread.Text = Loc.T("sf.delete");
        _btnUp.Text = Loc.T("sf.up");
        _btnDown.Text = Loc.T("sf.down");
        _btnDup.Text = Loc.T("sf.dup");
        _btnDelAction.Text = Loc.T("sf.delete");
        _btnAddAction.Text = Loc.T("sf.addaction");
        _tabPreview.Text = Loc.T("sf.tab.preview");
        _tabHeader.Text = Loc.T("sf.tab.header");

        // action-type combo: names are localized, so refill it and keep the selection
        int sel = _typeBox.SelectedIndex;
        _typeBox.Items.Clear();
        foreach (var t in ScriptProject.Catalog) _typeBox.Items.Add(t);
        _typeBox.Items.Add(ScriptProject.Comment);
        _typeBox.Items.Add(ScriptProject.Raw);
        _typeBox.SelectedIndex = sel >= 0 && sel < _typeBox.Items.Count ? sel : 0;

        // parameter hints of the selected action
        BuildArgEditors(CurrentAction);
        UpdateTitle();
    }

    // ---------------------------------------------------------------- project

    private void NewProject()
    {
        _proj = new ScriptProject { Header = Loc.T("sf.defaultheader") };
        var main = new ScriptThread("main");
        main.Actions.Add(new ScriptAction(ScriptProject.Catalog[0]));   // level waittill prespawn
        main.Actions.Add(new ScriptAction(ScriptProject.Catalog[1]));   // level waittill spawn
        _proj.Threads.Add(main);
        _path = null;
        ReloadAll();
    }

    private void ReloadAll()
    {
        _loading = true;
        _header.Text = _proj.Header.Replace("\r\n", "\n").Replace("\n", "\r\n");
        _threads.Items.Clear();
        foreach (var t in _proj.Threads) _threads.Items.Add(t);
        _loading = false;

        if (_threads.Items.Count > 0) _threads.SelectedIndex = 0;
        else { _thread = null; _actions.Items.Clear(); _argPanel.Controls.Clear(); }
        RefreshPreview();
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        Text = Loc.T("sf.title") + (_path == null ? "" : " — " + Path.GetFileName(_path));
    }

    private void RefreshPreview()
    {
        _preview.Text = _proj.ToScript();
    }

    // ---------------------------------------------------------------- threads

    private void OnThreadSelected()
    {
        if (_loading) return;
        _thread = _threads.SelectedItem as ScriptThread;
        ReloadActions(-1);
    }

    private void AddThread()
    {
        string name = Prompt(Loc.T("sf.newthread.title"), Loc.T("sf.newthread.label"), "newthread");
        if (string.IsNullOrWhiteSpace(name)) return;
        name = name.Trim().TrimEnd(':');

        var t = new ScriptThread(name);
        _proj.Threads.Add(t);
        _threads.Items.Add(t);
        _threads.SelectedItem = t;
        RefreshPreview();
    }

    private void RenameThread()
    {
        if (_thread == null) return;
        string name = Prompt(Loc.T("sf.rename.title"), Loc.T("sf.rename.label"), _thread.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        _thread.Name = name.Trim().TrimEnd(':');

        int i = _threads.SelectedIndex;
        _loading = true;
        _threads.Items[i] = _thread;
        _loading = false;
        RefreshPreview();
    }

    private void DeleteThread()
    {
        if (_thread == null) return;
        if (MessageBox.Show(Loc.F("sf.delthreadq", _thread.Name), Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        int i = _threads.SelectedIndex;
        _proj.Threads.Remove(_thread);
        _threads.Items.RemoveAt(i);
        _thread = null;
        if (_threads.Items.Count > 0) _threads.SelectedIndex = Math.Min(i, _threads.Items.Count - 1);
        else ReloadActions(-1);
        RefreshPreview();
    }

    // ---------------------------------------------------------------- actions

    private void ReloadActions(int select)
    {
        _loading = true;
        _actions.Items.Clear();
        if (_thread != null)
            foreach (var a in _thread.Actions) _actions.Items.Add(a);
        _loading = false;

        if (select >= 0 && select < _actions.Items.Count)
            _actions.SelectedIndex = select;
        else
            BuildArgEditors(null);
    }

    private ScriptAction CurrentAction => _actions.SelectedItem as ScriptAction;

    private void OnActionSelected()
    {
        if (_loading) return;
        BuildArgEditors(CurrentAction);
    }

    private void BuildArgEditors(ScriptAction action)
    {
        _argPanel.SuspendLayout();
        _argPanel.Controls.Clear();

        if (action != null)
        {
            for (int i = 0; i < action.Type.ParamCount; i++)
            {
                int idx = i;
                var lbl = new Label
                {
                    Text = action.Type.ParamName(i) + ":",
                    AutoSize = true,
                    Margin = new Padding(3, 6, 3, 0)
                };
                var tb = new TextBox { Text = action.Args[i], Width = 440 };
                tb.TextChanged += (_, _) =>
                {
                    action.Args[idx] = tb.Text;
                    int sel = _actions.SelectedIndex;
                    if (sel >= 0)
                    {
                        _loading = true;
                        _actions.Items[sel] = action;   // refresh the row text
                        _loading = false;
                    }
                    RefreshPreview();
                };
                _argPanel.Controls.Add(lbl);
                _argPanel.Controls.Add(tb);
            }
        }
        _argPanel.ResumeLayout();
    }

    private void AddAction()
    {
        if (_thread == null) { MessageBox.Show(Loc.T("sf.needthread")); return; }
        if (_typeBox.SelectedItem is not ActionType type) return;

        var a = new ScriptAction(type);
        int at = _actions.SelectedIndex >= 0 ? _actions.SelectedIndex + 1 : _thread.Actions.Count;
        _thread.Actions.Insert(at, a);
        ReloadActions(at);
        RefreshPreview();
    }

    private void MoveAction(int delta)
    {
        int i = _actions.SelectedIndex;
        if (_thread == null || i < 0) return;
        int j = i + delta;
        if (j < 0 || j >= _thread.Actions.Count) return;

        (_thread.Actions[i], _thread.Actions[j]) = (_thread.Actions[j], _thread.Actions[i]);
        ReloadActions(j);
        RefreshPreview();
    }

    private void DuplicateAction()
    {
        var a = CurrentAction;
        int i = _actions.SelectedIndex;
        if (_thread == null || a == null) return;

        _thread.Actions.Insert(i + 1, new ScriptAction(a.Type, (string[])a.Args.Clone()));
        ReloadActions(i + 1);
        RefreshPreview();
    }

    private void DeleteAction()
    {
        int i = _actions.SelectedIndex;
        if (_thread == null || i < 0) return;
        _thread.Actions.RemoveAt(i);
        ReloadActions(Math.Min(i, _thread.Actions.Count - 1));
        RefreshPreview();
    }

    // ---------------------------------------------------------------- files

    private void OpenScr()
    {
        using var dlg = new OpenFileDialog { Filter = Loc.T("sf.openfilter") };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            string text = Encoding.GetEncoding(1251).GetString(File.ReadAllBytes(dlg.FileName));
            _proj = ScriptProject.Parse(text);
            _path = dlg.FileName;
            ReloadAll();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.F("sf.openerr", ex.Message), Text,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Save(bool saveAs)
    {
        string path = _path;
        if (saveAs || path == null)
        {
            using var dlg = new SaveFileDialog { Filter = Loc.T("sf.savefilter"), FileName = "map.scr" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            path = dlg.FileName;
        }
        try
        {
            if (File.Exists(path) && !File.Exists(path + ".bak"))
                File.Copy(path, path + ".bak");
            File.WriteAllBytes(path, Encoding.GetEncoding(1251).GetBytes(_proj.ToScript()));
            _path = path;
            UpdateTitle();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.F("sf.saveerr", ex.Message), Text,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ---------------------------------------------------------------- input dialog

    private static string Prompt(string title, string label, string value)
    {
        using var f = new Form
        {
            Text = title,
            Width = 420,
            Height = 150,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var lbl = new Label { Text = label, Left = 10, Top = 10, Width = 380 };
        var tb = new TextBox { Text = value, Left = 10, Top = 35, Width = 380 };
        var ok = new Button { Text = Loc.T("dlg.ok"), Left = 230, Top = 70, Width = 75, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = Loc.T("dlg.cancel"), Left = 315, Top = 70, Width = 75, DialogResult = DialogResult.Cancel };
        f.Controls.AddRange(new Control[] { lbl, tb, ok, cancel });
        f.AcceptButton = ok;
        f.CancelButton = cancel;
        return f.ShowDialog() == DialogResult.OK ? tb.Text : null;
    }
}
