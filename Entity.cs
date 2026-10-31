using System.ComponentModel;
using System.Text;

namespace MohaaBspEditor;

public sealed class EntityProp : INotifyPropertyChanged
{
    private string _key = "";
    private string _value = "";

    public EntityProp() { }

    public EntityProp(string key, string value)
    {
        _key = key ?? "";
        _value = value ?? "";
    }

    public string Key
    {
        get => _key;
        set { if (_key == value) return; _key = value ?? ""; Raise(nameof(Key)); }
    }

    public string Value
    {
        get => _value;
        set { if (_value == value) return; _value = value ?? ""; Raise(nameof(Value)); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class Entity
{
    public BindingList<EntityProp> Props { get; } = new();

    public string Get(string key)
    {
        foreach (var p in Props)
            if (string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase))
                return p.Value;
        return "";
    }

    public string ClassName => Get("classname");

    public Entity Clone()
    {
        var e = new Entity();
        foreach (var p in Props)
            e.Props.Add(new EntityProp(p.Key, p.Value));
        return e;
    }
}

public static class EntityParser
{
    public static List<Entity> Parse(string text)
    {
        var list = new List<Entity>();
        var tz = new Tokenizer(text);

        while (tz.Next(out string tok, out bool quoted))
        {
            if (quoted || tok != "{")
                throw new FormatException(Loc.F("err.expected", tok, list.Count));

            var ent = new Entity();
            while (true)
            {
                if (!tz.Next(out string key, out bool keyQuoted))
                    throw new FormatException(Loc.F("err.eofentity", list.Count));

                if (!keyQuoted && key == "}")
                    break;

                if (!tz.Next(out string value, out _))
                    throw new FormatException(Loc.F("err.novalue", key, list.Count));

                ent.Props.Add(new EntityProp(key, value));
            }
            list.Add(ent);
        }
        return list;
    }

    public static string Serialize(IEnumerable<Entity> entities)
    {
        var sb = new StringBuilder();
        foreach (var e in entities)
        {
            sb.Append("{\n");
            foreach (var p in e.Props)
            {
                if (string.IsNullOrWhiteSpace(p.Key))
                    continue;
                if (p.Key.Contains('"') || p.Value.Contains('"'))
                    throw new InvalidDataException(Loc.F("err.quote", p.Key, p.Value));
                sb.Append('"').Append(p.Key).Append("\" \"").Append(p.Value).Append("\"\n");
            }
            sb.Append("}\n");
        }
        return sb.ToString();
    }

    private sealed class Tokenizer
    {
        private readonly string _s;
        private int _p;

        public Tokenizer(string s) { _s = s ?? ""; }

        public bool Next(out string tok, out bool quoted)
        {
            tok = null;
            quoted = false;

            while (true)
            {
                while (_p < _s.Length && (char.IsWhiteSpace(_s[_p]) || _s[_p] == '\0'))
                    _p++;
                if (_p >= _s.Length)
                    return false;

                // комментарий //
                if (_s[_p] == '/' && _p + 1 < _s.Length && _s[_p + 1] == '/')
                {
                    while (_p < _s.Length && _s[_p] != '\n') _p++;
                    continue;
                }
                break;
            }

            char c = _s[_p];

            if (c == '"')
            {
                int start = ++_p;
                while (_p < _s.Length && _s[_p] != '"') _p++;
                if (_p >= _s.Length)
                    throw new FormatException(Loc.T("err.unclosed"));
                tok = _s.Substring(start, _p - start);
                _p++;
                quoted = true;
                return true;
            }

            if (c == '{' || c == '}')
            {
                tok = c.ToString();
                _p++;
                return true;
            }

            int st = _p;
            while (_p < _s.Length && !char.IsWhiteSpace(_s[_p]) && _s[_p] != '{' && _s[_p] != '}' && _s[_p] != '"')
                _p++;
            tok = _s.Substring(st, _p - st);
            return true;
        }
    }
}
