# Spatha Macro Recorder

A keyboard and mouse macro recorder for Windows 10/11, built for the ASUS ROG Spatha X and
working independently of Armoury Crate.

## Disclaimer

Macros may violate the rules of specific games. Responsibility for using this tool lies with
the user.

## What it does

- Records keyboard and mouse input with 1 ms timing accuracy, keeping `down` and `up` as
  separate steps so holds and combos replay correctly.
- Replays macros through `SendInput` using **scan codes**, which is what games reading
  DirectInput / Raw Input actually see. Virtual-key events are ignored by most games.
- Binds macros to mouse buttons through a visual picker — you click a plate next to a picture
  of the mouse, never a raw key code.
- Playback modes: once, hold (loops while the trigger is held), repeat N times. Speed
  multiplier 0.5x–2.0x and optional delay jitter.
- Panic key stops playback instantly and releases every key and button the macro was holding.

## Portable build

The app ships as a single self-contained `.exe`. No installer, no .NET runtime to install, no
DLLs beside it. It writes nothing to the registry unless you enable "Start with Windows".

Data lives in `%AppData%\SpathaMacroRecorder\`:

```
Profiles\<name>.json   macro profiles
logs\log-<date>.txt    startup and error log
settings.json          hotkeys, autostart, desktop mode
crash.txt              startup failures, if any
```

The log records startup steps and errors only. Individual keystrokes are never written to
disk — key events exist in memory just long enough to become macro steps while recording.

### Verify the build

```
SpathaMacroRecorder.exe --self-test
```

Builds the main window, loads the theme and evaluates the bindings, then reports pass or fail.

## Side buttons need a one-time setup

Windows exposes only five mouse buttons to an application: left, right, wheel click, forward
and back. The six extra buttons on the Spatha X side block are sent as vendor HID usages that
no application can read directly.

The way around it: in Armoury Crate, assign a **keyboard key** to each of those buttons. The
assignment is stored in the mouse's onboard memory, so Armoury Crate can be closed afterwards.

Which key does not matter. F17–F22 are the tidiest choice — they exist in the HID
specification, Windows recognises them and no game uses them — but Armoury Crate does not
always offer them. Anything works: a rarely used key, or a combination such as `Ctrl+Alt+1`.
Pick keys you do not press while playing.

Then teach the program which button is which:

1. Open the main screen and click a marker on the photo.
2. Press **Detect**.
3. Press that button on the mouse.

The program records whatever code arrived and binds it to that marker. Repeat for each button.
The panel shows what the selected button currently answers to; **Reset detection** returns
everything to the default F17–F22 order.

Detection is the only reliable way round: which physical button sends which key is decided
inside Armoury Crate, and nothing outside it can be read. Forward and back are detected the
same way, so a pair swapped round is fixed in two clicks.

While the program runs, the keys it recognised as mouse buttons are swallowed: neither Windows
nor the game ever sees them. Otherwise Windows reacts to them on its own (some assignments
launch built-in actions), and a numpad key reaches the game as an arrow and breaks a stratagem.
A consequence worth knowing: whatever key you pick stops working as that key everywhere while
the program is open — so pick keys you never use. A button detected without Ctrl/Alt/Shift
still fires while you hold them, which is how stratagems are entered in Helldivers 2.

Numpad keys are recognised by their physical position, so toggling NumLock does not break them.

Left, right and wheel click need no setup.

## Built-in stratagems

Every Helldivers 2 stratagem from the [wiki](https://helldivers.wiki.gg/wiki/Stratagems/ru) is built
in: 63 of them, from support weapons to mission stratagems. Nothing has to be recorded.

1. Click a marker on the main screen.
2. Type part of the name in the search box above the list — `лазер`, `AC-8`, `пулемёт`. Case,
   `ё` and dashes do not matter. Enter picks the first match; a click picks any.

Every row shows the stratagem's in-game icon and its code in arrows, and the icon of the stratagem
on the button is shown above the search box. The icons come from the open
[Helldivers 2 stratagem icon set](https://github.com/nvigneux/Helldivers-2-Stratagems-icons-svg),
converted to PNG and built into the exe.

The chosen stratagem becomes an ordinary macro in the current profile and is bound to the button.
It types the code with the **keyboard arrow keys** only (never WASD), 30 ms before every press and
release. Hold Ctrl as usual and press the mouse button. The macro can be edited in Settings like any
other; picking the same stratagem again reuses it instead of creating a copy.

## Pause between key presses

Preferences → *Pause between key presses*, 30 ms by default. Games read the keyboard once per
frame; a press and a release sent in the same instant never reach the game, and a macro made of
zero delays is exactly that. The pause is inserted before every key press and release during
playback; larger delays from the steps table still apply as they are. If stratagems still fail
at a low frame rate, raise it to 50. 0 turns it off.

## Starting and closing together with the game

The program does not start with Windows and does not sit in the tray. Steam starts Helldivers 2
through it instead:

1. Open Preferences and press **Copy** under *Start together with the game (Steam)*.
2. In Steam: Library → Helldivers 2 → Properties → General → Launch options — paste the line.
   It looks like `"C:\Spatha\SpathaMacroRecorder.exe" --game %command%`.

Everything after `%command%` is appended by Steam to the game's own command and reaches Helldivers 2
unchanged, so launch options you already had (`--use-d3d11`, for example) go at the end of the line.
The game renders through DirectX 12 by default; no flag is needed for that.

From then on, launching the game from Steam starts the program, and the program starts the game
with exactly the command Steam gave it, from the folder Steam started the program in — the game's
install root. The game looks for its data relative to that folder, so handing it the folder of the
exe itself (`bin`) leaves it on a black screen. The window opens minimised so it does not jump over the
loading game. When the game closes, the program closes too. A short restart of the game process
right after launch (anti-cheat does this) does not count: the game has to be gone for three checks
in a row, about six seconds.

The program can still be opened by hand at any time; opened that way it stays open until you close
it. If a copy is already open when the game starts, the copy started by Steam replaces it.

The game is recognised by the process name taken from that command (`helldivers2`). Only the list
of running processes is read: no injection, no reading another process's memory. If you move the
program to another folder, copy the line again.

Earlier builds added the program to Windows startup; this build removes that entry on launch.

## If the buttons stop working after a restart

1. Make sure the program is running. It starts by itself only when the game is launched from
   Steam with the launch options above; otherwise open it by hand.
2. Make sure the tool that sends the keys (Armoury Crate or G-Helper) is running too. If its
   button mapping lives in the software rather than in the mouse's memory, the keys only exist
   while it runs.
3. Select a marker and press **Detect**, then press the button. If nothing arrives, the key never
   reaches Windows and the fix is in step 2; if it arrives, the problem is on this program's side.

The status line at the bottom of the main window shows the build name, whether input capture is
on, whether it will close together with the game, the last mouse button signal, the marker it matched
and whether a macro started. A screenshot of it right after pressing the button shows where the
chain breaks.

Starting the program closes any copy that is already running (for example one waiting in the
tray after Windows started), so two copies never fight over the same buttons.

The input hooks run on their own threads, so a busy window (typical right after Windows starts)
can no longer make Windows drop them; they are also reinstalled whenever the game starts.

## Administrator rights

Not required. The low-level hooks and `SendInput` work without elevation. Run as administrator
only if macros do not reach a game — Windows blocks input sent to windows running with higher
privileges than the sender.

## Building from source

Requires the .NET 10 SDK.

```
dotnet build
dotnet test
dotnet publish src/SpathaMacroRecorder -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The build also works from macOS or Linux (cross-compilation produces the Windows executable),
but the app itself and its tests only run on Windows.

## Scope

Standard input emulation through `SendInput` only. No reading or writing other processes'
memory, no DLL injection, no hooking into game processes.
