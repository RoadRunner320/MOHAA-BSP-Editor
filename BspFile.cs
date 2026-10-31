using System.Text;

namespace MohaaBspEditor;

/// <summary>
/// Работа с контейнером BSP от Medal of Honor: Allied Assault (+ Spearhead / Breakthrough).
///
/// Формат заголовка (little-endian):
///   char[4] ident    -> "2015" (MOHAA, Spearhead; v19) / "EALA" (Breakthrough; v21) — проверено на реальных картах
///   int32   version  -> 19 (AA, SH) или 21 (BT)
///   int32   checksum -> (подтверждено на карте m3l2.bsp, v19)
///   lump_t[28]       -> { int32 fileofs; int32 filelen; }   (начало таблицы: байт 12)
///
/// Индекс lump'а с энтити в v19 = 14 (0 = шейдеры).
///
/// Редактор НЕ трогает геометрию: меняется только lump с энтити.
/// Новый текст кладётся на то же место (если помещается) либо в конец файла,
/// после чего в заголовке обновляются offset/length.
/// </summary>
public sealed class BspFile
{
    public const int HeaderLumps = 28;
    public const int DefaultEntityLump = 14;   // LUMP_ENTITIES в MOHAA v19 (проверено на реальной карте)

    // Где начинается таблица lump'ов: 12 (ident+version+checksum), запасной вариант 8.
    private static readonly int[] LumpTableCandidates = { 12, 8 };
    private int _lumpTable = 12;

    private byte[] _data = Array.Empty<byte>();

    public string FilePath { get; private set; } = "";
    public string Ident { get; private set; } = "";
    public int Version { get; private set; }
    public int EntityLumpIndex { get; private set; }
    public string EntityText { get; private set; } = "";
    public long FileSize => _data.Length;

    /// <summary>Размер устаревших копий текста энтити, которые лежат в файле, но не указаны в заголовке.</summary>
    public int StaleBytes { get; private set; }

    public static BspFile Load(string path)
    {
        var bsp = new BspFile
        {
            FilePath = path,
            _data = File.ReadAllBytes(path)
        };
        bsp.ParseHeader();
        return bsp;
    }

    private void ParseHeader()
    {
        if (_data.Length < 12 + HeaderLumps * 8)
            throw new InvalidDataException(Loc.T("err.toosmall"));

        Ident = Encoding.ASCII.GetString(_data, 0, 4);
        // Известные сигнатуры: 2015 (MOHAA и Spearhead, v19), EALA (Breakthrough, v21), EBSP/IBSP (не проверялись).
        // Неизвестную сигнатуру не отвергаем: ниже всё равно проверяются таблица lump'ов и текст энтити.
        Version = BitConverter.ToInt32(_data, 4);

        // подбираем смещение таблицы lump'ов, при котором все записи лежат внутри файла
        bool found = false;
        foreach (int cand in LumpTableCandidates)
        {
            _lumpTable = cand;
            if (LumpTableIsValid()) { found = true; break; }
        }
        if (!found)
            throw new InvalidDataException(Loc.F("err.table", Version));

        EntityLumpIndex = FindEntityLump();
        EntityText = ReadEntityText(EntityLumpIndex);
        StaleBytes = FindStaleEntityCopies().Sum(c => c.Length);
    }

    private bool LumpTableIsValid()
    {
        for (int i = 0; i < HeaderLumps; i++)
        {
            GetLump(i, out int ofs, out int len);
            if (ofs < 0 || len < 0 || (long)ofs + len > _data.Length)
                return false;
        }
        return true;
    }

    private void GetLump(int index, out int ofs, out int len)
    {
        int pos = _lumpTable + index * 8;
        ofs = BitConverter.ToInt32(_data, pos);
        len = BitConverter.ToInt32(_data, pos + 4);
    }

    private void SetLump(int index, int ofs, int len)
    {
        int pos = _lumpTable + index * 8;
        BitConverter.GetBytes(ofs).CopyTo(_data, pos);
        BitConverter.GetBytes(len).CopyTo(_data, pos + 4);
    }

    /// <summary>
    /// Сначала пробуем стандартный индекс (14). Если там не текст энтити
    /// (на старых версиях BSP нумерация лампов может отличаться) — перебираем все.
    /// </summary>
    private int FindEntityLump()
    {
        if (LooksLikeEntities(DefaultEntityLump))
            return DefaultEntityLump;

        for (int i = 0; i < HeaderLumps; i++)
            if (i != DefaultEntityLump && LooksLikeEntities(i))
                return i;

        throw new InvalidDataException(Loc.T("err.noent"));
    }

    private bool LooksLikeEntities(int index)
    {
        GetLump(index, out int ofs, out int len);
        if (len < 2) return false;

        // первый непробельный байт должен быть '{'
        int p = ofs, end = ofs + len;
        while (p < end && (_data[p] == ' ' || _data[p] == '\t' || _data[p] == '\r' || _data[p] == '\n' || _data[p] == 0))
            p++;
        if (p >= end || _data[p] != (byte)'{') return false;

        string text = ReadEntityText(index);
        return text.Contains("\"classname\"", StringComparison.Ordinal);
    }

    private string ReadEntityText(int index)
    {
        GetLump(index, out int ofs, out int len);
        int realLen = len;
        int zero = Array.IndexOf(_data, (byte)0, ofs, len);
        if (zero >= 0) realLen = zero - ofs;
        return Encoding.Latin1.GetString(_data, ofs, realLen);
    }

    /// <summary>Подменяет текст энтити в памяти (на диск пишет Save).</summary>
    public void SetEntities(string text)
    {
        byte[] payload = Encoding.Latin1.GetBytes(text);
        byte[] bytes = new byte[payload.Length + 1];          // + нулевой терминатор
        Array.Copy(payload, bytes, payload.Length);

        GetLump(EntityLumpIndex, out int ofs, out int len);

        if (bytes.Length <= len)
        {
            // помещается на старое место
            Array.Clear(_data, ofs, len);
            Array.Copy(bytes, 0, _data, ofs, bytes.Length);
            SetLump(EntityLumpIndex, ofs, bytes.Length);
        }
        else
        {
            int newOfs;
            if (ofs + len == _data.Length)
            {
                // lump в самом конце файла — просто расширяем его
                newOfs = ofs;
                Array.Resize(ref _data, ofs + bytes.Length);
            }
            else
            {
                // дописываем в конец, выравнивание по 4 байта
                newOfs = (_data.Length + 3) & ~3;
                Array.Resize(ref _data, newOfs + bytes.Length);
            }
            Array.Copy(bytes, 0, _data, newOfs, bytes.Length);
            SetLump(EntityLumpIndex, newOfs, bytes.Length);
        }

        EntityText = text;
        StaleBytes = FindStaleEntityCopies().Sum(c => c.Length);
    }

    // ------------------------------------------------------------ очистка

    /// <summary>
    /// Ищет в «пустотах» между lump'ами старые копии текста энтити (остаются после каждого
    /// сохранения, которое дописывает lump в конец файла). Любые другие данные вне lump'ов
    /// (например, бинарные 80 с лишним байт в конце оригинальных карт) НЕ трогаются:
    /// неизвестно, читает ли их игра.
    /// </summary>
    private List<(int Start, int Length)> FindStaleEntityCopies()
    {
        var result = new List<(int Start, int Length)>();

        var covered = new List<(int A, int B)>();
        for (int i = 0; i < HeaderLumps; i++)
        {
            GetLump(i, out int ofs, out int len);
            if (len > 0) covered.Add((ofs, ofs + len));
        }
        covered.Sort();

        int pos = _lumpTable + HeaderLumps * 8;
        foreach (var (a, b) in covered)
        {
            if (a > pos) ScanGap(pos, a, result);
            if (b > pos) pos = b;
        }
        if (pos < _data.Length) ScanGap(pos, _data.Length, result);

        return result;
    }

    private void ScanGap(int from, int to, List<(int Start, int Length)> result)
    {
        int i = from;
        while (i + 3 < to)
        {
            // текст энтити начинается с "{\n\"" и содержит "classname"
            if (_data[i] == (byte)'{' && _data[i + 1] == (byte)'\n' && _data[i + 2] == (byte)'"')
            {
                int zero = Array.IndexOf(_data, (byte)0, i, to - i);
                int textEnd = zero < 0 ? to : zero;

                if (Encoding.Latin1.GetString(_data, i, textEnd - i).Contains("\"classname\"", StringComparison.Ordinal))
                {
                    int end = zero < 0 ? to : zero + 1;
                    while (end < to && _data[end] == 0) end++;   // нулевое выравнивание после текста

                    int len = (end - i) & ~3;                    // кратно 4, чтобы выравнивание lump'ов не нарушилось
                    if (len > 0)
                    {
                        result.Add((i, len));
                        i += len;
                        continue;
                    }
                }
            }
            i++;
        }
    }

    /// <summary>
    /// Удаляет старые копии энтити из файла и сдвигает смещения lump'ов в заголовке.
    /// Содержимое lump'ов не меняется. Возвращает число освобождённых байт.
    /// </summary>
    public int Compact()
    {
        var stale = FindStaleEntityCopies();
        if (stale.Count == 0) return 0;

        int removed = stale.Sum(c => c.Length);
        var output = new byte[_data.Length - removed];

        int src = 0, dst = 0;
        foreach (var (start, length) in stale)
        {
            int n = start - src;
            Buffer.BlockCopy(_data, src, output, dst, n);
            dst += n;
            src = start + length;
        }
        Buffer.BlockCopy(_data, src, output, dst, _data.Length - src);

        // новые смещения считаем по старой таблице, поэтому читаем её до подмены _data
        var table = new (int Ofs, int Len)[HeaderLumps];
        for (int i = 0; i < HeaderLumps; i++)
        {
            GetLump(i, out int o, out int l);
            table[i] = (o, l);
        }

        _data = output;

        for (int i = 0; i < HeaderLumps; i++)
        {
            var (o, l) = table[i];
            if (l <= 0) continue;
            int shift = 0;
            foreach (var (start, length) in stale)
                if (start + length <= o) shift += length;
            SetLump(i, o - shift, l);
        }

        StaleBytes = FindStaleEntityCopies().Sum(c => c.Length);
        return removed;
    }

    public void Save(string path)
    {
        File.WriteAllBytes(path, _data);
        FilePath = path;
    }
}
