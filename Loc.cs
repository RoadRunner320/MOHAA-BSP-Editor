namespace MohaaBspEditor;

/// <summary>
/// Простая локализация: английский (по умолчанию) и русский.
/// Loc.T("key") — строка на текущем языке, Loc.F("key", args) — с форматированием.
/// Выбранный язык запоминается в %AppData%\MohaaBspEditor\lang.txt.
/// </summary>
public static class Loc
{
    public const string English = "en";
    public const string Russian = "ru";

    public static string Lang { get; private set; } = English;
    public static event Action Changed;

    private static string SettingsFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "MohaaBspEditor", "lang.txt");

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                string v = File.ReadAllText(SettingsFile).Trim();
                if (v == English || v == Russian) Lang = v;
            }
        }
        catch { /* настройки не критичны */ }
    }

    public static void Set(string lang)
    {
        if (lang != English && lang != Russian) return;
        if (Lang == lang) return;
        Lang = lang;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, lang);
        }
        catch { /* ignore */ }
        Changed?.Invoke();
    }

    public static string T(string key)
    {
        if (!S.TryGetValue(key, out var v)) return key;
        return Lang == Russian ? v.Ru : v.En;
    }

    public static string F(string key, params object[] args) => string.Format(T(key), args);

    private static readonly Dictionary<string, (string En, string Ru)> S = new()
    {
        // ---------------- общее / главное окно
        ["app.title"] = ("MOHAA BSP Entity Editor", "Редактор энтити MOHAA BSP"),
        ["menu.file"] = ("File", "Файл"),
        ["menu.open"] = ("Open...", "Открыть..."),
        ["menu.save"] = ("Save", "Сохранить"),
        ["menu.saveas"] = ("Save as...", "Сохранить как..."),
        ["menu.savecompact"] = ("Save and remove stale entity copies", "Сохранить и удалить старые копии энтити"),
        ["menu.export"] = ("Export entities to .ent...", "Экспорт энтити в .ent..."),
        ["menu.import"] = ("Import entities from .ent...", "Импорт энтити из .ent..."),
        ["menu.exit"] = ("Exit", "Выход"),
        ["menu.entity"] = ("Entities", "Энтити"),
        ["menu.add"] = ("Add", "Добавить"),
        ["menu.dup"] = ("Duplicate", "Дублировать"),
        ["menu.del"] = ("Delete selected", "Удалить выбранные"),
        ["menu.scripts"] = ("Scripts", "Скрипты"),
        ["menu.scriptbuilder"] = ("Script builder (.scr)...", "Конструктор скриптов (.scr)..."),
        ["menu.language"] = ("Language", "Язык"),
        ["menu.help"] = ("Help", "Справка"),
        ["menu.about"] = ("About...", "О программе..."),
        ["about.title"] = ("About", "О программе"),
        ["about.text"] = ("{0}\nVersion {1}\n\nEditor for the entities of compiled Medal of Honor: Allied Assault, Spearhead and Breakthrough maps (.bsp), with a visual builder for map scripts (.scr).\n\nNot affiliated with or endorsed by Electronic Arts.",
                          "{0}\nВерсия {1}\n\nРедактор энтити скомпилированных карт Medal of Honor: Allied Assault, Spearhead и Breakthrough (.bsp) с визуальным конструктором скриптов карт (.scr).\n\nПроект не связан с Electronic Arts и не одобрен ею."),
        ["ctx.del"] = ("Delete", "Удалить"),
        ["tpl.empty"] = ("Empty entity", "Пустая энтити"),
        ["search.placeholder"] = ("Search keys and values...", "Поиск по ключам и значениям..."),
        ["tab.props"] = ("Properties", "Свойства"),
        ["tab.raw"] = ("Entity text (all)", "Весь текст энтити"),
        ["btn.applytext"] = ("Apply text", "Применить текст"),
        ["grid.key"] = ("Key", "Ключ"),
        ["grid.value"] = ("Value", "Значение"),

        ["status.hint"] = ("Open a .bsp (File menu or drag a file into the window)",
                           "Откройте .bsp (меню Файл или перетащите файл в окно)"),
        ["status.info"] = ("{0} v{1} | entity lump: #{2} | entities: {3} (shown {4}) | file size: {5:N0} bytes | stale copies: {6:N0} bytes",
                           "{0} v{1} | lump энтити: #{2} | энтити: {3} (показано {4}) | размер файла: {5:N0} байт | старые копии: {6:N0} байт"),

        ["msg.openfirst"] = ("Open a BSP first.", "Сначала откройте BSP."),
        ["msg.deleteq"] = ("Delete {0} entity(ies)?", "Удалить энтити: {0} шт.?"),
        ["msg.parseerr"] = ("Entity text parse error:\n{0}", "Ошибка разбора текста энтити:\n{0}"),
        ["msg.openerr"] = ("Could not open \"{0}\":\n{1}", "Не удалось открыть \"{0}\":\n{1}"),
        ["msg.saveerr"] = ("Save error:\n{0}", "Ошибка сохранения:\n{0}"),
        ["msg.importq"] = ("All current entities will be replaced with the file contents. Continue?",
                           "Все текущие энтити будут заменены содержимым файла. Продолжить?"),
        ["msg.importerr"] = ("Import error:\n{0}", "Ошибка импорта:\n{0}"),
        ["msg.compacted"] = ("Saved. Removed {0:N0} bytes of stale entity copies (file size {1:N0} -> {2:N0} bytes).",
                             "Сохранено. Удалено {0:N0} байт старых копий энтити (размер файла {1:N0} -> {2:N0} байт)."),
        ["msg.nostale"] = ("Saved. No stale entity copies were found, so there was nothing to remove.",
                           "Сохранено. Старых копий энтити не найдено, удалять нечего."),
        ["msg.unsaved"] = ("There are unsaved changes. Save?", "Есть несохранённые изменения. Сохранить?"),

        ["dlg.bspopen"] = ("BSP maps (*.bsp)|*.bsp|All files|*.*", "BSP карты (*.bsp)|*.bsp|Все файлы|*.*"),
        ["dlg.bspsave"] = ("BSP maps (*.bsp)|*.bsp", "BSP карты (*.bsp)|*.bsp"),
        ["dlg.entsave"] = ("Entity text (*.ent)|*.ent|Text (*.txt)|*.txt", "Entity text (*.ent)|*.ent|Текст (*.txt)|*.txt"),
        ["dlg.entopen"] = ("Entity text (*.ent;*.txt)|*.ent;*.txt|All files|*.*", "Entity text (*.ent;*.txt)|*.ent;*.txt|Все файлы|*.*"),
        ["dlg.ok"] = ("OK", "OK"),
        ["dlg.cancel"] = ("Cancel", "Отмена"),

        // ---------------- ошибки формата (BspFile / Entity)
        ["err.toosmall"] = ("The file is too small to be a BSP.", "Файл слишком мал для BSP."),
        ["err.table"] = ("The lump table failed validation (version {0}). This is probably a different BSP format.",
                         "Таблица lump'ов не прошла проверку (версия {0}). Вероятно, другой формат BSP."),
        ["err.noent"] = ("Could not find the entity lump in this BSP.", "Не удалось найти lump с энтити в этом BSP."),
        ["err.expected"] = ("Expected '{{', found '{0}' (entity #{1}).", "Ожидалась '{{', найдено '{0}' (энтити #{1})."),
        ["err.eofentity"] = ("Unexpected end of text inside entity #{0}.", "Неожиданный конец текста внутри энтити #{0}."),
        ["err.novalue"] = ("No value for key \"{0}\" in entity #{1}.", "Нет значения для ключа \"{0}\" в энтити #{1}."),
        ["err.quote"] = ("Key/value contains a quote: \"{0}\" \"{1}\". The entity format does not support this.",
                         "Ключ/значение содержит кавычку: \"{0}\" \"{1}\". Формат энтити этого не поддерживает."),
        ["err.unclosed"] = ("Unclosed quote in the entity text.", "Незакрытая кавычка в тексте энтити."),

        // ---------------- конструктор скриптов
        ["sf.title"] = ("MOHAA Script Builder (.scr)", "Конструктор скриптов MOHAA (.scr)"),
        ["sf.new"] = ("New", "Новый"),
        ["sf.open"] = ("Open .scr...", "Открыть .scr..."),
        ["sf.threads"] = ("Threads (labels):", "Потоки (метки):"),
        ["sf.addthread"] = ("+ Thread", "+ Поток"),
        ["sf.rename"] = ("Rename", "Переименовать"),
        ["sf.delete"] = ("Delete", "Удалить"),
        ["sf.up"] = ("Up", "Вверх"),
        ["sf.down"] = ("Down", "Вниз"),
        ["sf.dup"] = ("Duplicate", "Дублировать"),
        ["sf.addaction"] = ("Add action", "Добавить действие"),
        ["sf.tab.preview"] = ("Generated script", "Готовый скрипт"),
        ["sf.tab.header"] = ("Header (comments, exec)", "Заголовок (комментарии, exec)"),
        ["sf.newthread.title"] = ("New thread", "Новый поток"),
        ["sf.newthread.label"] = ("Thread (label) name, e.g. trigger1 or Tanim:", "Имя потока (метки), например trigger1 или Tanim:"),
        ["sf.rename.title"] = ("Rename", "Переименовать"),
        ["sf.rename.label"] = ("New thread name:", "Новое имя потока:"),
        ["sf.delthreadq"] = ("Delete thread \"{0}\" with all its actions?", "Удалить поток \"{0}\" со всеми действиями?"),
        ["sf.needthread"] = ("Create a thread first.", "Сначала создайте поток."),
        ["sf.openerr"] = ("Could not open the script:\n{0}", "Не удалось открыть скрипт:\n{0}"),
        ["sf.saveerr"] = ("Save error:\n{0}", "Ошибка сохранения:\n{0}"),
        ["sf.openfilter"] = ("MOHAA scripts (*.scr)|*.scr|All files|*.*", "Скрипты MOHAA (*.scr)|*.scr|Все файлы|*.*"),
        ["sf.savefilter"] = ("MOHAA scripts (*.scr)|*.scr", "Скрипты MOHAA (*.scr)|*.scr"),
        ["sf.defaultheader"] = ("// Map script\r\n\r\nexec global/ai.scr", "// Скрипт карты\r\n\r\nexec global/ai.scr"),

        // ---------------- каталог действий: act.<id> и act.<id>.p<N>
        ["act.prespawn"] = ("Wait for prespawn (before the level loads)", "Ждать prespawn (до загрузки уровня)"),
        ["act.spawn"] = ("Wait for spawn (player appeared)", "Ждать spawn (игрок появился)"),
        ["act.exec"] = ("Include script (exec)", "Подключить скрипт (exec)"),
        ["act.exec.p0"] = ("file, e.g. global/ai.scr", "файл, напр. global/ai.scr"),

        ["act.item"] = ("Player: give weapon", "Игроку: выдать оружие"),
        ["act.item.p0"] = (".tik file, e.g. weapons/sten.tik", "файл .tik, напр. weapons/sten.tik"),
        ["act.ammo"] = ("Player: ammo", "Игроку: патроны"),
        ["act.ammo.p0"] = ("type (pistol, smg, rifle, agrenade, smokegrenade)", "тип (pistol, smg, rifle, agrenade, smokegrenade)"),
        ["act.ammo.p1"] = ("amount", "количество"),
        ["act.use"] = ("Player: select weapon", "Игроку: выбрать оружие"),
        ["act.use.p0"] = ("weapon name", "название оружия"),
        ["act.takeall"] = ("Player: take everything", "Игроку: забрать всё"),
        ["act.fullheal"] = ("Player: full heal", "Игроку: полное лечение"),
        ["act.playsound"] = ("Player: play sound", "Игроку: звук"),
        ["act.playsound.p0"] = ("sound alias", "имя звука (alias)"),
        ["act.stufftext"] = ("Player: console command", "Игроку: консольная команда"),
        ["act.stufftext.p0"] = ("command, e.g. tmstartloop sound/music/x.mp3", "команда, напр. tmstartloop sound/music/x.mp3"),
        ["act.iprintnoloc"] = ("Player: on-screen message", "Игроку: сообщение на экране"),
        ["act.iprintnoloc.p0"] = ("text", "текст"),

        ["act.objadd"] = ("Objective: add", "Задача: добавить"),
        ["act.objadd.p0"] = ("objective number", "номер задачи"),
        ["act.objadd.p1"] = ("state (in the sample map: 1, 2 = pending/active, 3 = completed)",
                             "состояние (в примере 1, 2 — активна/ожидает, 3 — выполнена)"),
        ["act.objadd.p2"] = ("objective text", "текст задачи"),
        ["act.objadd.p3"] = ("targetname of the map point", "targetname точки на карте"),
        ["act.objcur"] = ("Objective: set current", "Задача: сделать текущей"),
        ["act.objcur.p0"] = ("objective number", "номер задачи"),

        ["act.setthread"] = ("Entity: set thread on use", "Энтити: назначить поток при использовании"),
        ["act.setthread.p0"] = ("entity targetname", "targetname энтити"),
        ["act.setthread.p1"] = ("thread name", "имя потока"),
        ["act.thread"] = ("Call thread", "Вызвать поток"),
        ["act.thread.p0"] = ("thread name", "имя потока"),
        ["act.waitthread"] = ("Call thread and wait", "Вызвать поток и дождаться"),
        ["act.waitthread.p0"] = ("thread name", "имя потока"),
        ["act.wait"] = ("Wait (seconds)", "Пауза (секунды)"),
        ["act.wait.p0"] = ("seconds", "секунды"),
        ["act.selfremove"] = ("Remove this entity (self remove)", "Удалить эту энтити (self remove)"),
        ["act.remove"] = ("Remove entity by targetname", "Удалить энтити по targetname"),
        ["act.remove.p0"] = ("targetname", "targetname"),

        ["act.objexec"] = ("Object: run script", "Объект: выполнить скрипт"),
        ["act.objexec.p0"] = ("object, e.g. level.friendly1", "объект, напр. level.friendly1"),
        ["act.objexec.p1"] = ("script file", "файл скрипта"),
        ["act.objthread"] = ("Object: start thread", "Объект: запустить поток"),
        ["act.objthread.p0"] = ("object, e.g. level.friendly1", "объект, напр. level.friendly1"),
        ["act.objthread.p1"] = ("thread, e.g. global/friendly.scr::friendlythink", "поток, напр. global/friendly.scr::friendlythink"),
        ["act.maxdist"] = ("Object: max distance (maxdist)", "Объект: макс. дистанция (maxdist)"),
        ["act.maxdist.p0"] = ("object", "объект"),
        ["act.maxdist.p1"] = ("value", "значение"),
        ["act.mindist"] = ("Object: min distance (mindist)", "Объект: мин. дистанция (mindist)"),
        ["act.mindist.p0"] = ("object", "объект"),
        ["act.mindist.p1"] = ("value", "значение"),

        ["act.println"] = ("Print to console (println)", "Вывод в консоль (println)"),
        ["act.println.p0"] = ("text in quotes or a variable", "текст в кавычках или переменная"),
        ["act.iprintln"] = ("Message to player (iprintln, localized)", "Сообщение игроку (iprintln, с локализацией)"),
        ["act.iprintln.p0"] = ("text", "текст"),

        ["act.if"] = ("Condition: if", "Условие: if"),
        ["act.if.p0"] = ("condition, e.g. isalive level.friendly2", "условие, напр. isalive level.friendly2"),
        ["act.else"] = ("Otherwise: else", "Иначе: else"),
        ["act.for"] = ("Loop: for", "Цикл: for"),
        ["act.for.p0"] = ("e.g. local.i=1;local.i<level.enemys+1;local.i++", "напр. local.i=1;local.i<level.enemys+1;local.i++"),
        ["act.open"] = ("Block: {", "Блок: {"),
        ["act.close"] = ("Block: }", "Блок: }"),
        ["act.goto"] = ("Jump: goto", "Перейти: goto"),
        ["act.goto.p0"] = ("label", "метка"),
        ["act.assign"] = ("Assign: variable = value", "Присвоить: переменная = значение"),
        ["act.assign.p0"] = ("variable, e.g. level.friendly1.health", "переменная, напр. level.friendly1.health"),
        ["act.assign.p1"] = ("value", "значение"),

        ["act.raw"] = ("Raw text", "Свой текст (raw)"),
        ["act.raw.p0"] = ("script line", "строка скрипта"),
        ["act.comment"] = ("Comment", "Комментарий"),
        ["act.comment.p0"] = ("text", "текст"),
    };
}
