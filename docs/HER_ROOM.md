# Her Room

Her Room is a small 2D top-down room that she and the player share. It is reached through a door on the desktop, opened from the Ribbon menu's "Her room" element. The design and its decisions are in Linear JUH-175. The art brief and the art reviews are in JUH-225.

**Status:**
- **Phase 1a, the room logic (JUH-226):** built.
- **Phase 1b, the room window (JUH-228):** built, using Codex's draft art as stand-ins. Launch with `--ribbon-ui` and click the door glyph beside the Ribbon menu.
- **Phase 2, slots and activities:** not started.

## v1 in short

- **The window.** One warm room in a Ribbon-styled window. The window is square and resizable: at least 480 px, at most the height of the work area. The chat box docks beside it.
- **The door.** It opens in about 1.2 s, and the opening covers loading the room. It shows the Windows wallpaper, never a screen capture.
- **Light.** The room follows the computer's clock: a light tint, the sky in the window, and the lamp in the evening. The art is drawn in neutral daylight, and code adds all the lighting.
- **Moving.** She and the player are chibi figures. The player moves by clicking, which is also how you use objects, or with the arrow keys, which work only while the chat input does not have focus.
- **Slots.** There are seven: bed, desk and PC, window, rug and pouf, bookshelf, wardrobe, and a hobby that is personal to each persona. Activities and slots come in phase 2.

## The logic (Core assembly)

**Coordinates.** Points on the room picture are fractions of it: x to the right and y down from the top left. A redrawn or resized shell only changes the numbers in `RoomLayout.V1()`. The v1 numbers are measured from Codex's empty-shell draft: 1254 × 1254 px, a floor of 12 × 9 tiles, the window opening, and the door in columns 5 and 6 of the front row.

| Class | What it does |
| --- | --- |
| `RoomLayout` | The floor grid, the floor and window rectangles, the entry tiles for her and the player, and the tiles furniture blocks (the rug is walkable). Converts a tile to a point (`PointOf`, `FeetOf`) and a point to a tile (`TryTileAt`), and gives the back-to-front draw order (`SortOrder`). |
| `RoomPaths` | The shortest walk over free floor in four directions. A click on furniture or on an unreachable tile walks to the reachable free tile closest to it. Its buffers are reused between calls. |
| `RoomWalker` | One figure: walks a path at about 2.6 tiles per second, and faces down, up, left or right for the animation rows. A new click in the middle of a step finishes that step first. An arrow key steps one free tile, or only turns when the tile is blocked. `Tick` does not allocate. |
| `RoomDoor` | Closed, opening, open, closing. A close while opening turns the door around from where it is. `Tick` reports `Opened` and `Closed` once each, and `Angle` is the eased swing of the door panel. |
| `RoomClock` | From the local time: a tint that blends smoothly between night, morning (from 05:30), day (09:00–17:00), dusk (to 21:00) and night. Also the sky colour, the lamp (on 18:30–07:00, with half-hour fades) and bedtime (22:30–06:30). |
| `RoomWindowSize` | Keeps the square window between 480 px and the work-area height, and converts a click in the window to a point on the room picture. |

Tests: `RoomLogicTests`, `RoomWanderTests` and `WallpaperCropTests`.

## The room window (phase 1b, NewUI assembly)

**Opening and closing**
- `HerRoomView` is added by `NewUiRoot` when the Ribbon is on.
- A click on the Ribbon's door glyph opens it; a second click closes it. The × and Esc also close it.

**The door**
- `DesktopWallpaper` loads Windows' wallpaper file, trying `TranscodedWallpaper` first and then the picked file. It never captures the screen.
- `WallpaperCrop` (Core) picks the part of the wallpaper that sits behind the door. It assumes Windows' "Fill" mode, and uses the work area as the monitor.
- When there is no wallpaper, the door is plain wood.
- The door panel swings on its left hinge, then the room window grows out of the doorway and fades in. Closing runs it backwards.

**The window**
- It is centred in the visible part of Happi's window and square.
- It can be resized from the cyan corner. The size is saved in PlayerPrefs `HerRoom.Side` and limited by `RoomWindowSize`.
- The frame is Ribbon-style: dark panel, clipped corners, gold accent, "Her Room" and ×.

**Inside**
- The sky colour sits behind the shell's open window. The shell is tinted by `RoomClock`.
- Two 256 px figures stand with their feet on the tile's front edge. They're drawn back to front, and bob while walking until walk frames exist.

**Input**
- **Clicking:** a click on the floor walks the player there.
- **Arrow keys:** they step the player while nothing else owns the keyboard (chat input, Settings, the wheel). Because the room implements `IOverlayKeepAwake`, the 2D avatar's own arrow keys are off while it is open (the JUH-200 guard).

**Her (`RoomWander`)**
- She walks in through the door.
- After 6–14 s she strolls to a random free tile.
- She stays and faces the player while he is within two steps.
- At bedtime she goes to the tile at the foot of the bed and stays there.

**The desktop**
- `HerRoomNow.IsOpen` pauses her desktop walking (`DesktopPresence`) while the room is open.
- Her desktop figure is behind the room window. It is not hidden separately.

**Stand-in art** (`Resources/HerRoom/`, from Codex's drafts in JUH-225)
- `room_shell.png`: 1024 px, with the window opening made transparent.
- `happi_idle.png` and `player_idle.png`: 256 px, with the feet at y = 240.
- Happi's room outfit is the swimsuit (Jussi).
- The finished art replaces these files in place.

**Not yet:** chat docking and her speech bubble, slots and activities, walk frames, and the lamp glow.
