# MOHAA BSP Entity Editor

**Version 1.0.1**

A Windows desktop tool for editing the entities of **compiled** *Medal of Honor: Allied Assault*
maps (`.bsp`) without decompiling them, plus a visual builder for map scripts (`.scr`).

It supports maps from Allied Assault, Spearhead and Breakthrough. Only the entity lump of the
BSP is rewritten; geometry, lightmaps, and every other lump are left byte-for-byte untouched.

Inspired by [CoD-BSP-Editor](https://github.com/kartjom/CoD-BSP-Editor), which does the same for
Call of Duty 1. No code from that project is used.

> **Disclaimer:** this project is not affiliated with or endorsed by Electronic Arts.
> *Medal of Honor* is a trademark of Electronic Arts Inc.

## Features

**Entity editor**
- Open a `.bsp` from the menu, by drag and drop, or as a command-line argument
- Fast entity list (thousands of entities) with search across all keys and values
- Edit properties in a table: change values, add or delete keys
- Add entities from templates, duplicate (`Ctrl+D`), delete (`Del` or context menu); multi-select supported
- "Entity text" tab to edit the whole entity lump as plain text
- Export / import entities as `.ent` files
- Save, Save as, and **Save and remove stale entity copies** (see below)
- A `.bak` backup of the original file is created on the first save
- Status bar shows format, version, entity lump index, entity count, file size and stale data size

**Script builder (`.scr`)** — `Scripts → Script builder` (`Ctrl+R`)
- Build a script from threads (`main:`, `trigger1:` ...) and ordered actions with typed fields
- 32 ready-made action types: give weapons, set ammo, objectives, messages, sounds,
  waits, thread calls, `if` / `for` / `goto`, variable assignment, and more; plus raw text and comments
- Live preview of the generated script
- Opens existing `.scr` files and breaks them into editable blocks
- Saves in Windows-1251 with CRLF line endings, like the original maps; a `.bak` is created first

**Interface language** — English (default) and Russian, switchable from the **Language** menu.
The choice applies instantly and is remembered in `%AppData%\MohaaBspEditor\lang.txt`.

## Supported map formats

Verified by analysing real maps from each game:

| Game | Signature | Version |
|------|-----------|---------|
| Allied Assault | `2015` | 19 |
| Spearhead | `2015` | 19 |
| Breakthrough | `EALA` | 21 |

Other signatures are not rejected outright: the file is accepted if its lump table is valid and an
entity lump can be found.

## Keyboard shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+O` | Open |
| `Ctrl+S` | Save |
| `Ctrl+Shift+S` | Save as |
| `Ctrl+D` | Duplicate selected entities |
| `Del` (in the list) | Delete selected entities |
| `Ctrl+R` | Open the script builder |

In the script builder: `Ctrl+N` new, `Ctrl+O` open, `Ctrl+S` save, `Ctrl+Shift+S` save as.

## Stale entity copies

When the entity text grows, a normal **Save** writes the new lump at the end of the file and points the
header there. The old text stays in the file as unreferenced data that the game ignores. After several
saves a map can contain multiple dead copies of its entity list (which also makes hex-editor searches
show duplicates of the same entity).

The status bar shows how much of this data a file contains. **File → Save and remove stale entity
copies** saves and then deletes those old copies, shifting the lump offsets in the header.

- Only recognised copies of entity text are removed. Lump contents are never changed.
- Other unreferenced data (for example the ~80 bytes at the end of original maps) is kept, because it is
  unknown whether the game reads it.

## BSP layout used by the editor

```
offset  size  field
0       4     signature  ("2015" or "EALA")
4       4     version    (19 or 21)
8       4     checksum   (kept as is)
12      28*8  lump table: { int32 offset; int32 length } x 28
236     ...   lump data
```

The entity lump is **lump 14** and holds the text `"key" "value"` entity list, terminated with a NUL byte.
If that lump does not contain entity text, the editor searches the other lumps. If the new text fits in the
old lump it is written in place; otherwise the lump is extended (if it is last in the file) or appended.

## Building

Requirements: Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```
dotnet build -c Release
dotnet run -c Release
```

To produce a distributable build:

```
dotnet publish -c Release -r win-x64 --self-contained false
```

(`--self-contained false` requires the .NET 8 Desktop Runtime on the target machine.)
To target .NET 6 instead, change `net8.0-windows` to `net6.0-windows` in `MohaaBspEditor.csproj`.

The version number is defined once, in `MohaaBspEditor.csproj` (`<Version>`), and is shown in the
window title and in **Help → About**.

## Project structure

| File | Purpose |
|------|---------|
| `Program.cs` | Entry point, language and code-page setup |
| `MainForm.cs` | Entity editor window |
| `BspFile.cs` | BSP header, lump table, entity lump read/write, stale-copy cleanup |
| `Entity.cs` | Entity model, parser and serializer |
| `ScriptForm.cs` | Script builder window |
| `ScriptModel.cs` | Script action catalog, generator and `.scr` parser |
| `Loc.cs` | English / Russian string table and language switching |
| `AppInfo.cs` | Application version |

## Known limitations

- **Always keep a backup.** A `.bak` is created automatically, but test your maps in the game.
- The header checksum (bytes 8-11) is not recalculated. It is not known whether the game verifies it.
- Entity text is read and written as Latin-1, so bytes are preserved exactly. Non-ASCII text in
  existing maps (for example Cyrillic in a Windows-1251 `message` key) appears garbled but round-trips
  losslessly. Typing characters above U+00FF into the entity text is not supported.
- Keys and values cannot contain double quotes (the entity format has no escaping).
- Only the entity lump is editable. Brushes, models, lighting and other lumps are not touched.
- Script builder: blank lines are dropped when importing a `.scr`, and indentation inside `if` / `for`
  blocks is normalised. This does not affect how the script runs.
- The script action catalog is based on real map scripts. Check parameter values (ammo types,
  objective states and so on) against the game.
