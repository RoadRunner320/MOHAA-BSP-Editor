using System.Text;
using System.Text.RegularExpressions;

namespace MohaaBspEditor;

/// <summary>
/// Тип действия: шаблон строки скрипта + названия параметров.
/// Все шаблоны взяты из реального скрипта карты t1l1.scr (кроме wait/thread/waitthread —
/// это стандартные команды скриптового языка MOHAA).
/// </summary>
public sealed class ActionType
{
    public string Id { get; }
    public string Template { get; }
    public int ParamCount { get; }
    private Regex _pattern;

    public ActionType(string id, string template, int paramCount = 0)
    {
        Id = id;
        Template = template;
        ParamCount = paramCount;
    }

    /// <summary>Название действия на текущем языке.</summary>
    public string Name => Loc.T("act." + Id);

    /// <summary>Подсказка к i-му параметру на текущем языке.</summary>
    public string ParamName(int i) => Loc.T($"act.{Id}.p{i}");

    public override string ToString() => Name;

    /// <summary>Пробует распознать строку скрипта этим типом. Возвращает аргументы или null.</summary>
    public string[] Match(string trimmedLine)
    {
        if (ParamCount == 0)
            return string.Equals(trimmedLine, Template, StringComparison.OrdinalIgnoreCase)
                ? Array.Empty<string>() : null;

        _pattern ??= new Regex(
            "^" + Regex.Replace(Regex.Escape(Template), @"\\\{(\d)\}", "(.*?)") + "$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var m = _pattern.Match(trimmedLine);
        if (!m.Success) return null;

        var args = new string[ParamCount];
        for (int i = 0; i < args.Length; i++)
            args[i] = m.Groups[i + 1].Value;
        return args;
    }
}

public sealed class ScriptAction
{
    public ActionType Type { get; }
    public string[] Args { get; }

    public ScriptAction(ActionType type, string[] args = null)
    {
        Type = type;
        Args = new string[type.ParamCount];
        for (int i = 0; i < Args.Length; i++)
            Args[i] = args != null && i < args.Length ? args[i] : "";
    }

    public string ToLine() =>
        Type.ParamCount == 0
            ? Type.Template
            : string.Format(Type.Template, Args.Cast<object>().ToArray());

    public override string ToString() => ToLine();
}

public sealed class ScriptThread
{
    public string Name { get; set; }
    public List<ScriptAction> Actions { get; } = new();
    public ScriptThread(string name) { Name = name; }
    public override string ToString() => Name + ":";
}

public sealed class ScriptProject
{
    public static readonly ActionType Raw = new("raw", "{0}", 1);
    public static readonly ActionType Comment = new("comment", "// {0}", 1);

    public static readonly ActionType[] Catalog =
    {
        new("prespawn", "level waittill prespawn"),
        new("spawn",    "level waittill spawn"),
        new("exec",     "exec {0}", 1),

        new("item",      "$player item {0}", 1),
        new("ammo",      "$player ammo {0} {1}", 2),
        new("use",       "$player use \"{0}\"", 1),
        new("takeall",   "$player takeall"),
        new("fullheal",  "$player fullheal"),
        new("playsound", "$player playsound {0}", 1),
        new("stufftext", "$player stufftext \"{0}\"", 1),
        new("iprintnoloc", "iprintln_noloc \"{0}\"", 1),

        new("objadd", "waitthread global/objectives.scr::add_objectives {0} {1} \"{2}\" ${3}.origin", 4),
        new("objcur", "waitthread global/objectives.scr::current_objectives {0}", 1),

        new("setthread",  "${0} setthread {1}", 2),
        new("thread",     "thread {0}", 1),
        new("waitthread", "waitthread {0}", 1),
        new("wait",       "wait {0}", 1),
        new("selfremove", "self remove"),
        new("remove",     "${0} remove", 1),

        new("objexec",   "{0} exec {1}", 2),
        new("objthread", "{0} thread {1}", 2),
        new("maxdist",   "{0} maxdist {1}", 2),
        new("mindist",   "{0} mindist {1}", 2),

        new("println",  "println {0}", 1),
        new("iprintln", "iprintln \"{0}\"", 1),

        new("if",    "if ({0})", 1),
        new("else",  "else"),
        new("for",   "for ({0})", 1),
        new("open",  "{"),
        new("close", "}"),
        new("goto",  "goto {0}", 1),

        new("assign", "{0} = {1}", 2),
    };

    public string Header { get; set; } = "";
    public List<ScriptThread> Threads { get; } = new();

    // ------------------------------------------------------------ генерация

    public string ToScript()
    {
        var sb = new StringBuilder();

        string header = (Header ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n").TrimEnd();
        if (header.Length > 0)
            sb.Append(header).Append("\r\n\r\n");

        foreach (var t in Threads)
        {
            sb.Append(t.Name).Append(":\r\n");
            foreach (var a in t.Actions)
            {
                string line = a.ToLine();
                bool keepAsIs = a.Type == Raw && line.Length > 0 && char.IsWhiteSpace(line[0]);
                sb.Append(keepAsIs ? "" : "\t").Append(line).Append("\r\n");
            }
            sb.Append("end\r\n\r\n");
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------ разбор .scr

    private static readonly Regex LabelRx = new(@"^\s*([A-Za-z_]\w*)\s*:\s*$", RegexOptions.Compiled);
    private static readonly Regex EndRx = new(@"^\s*end\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static ScriptProject Parse(string text)
    {
        var proj = new ScriptProject();
        var header = new StringBuilder();
        var pending = new List<ScriptAction>();
        ScriptThread current = null;
        bool seenLabel = false;

        foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');

            if (!seenLabel && !LabelRx.IsMatch(line))
            {
                header.Append(line).Append('\n');
                continue;
            }

            var lm = LabelRx.Match(line);
            if (lm.Success && !line.TrimStart().StartsWith("//"))
            {
                seenLabel = true;
                current = new ScriptThread(lm.Groups[1].Value);
                current.Actions.AddRange(pending);
                pending.Clear();
                proj.Threads.Add(current);
                continue;
            }

            if (EndRx.IsMatch(line)) { current = null; continue; }
            if (line.Trim().Length == 0) continue;

            var action = ParseLine(line);
            if (current != null) current.Actions.Add(action);
            else pending.Add(action);   // комментарии между потоками уйдут в следующий поток
        }

        if (pending.Count > 0 && proj.Threads.Count > 0)
            proj.Threads[^1].Actions.AddRange(pending);

        proj.Header = header.ToString().TrimEnd();
        return proj;
    }

    public static ScriptAction ParseLine(string line)
    {
        string trimmed = line.Trim();

        if (trimmed.StartsWith("//"))
            return new ScriptAction(Comment, new[] { trimmed.Substring(2).Trim() });

        foreach (var type in Catalog)
        {
            var args = type.Match(trimmed);
            if (args != null) return new ScriptAction(type, args);
        }
        return new ScriptAction(Raw, new[] { line.TrimEnd() });
    }
}
