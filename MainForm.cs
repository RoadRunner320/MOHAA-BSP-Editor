using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace MohaaBspEditor;

public sealed class MainForm : Form
{
    // Name == null -> "Empty entity" (localized); otherwise classname shown as is.
    private static readonly (string Name, string[] Kv)[] Templates =
    {
        (null,                     new[] { "classname", "" }),
        ("info_player_start",      new[] { "classname", "info_player_start",      "origin", "0 0 0", "angle", "0" }),
        ("info_player_deathmatch", new[] { "classname", "info_player_deathmatch", "origin", "0 0 0", "angle", "0" }),
        ("info_player_allied",     new[] { "classname", "info_player_allied",     "origin", "0 0 0", "angle", "0" }),
        ("info_player_axis",       new[] { "classname", "info_player_axis",       "origin", "0 0 0", "angle", "0" }),
        ("script_origin",          new[] { "classname", "script_origin",          "origin", "0 0 0", "targetname", "" }),
        ("light",                  new[] { "classname", "light",                  "origin", "0 0 0", "light", "300" }),
    };

    private BspFile _bsp;
    private List<Entity> _all = new();
    private readonly List<int> _view = new();   // indices into _all that pass the filter
    private Entity _current;
    private bool _dirty;
    private string _savedText = "";   // entity text as of the last load/save, used to detect real changes

    private readonly ListView _list = new();
    private readonly TextBox _search = new();
    private readonly DataGridView _grid = new();
    private readonly TabControl _tabs = new();
    private readonly TextBox _raw = new();
    private readonly ToolStripStatusLabel _status = new();

    // localizable controls
    private readonly TabPage _tabProps = new();
    private readonly TabPage _tabRaw = new();
    private readonly Button _btnApply = new();
    private readonly ContextMenuStrip _listMenu = new();
    private readonly ToolStripMenuItem _mFile = new(), _miOpen = new(), _miSave = new(), _miSaveAs = new(), _miSaveCompact = new(),
        _miExport = new(), _miImport = new(), _miExit = new();
    private readonly ToolStripMenuItem _mEnt = new(), _mAdd = new(), _miDup = new(), _miDel = new();
    private readonly ToolStripMenuItem _mScript = new(), _miScriptBuilder = new();
    private readonly ToolStripMenuItem _mHelp = new(), _miAbout = new();
    private readonly ToolStripMenuItem _mLang = new(), _miEn = new("English"), _miRu = new("Русский");

    public MainForm()
    {
        Width = 1100;
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;

        BuildUi();
        ApplyLanguage();

        Loc.Changed += ApplyLanguage;
        FormClosed += (_, _) => Loc.Changed -= ApplyLanguage;

        DragEnter += (_, e) =>
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        };
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                if (ConfirmDiscard()) LoadFile(files[0]);
        };
    }

    // ---------------------------------------------------------------- UI

    private void BuildUi()
    {
        // --- entity list (left)
        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.HideSelection = false;
        _list.MultiSelect = true;
        _list.VirtualMode = true;
        _list.GridLines = true;
        _list.Columns.Add("#", 50);
        _list.Columns.Add("classname", 160);
        _list.Columns.Add("targetname", 120);
        _list.Columns.Add("origin", 140);
        _list.RetrieveVirtualItem += OnRetrieveItem;
        _list.SelectedIndexChanged += (_, _) => OnSelectionChanged();
        _list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; }
        };

        _search.Dock = DockStyle.Top;
        _search.TextChanged += (_, _) => ApplyFilter();

        var left = new Panel { Dock = DockStyle.Fill };
        left.Controls.Add(_list);     // Fill first
        left.Controls.Add(_search);   // Top last

        _listMenu.Items.Add(new ToolStripMenuItem("", null, (_, _) => DuplicateSelected()) { Name = "dup" });
        _listMenu.Items.Add(new ToolStripMenuItem("", null, (_, _) => DeleteSelected()) { Name = "del" });
        _list.ContextMenuStrip = _listMenu;

        // --- properties (right, tab 1)
        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = true;
        _grid.AllowUserToAddRows = true;
        _grid.AllowUserToDeleteRows = true;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.RowHeadersWidth = 30;
        _grid.DataError += (_, e) => e.ThrowException = false;
        _grid.DataBindingComplete += (_, _) => SetGridHeaders();
        _tabProps.Controls.Add(_grid);

        // --- raw text (right, tab 2)
        _raw.Dock = DockStyle.Fill;
        _raw.Multiline = true;
        _raw.ScrollBars = ScrollBars.Both;
        _raw.WordWrap = false;
        _raw.Font = new Font("Consolas", 9.5f);
        _raw.AcceptsReturn = true;
        _raw.AcceptsTab = true;

        _btnApply.Dock = DockStyle.Bottom;
        _btnApply.Height = 32;
        _btnApply.Click += (_, _) => ApplyRaw();

        _tabRaw.Controls.Add(_raw);
        _tabRaw.Controls.Add(_btnApply);

        _tabs.Dock = DockStyle.Fill;
        _tabs.TabPages.Add(_tabProps);
        _tabs.TabPages.Add(_tabRaw);
        _tabs.SelectedIndexChanged += (_, _) =>
        {
            if (_tabs.SelectedIndex == 1)
            {
                CommitPendingEdits();
                _raw.Text = SafeSerialize().Replace("\n", "\r\n");
            }
        };

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 500
        };
        split.Panel1.Controls.Add(left);
        split.Panel2.Controls.Add(_tabs);

        // --- menu
        var menu = new MenuStrip();

        _miOpen.ShortcutKeys = Keys.Control | Keys.O;
        _miOpen.Click += (_, _) => OpenDialog();
        _miSave.ShortcutKeys = Keys.Control | Keys.S;
        _miSave.Click += (_, _) => Save();
        _miSaveAs.ShortcutKeys = Keys.Control | Keys.Shift | Keys.S;
        _miSaveAs.Click += (_, _) => SaveAs();
        _miSaveCompact.Click += (_, _) => SaveCompact();
        _miExport.Click += (_, _) => ExportEnt();
        _miImport.Click += (_, _) => ImportEnt();
        _miExit.Click += (_, _) => Close();

        _mFile.DropDownItems.Add(_miOpen);
        _mFile.DropDownItems.Add(_miSave);
        _mFile.DropDownItems.Add(_miSaveAs);
        _mFile.DropDownItems.Add(_miSaveCompact);
        _mFile.DropDownItems.Add(new ToolStripSeparator());
        _mFile.DropDownItems.Add(_miExport);
        _mFile.DropDownItems.Add(_miImport);
        _mFile.DropDownItems.Add(new ToolStripSeparator());
        _mFile.DropDownItems.Add(_miExit);

        _miDup.ShortcutKeys = Keys.Control | Keys.D;
        _miDup.Click += (_, _) => DuplicateSelected();
        _miDel.Click += (_, _) => DeleteSelected();
        _mEnt.DropDownItems.Add(_mAdd);
        _mEnt.DropDownItems.Add(_miDup);
        _mEnt.DropDownItems.Add(_miDel);

        _miScriptBuilder.ShortcutKeys = Keys.Control | Keys.R;
        _miScriptBuilder.Click += (_, _) => new ScriptForm().Show(this);
        _mScript.DropDownItems.Add(_miScriptBuilder);

        _miEn.Click += (_, _) => Loc.Set(Loc.English);
        _miRu.Click += (_, _) => Loc.Set(Loc.Russian);
        _mLang.DropDownItems.Add(_miEn);
        _mLang.DropDownItems.Add(_miRu);

        menu.Items.Add(_mFile);
        menu.Items.Add(_mEnt);
        menu.Items.Add(_mScript);
        menu.Items.Add(_mLang);

        _miAbout.Click += (_, _) => MessageBox.Show(this,
            Loc.F("about.text", Loc.T("app.title"), AppInfo.Version),
            Loc.T("about.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        _mHelp.DropDownItems.Add(_miAbout);
        menu.Items.Add(_mHelp);

        // --- status
        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_status);

        // Order matters: Fill first, then Bottom/Top
        Controls.Add(split);
        Controls.Add(statusStrip);
        Controls.Add(menu);
        MainMenuStrip = menu;
    }

    /// <summary>Sets every user-visible string for the current language.</summary>
    private void ApplyLanguage()
    {
        _mFile.Text = Loc.T("menu.file");
        _miOpen.Text = Loc.T("menu.open");
        _miSave.Text = Loc.T("menu.save");
        _miSaveAs.Text = Loc.T("menu.saveas");
        _miSaveCompact.Text = Loc.T("menu.savecompact");
        _miExport.Text = Loc.T("menu.export");
        _miImport.Text = Loc.T("menu.import");
        _miExit.Text = Loc.T("menu.exit");

        _mEnt.Text = Loc.T("menu.entity");
        _mAdd.Text = Loc.T("menu.add");
        _miDup.Text = Loc.T("menu.dup");
        _miDel.Text = Loc.T("menu.del");

        _mScript.Text = Loc.T("menu.scripts");
        _miScriptBuilder.Text = Loc.T("menu.scriptbuilder");

        _mLang.Text = Loc.T("menu.language");
        _mHelp.Text = Loc.T("menu.help");
        _miAbout.Text = Loc.T("menu.about");
        _miEn.Checked = Loc.Lang == Loc.English;
        _miRu.Checked = Loc.Lang == Loc.Russian;

        _listMenu.Items["dup"].Text = Loc.T("menu.dup");
        _listMenu.Items["del"].Text = Loc.T("ctx.del");

        _search.PlaceholderText = Loc.T("search.placeholder");
        _tabProps.Text = Loc.T("tab.props");
        _tabRaw.Text = Loc.T("tab.raw");
        _btnApply.Text = Loc.T("btn.applytext");

        // "Add" submenu: rebuild so the localized "Empty entity" name is refreshed
        _mAdd.DropDownItems.Clear();
        foreach (var (name, kv) in Templates)
        {
            var captured = kv;
            _mAdd.DropDownItems.Add(name ?? Loc.T("tpl.empty"), null, (_, _) => AddEntity(captured));
        }

        SetGridHeaders();
        UpdateTitle();
        UpdateStatus();
    }

    private void SetGridHeaders()
    {
        if (_grid.Columns.Contains("Key")) _grid.Columns["Key"].HeaderText = Loc.T("grid.key");
        if (_grid.Columns.Contains("Value")) _grid.Columns["Value"].HeaderText = Loc.T("grid.value");
    }

    // ---------------------------------------------------------------- list

    private void OnRetrieveItem(object sender, RetrieveVirtualItemEventArgs e)
    {
        if (e.ItemIndex < 0 || e.ItemIndex >= _view.Count)
        {
            e.Item = new ListViewItem(new[] { "", "", "", "" });
            return;
        }
        int idx = _view[e.ItemIndex];
        var ent = _all[idx];
        e.Item = new ListViewItem(new[]
        {
            idx.ToString(), ent.ClassName, ent.Get("targetname"), ent.Get("origin")
        });
    }

    private void OnSelectionChanged()
    {
        if (_list.SelectedIndices.Count == 1)
        {
            int vi = _list.SelectedIndices[0];
            if (vi >= 0 && vi < _view.Count)
            {
                _current = _all[_view[vi]];
                _grid.DataSource = _current.Props;
                return;
            }
        }
        _current = null;
        _grid.DataSource = null;
    }

    private static bool Matches(Entity e, string q)
    {
        foreach (var p in e.Props)
            if (p.Key.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                p.Value.Contains(q, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private void ApplyFilter(Entity keepSelected = null)
    {
        string q = _search.Text.Trim();

        _current = null;
        _grid.DataSource = null;

        _view.Clear();
        for (int i = 0; i < _all.Count; i++)
            if (q.Length == 0 || Matches(_all[i], q))
                _view.Add(i);

        _list.VirtualListSize = _view.Count;
        _list.Invalidate();

        if (keepSelected != null)
        {
            int vi = _view.IndexOf(_all.IndexOf(keepSelected));
            if (vi >= 0)
            {
                _list.SelectedIndices.Clear();
                _list.SelectedIndices.Add(vi);
                _list.EnsureVisible(vi);
            }
        }
        UpdateStatus();
    }

    // ---------------------------------------------------------------- entities

    private void Track(Entity e)
    {
        e.Props.ListChanged += (_, _) =>
        {
            MarkDirty();
            _list.Invalidate();
        };
    }

    private void SetEntities(List<Entity> list)
    {
        _all = list;
        foreach (var e in _all) Track(e);
        ApplyFilter();
    }

    private void AddEntity(string[] kv)
    {
        if (_bsp == null) { Info(Loc.T("msg.openfirst")); return; }

        var e = new Entity();
        for (int i = 0; i + 1 < kv.Length; i += 2)
            e.Props.Add(new EntityProp(kv[i], kv[i + 1]));
        Track(e);
        _all.Add(e);

        _search.Text = "";          // resets the filter
        ApplyFilter(e);
        MarkDirty();
    }

    private void DuplicateSelected()
    {
        if (_bsp == null || _list.SelectedIndices.Count == 0) return;

        Entity last = null;
        var sources = new List<Entity>();
        foreach (int vi in _list.SelectedIndices)
            sources.Add(_all[_view[vi]]);

        foreach (var src in sources)
        {
            var copy = src.Clone();
            Track(copy);
            _all.Add(copy);
            last = copy;
        }
        _search.Text = "";
        ApplyFilter(last);
        MarkDirty();
    }

    private void DeleteSelected()
    {
        if (_bsp == null || _list.SelectedIndices.Count == 0) return;

        var indices = new List<int>();
        foreach (int vi in _list.SelectedIndices)
            indices.Add(_view[vi]);

        if (MessageBox.Show(Loc.F("msg.deleteq", indices.Count), Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        indices.Sort();
        for (int i = indices.Count - 1; i >= 0; i--)
            _all.RemoveAt(indices[i]);

        ApplyFilter();
        MarkDirty();
    }

    // ---------------------------------------------------------------- raw text

    private bool ApplyRaw()
    {
        if (_bsp == null) return false;
        try
        {
            var list = EntityParser.Parse(_raw.Text.Replace("\r\n", "\n"));
            SetEntities(list);
            MarkDirty();
            return true;
        }
        catch (Exception ex)
        {
            Error(Loc.F("msg.parseerr", ex.Message));
            return false;
        }
    }

    private string SafeSerialize()
    {
        try { return EntityParser.Serialize(_all); }
        catch (Exception ex) { return "// " + ex.Message + "\n"; }
    }

    // ---------------------------------------------------------------- files

    private void OpenDialog()
    {
        if (!ConfirmDiscard()) return;
        using var dlg = new OpenFileDialog { Filter = Loc.T("dlg.bspopen") };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            LoadFile(dlg.FileName);
    }

    public void LoadFile(string path)
    {
        try
        {
            var bsp = BspFile.Load(path);
            var ents = EntityParser.Parse(bsp.EntityText);

            _bsp = bsp;
            _search.Text = "";
            SetEntities(ents);
            _savedText = SafeSerialize();
            _dirty = false;
            UpdateTitle();
            UpdateStatus();
        }
        catch (Exception ex)
        {
            Error(Loc.F("msg.openerr", path, ex.Message));
        }
    }

    private void Save()
    {
        if (_bsp == null) return;
        SaveTo(_bsp.FilePath);
    }

    private void SaveCompact()
    {
        if (_bsp == null) return;
        SaveTo(_bsp.FilePath, true);
    }

    private void SaveAs()
    {
        if (_bsp == null) return;
        using var dlg = new SaveFileDialog
        {
            Filter = Loc.T("dlg.bspsave"),
            FileName = Path.GetFileName(_bsp.FilePath)
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
            SaveTo(dlg.FileName);
    }

    private bool SaveTo(string path, bool compact = false)
    {
        if (_bsp == null) return false;
        CommitPendingEdits();                       // a cell still being edited must be included in the file
        if (_tabs.SelectedIndex == 1 && !ApplyRaw()) return false;

        try
        {
            string text = EntityParser.Serialize(_all);
            _bsp.SetEntities(text);

            long sizeBefore = _bsp.FileSize;
            int removed = compact ? _bsp.Compact() : 0;

            // back up the original once
            if (File.Exists(path) && !File.Exists(path + ".bak"))
                File.Copy(path, path + ".bak");

            _bsp.Save(path);
            _savedText = text;
            _dirty = false;
            UpdateTitle();
            UpdateStatus();

            if (compact)
                Info(removed > 0
                    ? Loc.F("msg.compacted", removed, sizeBefore, _bsp.FileSize)
                    : Loc.T("msg.nostale"));
            return true;
        }
        catch (Exception ex)
        {
            Error(Loc.F("msg.saveerr", ex.Message));
            return false;
        }
    }

    private void ExportEnt()
    {
        if (_bsp == null) return;
        CommitPendingEdits();
        using var dlg = new SaveFileDialog
        {
            Filter = Loc.T("dlg.entsave"),
            FileName = Path.GetFileNameWithoutExtension(_bsp.FilePath) + ".ent"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dlg.FileName, EntityParser.Serialize(_all), Encoding.Latin1);
        }
        catch (Exception ex) { Error(ex.Message); }
    }

    private void ImportEnt()
    {
        if (_bsp == null) { Info(Loc.T("msg.openfirst")); return; }
        using var dlg = new OpenFileDialog { Filter = Loc.T("dlg.entopen") };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        if (MessageBox.Show(Loc.T("msg.importq"), Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        try
        {
            var list = EntityParser.Parse(File.ReadAllText(dlg.FileName, Encoding.Latin1));
            _search.Text = "";
            SetEntities(list);
            MarkDirty();
        }
        catch (Exception ex) { Error(Loc.F("msg.importerr", ex.Message)); }
    }

    // ---------------------------------------------------------------- helpers

    private void MarkDirty()
    {
        if (_dirty) return;
        _dirty = true;
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        string name = _bsp == null ? "" : " — " + Path.GetFileName(_bsp.FilePath);
        Text = Loc.T("app.title") + " v" + AppInfo.Version + name + (_dirty ? " *" : "");
    }

    private void UpdateStatus()
    {
        if (_bsp == null)
        {
            _status.Text = Loc.T("status.hint");
            return;
        }
        _status.Text = Loc.F("status.info",
            _bsp.Ident, _bsp.Version, _bsp.EntityLumpIndex, _all.Count, _view.Count, _bsp.FileSize, _bsp.StaleBytes);
    }

    /// <summary>
    /// Pushes a half-typed cell value from the properties grid into the entity data.
    /// Without this, Save would write the OLD value and the edit would only be applied
    /// later (when the cell loses focus), which re-raised the "unsaved changes" flag.
    /// </summary>
    private void CommitPendingEdits()
    {
        try
        {
            if (_grid.IsCurrentCellInEditMode)
                _grid.EndEdit();
            if (_grid.DataSource != null)
                BindingContext[_grid.DataSource].EndCurrentEdit();
        }
        catch
        {
            // nothing to commit, or the value is invalid; the normal validation will report it
        }
    }

    /// <summary>True when the current entities serialize exactly to what was last loaded/saved.</summary>
    private bool IsUnchangedSinceSave()
    {
        try { return EntityParser.Serialize(_all) == _savedText; }
        catch { return false; }
    }

    private bool ConfirmDiscard()
    {
        CommitPendingEdits();
        if (!_dirty) return true;
        if (_bsp != null && IsUnchangedSinceSave())   // edited and reverted: nothing to save
        {
            _dirty = false;
            UpdateTitle();
            return true;
        }
        var r = MessageBox.Show(Loc.T("msg.unsaved"), Text,
            MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (r == DialogResult.Cancel) return false;
        if (r == DialogResult.Yes) return SaveTo(_bsp.FilePath);
        return true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!ConfirmDiscard()) e.Cancel = true;
        base.OnFormClosing(e);
    }

    private void Error(string msg) => MessageBox.Show(this, msg, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
    private void Info(string msg) => MessageBox.Show(this, msg, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
}
