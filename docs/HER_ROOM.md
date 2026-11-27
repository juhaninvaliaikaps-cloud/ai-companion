# Her Room

Her Room is a small 2D top-down room that she and the player share. It is reached through a door on the desktop, opened from the Ribbon menu's "Her room" element. The design and its decisions are in Linear JUH-175. The art brief and the art reviews are in JUH-225.

**Status:** phase 1a, the room logic (JUH-226), is in the code. The room window and its drawing (phase 1b) are not built yet.

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

Tests: `RoomLogicTests`.
