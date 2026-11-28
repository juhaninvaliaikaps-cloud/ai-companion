# adult-player.exe

This is the small .NET Framework WinForms host that embeds `ffplay` for the
adult clip window. It is kept under `tools/adult-player/` so the executable in
`Assets/StreamingAssets/` can be rebuilt from source.

## Build

Requires the .NET SDK with the .NET Framework 4.8 reference assemblies.

```powershell
dotnet build tools/adult-player/adult-player.csproj -c Release
```

The build output is:

```text
tools/adult-player/bin/Release/net48/adult-player.exe
```

Copy it over the asset, keeping the existing `.meta` and path:

```powershell
Copy-Item tools/adult-player/bin/Release/net48/adult-player.exe `
  Assets/StreamingAssets/adult-player.exe -Force
```

Do not delete or regenerate `Assets/StreamingAssets/adult-player.exe.meta`.

## Runtime

The host looks for `ffprobe.exe` and `ffplay.exe` on `PATH`, then in
`%LOCALAPPDATA%\Microsoft\WinGet\Links`. It has no network calls. The source is a
decompiled copy of the shipped executable, kept here so the binary and source
stay together.
