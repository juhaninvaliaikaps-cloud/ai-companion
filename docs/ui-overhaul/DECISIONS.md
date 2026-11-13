# Decisions made during the overnight JUH-104 / JUH-108 run

Each entry: what was ambiguous, what was chosen, and why. Nobody was available to ask, so
the safest reasonable option was taken.

## PR A

**A1. Mockup file location.** The addendum names `E:\LOOT\radial_mockup.png`, which does not
exist on this PC. The identical approved image was attached to the task in chat; that file is
committed as `docs/ui-overhaul/radial_mockup.png`.

**A2. uGUI + TextMeshPro, not UI Toolkit.** `OverlayWindow.IsOverUi` decides click-through
with `EventSystem.RaycastAll`, which only sees uGUI graphics. UI Toolkit panels would need a
second hit test. uGUI keeps that rule unchanged.

**A3. TMP Essential Resources committed.** `com.unity.ugui` 2.6.0 includes TextMeshPro, but its
default font asset, settings and SDF shaders come from the "TMP Essential Resources" package.
Without them a player build has no default TMP font. They were imported once in batchmode
(`-importPackage`) into `Assets/TextMesh Pro/` (about 4 MB, the `.ttf` through LFS). The old
UI does not use them.

**A4. Theme is a C# class, not a ScriptableObject asset.** `HappiTheme` holds every colour,
font, size, radius, spacing and timing, and `HappiTheme.Current` is the single instance the
new views read. A plain class has no asset GUID or import step, is easy to diff, and works in
EditMode tests. Making it a ScriptableObject later only means wrapping this class.

**A5. 100–200% scaling through the theme.** `OverlayWindow.LockUiPixels` forces every
`CanvasScaler` to `ConstantPixelSize` with factor 1, including new ones. Changing that would
change the old UI. New views multiply their theme sizes by `HappiTheme.UiScale(Screen.dpi)`
(clamped 1.0–2.5).

**A6. New code location.** The brief says `Assets/Scripts/UI/NewUI/`; the project keeps scripts
under `Assets/_Project/Scripts/`, so new views go in `Assets/_Project/Scripts/UI/NewUI/` with a
new assembly `AICompanion.NewUI`. It references the existing assemblies; none of them
references it, so the old UI cannot depend on the new one.

**A7. Product name.** Following the JUH-104 comment, new views use "Happi" as the app name
(settings header, wizard, chat title). The old UI keeps `AppName.Display` unchanged. The wheel
centre shows her name: the active persona's display name, else the setup name, else "Leia".

**A8. Right-click conflict.** On `main`, right-click on her toggles rotate mode (K-10). The
wheel needs right-click. With New UI **on**, right-click on her opens the wheel and
**Shift+right-click** toggles rotate mode as before; rotate mode stays reachable. With New UI
**off** nothing changes. (Implemented in PR D.)

**A9. Inventory duplicates.** The Voice tab on `main` shows the Piper exe and Piper voice
fields twice. The inventory lists them once (S-VO-04, S-VO-05); the new settings view shows
them once. The old panel is not changed.
