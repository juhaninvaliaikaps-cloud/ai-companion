# Encrypted local storage

Private files under the per-user folder are encrypted at rest. The key never leaves the machine. Nothing in this path calls a network API.

The folder is `%APPDATA%\ProjectLeia` (typically `C:\Users\<you>\AppData\Roaming\ProjectLeia`). If the roaming profile path is empty, the same names are used under `Application.persistentDataPath\ProjectLeia`.

## What is encrypted

AES-256-GCM, file by file. The name is bound into the ciphertext, so a file copied over a different name does not decrypt.

| Relative path | On disk |
| --- | --- |
| `settings.json` | Encrypted. Holds provider, persona id, private mode, and the screen-look switches (including cloud consent and the chosen display). |
| `personality.json` | Encrypted. |
| `memory.json`, `memory-<persona>.json` | Encrypted. One file per persona. The empty id keeps `memory.json`. |
| `chat-history.json`, `chat-history-<persona>.json` | Encrypted. |
| `personas\<id>.json` | Encrypted. These are personas you saved or edited. |
| `secrets.json` | Not AES. The API key is already wrapped with Windows DPAPI for the current user (`CryptProtectData`, no machine scope). The file says `"protected": true`. See [CHAT_PROVIDERS.md](CHAT_PROVIDERS.md). |
| `install-key.leia` | The AES key, not a data file. On Windows this is a DPAPI blob (`LEIAKEY1`). It is not exported. |

Built-in personas under `Assets/_Project/Resources/Personas/` stay plain text. They ship with the app and are not your data.

## What is not encrypted

These were left out on purpose:

- **Portrait images** and `portraits\portraits.json`. The app reads them by path for the expression frame. They are not chat or memory. Export copies the bytes. Wipe deletes them. See [PORTRAITS.md](PORTRAITS.md).
- **Screen observations.** The last few looks stay in RAM and are never written. Screen-look on/off, consent, and the display name live in `settings.json`, which is encrypted. Capture code does not write a file. See [SCREEN_LOOK.md](SCREEN_LOOK.md).
- **PlayerPrefs** for window scale, zoom, mute, pause, whether chat starts open, the taskbar flag, and avatar yaw. Those are window preferences, not conversation content, and Unity owns that hive. Wipe does not clear them. Some of the same flags are also stored in `settings.json` once you change settings.
- **Piper's temporary wav.** It is created for one utterance and deleted after playback.
- **Whisper model files and Piper/Kokoro binaries.** Those are models, not your messages.
- **The Windows Run key** for start with Windows.
- **A stray file** you drop in the folder yourself (for example `notes.txt`). Wipe removes the app's files listed above, not an unrelated file.

## How the cipher is chosen

`ProjectSettings` uses API compatibility level 6, which is .NET Standard 2.0. `System.Security.Cryptography.AesGcm` is not part of that profile and is not in Unity's Mono or IL2CPP class library, so a direct call would not compile for the Windows player or for EditMode tests.

The implementation is AES-256-GCM (NIST SP 800-38D) with a 96-bit nonce and a 16-byte tag, built on `Aes` in ECB mode as the block function. That type exists on Mono and on IL2CPP, and it does not need a new package. No `Packages/` or `ProjectSettings/` change was required.

Each file starts with the ASCII bytes `LEIAENC1`, then a version byte, the nonce, the tag, and the ciphertext. Logs record the file name and the exception type. They do not record plaintext, keys, or API keys.

## Where the key lives

`install-key.leia` sits in the ProjectLeia folder.

- **Windows editor and Windows player.** The 32 key bytes are wrapped with DPAPI for the current user. `CRYPTPROTECT_LOCAL_MACHINE` is not set, so another account on the same PC cannot unwrap it. The UI prompt is suppressed.
- **Windows player if DPAPI fails.** The key file is not created and private files are not written. The player does not fall back to plaintext. The same rule applies to `secrets.json`: a failed protect does not store the raw API key.
- **Editor and CI where DPAPI is not available** (the Linux EditMode job). The key file's first line is `LEIA-DEV-FALLBACK-NOT-DPAPI`, then the key as base64. Data files are still AES-GCM, not plaintext. A Windows player refuses to use that fallback file.

Copying the folder to another Windows account does not make the data readable, because the DPAPI blob is bound to the user who created it.

## Migration

On startup, and again the first time a file is read, a plaintext file is migrated in place:

1. Encrypt to a sibling `*.writing` file.
2. Decrypt that copy and compare it to the original bytes.
3. Only then replace the original. A `*.held` copy of the original is kept until the replaced file decrypts to the same bytes.
4. If any step fails, the original stays and a warning is logged. The app still uses the plaintext it already read.

A second launch finds `LEIAENC1` and does not migrate again.

## Unreadable files

A truncated file, a flipped byte, or a file whose name does not match the name inside the tag does not throw. The bad file is moved to `<name>.unreadable-<utc>` beside it, a warning names the file, and that store starts empty. The backup is the ciphertext, not a decoded copy. The next save creates a new file with the current install key.

If the install key itself cannot be unwrapped, it is left in place and new writes are skipped.

## Export and wipe

Settings → **Privacy**.

- **Export decrypted folder** writes JSON and persona files as plaintext into the folder you pick, plus a raw copy of `portraits`. The API key is included only when this PC can read `secrets.json`, in the existing `"protected": false` shape. The install key is not exported. The folder must sit outside the ProjectLeia directory.
- **Export decrypted zip** writes `ProjectLeia-export.zip` in that folder (or the `.zip` path you typed). The zip is built in a temp directory that is deleted afterwards. The zip is stored, not compressed.
- **Wipe all local data** asks you to confirm. It deletes the encrypted files, saved personas, portraits in that folder, `secrets.json`, quarantine copies, and the install key. Built-in personas stay. An unrelated file in the folder is left. PlayerPrefs are left. The in-memory chat and memory stores are cleared. The next settings change writes a new key.

The export is decrypted. Keep that folder private.

## Manual check on Windows

Replace the build folder if yours is not `Builds\Windows`. Run the player, say something worth remembering, and turn on **Save chat history** before the file checks.

Ciphertext starts with `LEIAENC1` (`4C 45 49 41 45 4E 43 31`):

```powershell
Set-Location $env:APPDATA\ProjectLeia
Format-Hex -Path .\settings.json -Count 8
Get-ChildItem -Name
```

`install-key.leia` should be present. Opening it in Notepad should not show a JSON settings document. `Select-String` on the encrypted files should not print a companion name or a chat line:

```powershell
Set-Location $env:APPDATA\ProjectLeia
Select-String -Path .\settings.json,.\memory.json,.\personality.json,.\chat-history.json -Pattern "companionName" -SimpleMatch
```

Export from **Settings → Privacy** into a folder outside AppData, then read the copy (this print is the decrypted file; close the window when you are done):

```powershell
Set-Location "$env:USERPROFILE\Desktop"
Get-Content .\settings.json -TotalCount 8
```

If you used **Export decrypted zip**:

```powershell
Set-Location "$env:USERPROFILE\Desktop"
Expand-Archive -Path .\ProjectLeia-export.zip -DestinationPath .\ProjectLeia-export-check -Force
Get-ChildItem .\ProjectLeia-export-check -Recurse -Name
```

Tamper one byte, start the player again, and confirm it still opens. Memory for that file is empty and a backup remains:

```powershell
Set-Location $env:APPDATA\ProjectLeia
$bytes = [System.IO.File]::ReadAllBytes(".\memory.json")
$bytes[$bytes.Length - 1] = $bytes[$bytes.Length - 1] -bxor 255
[System.IO.File]::WriteAllBytes(".\memory.json", $bytes)
```

```powershell
Set-Location "C:\path\to\ai-companion\Builds\Windows"
.\BackseatCatgirl.exe
```

```powershell
Set-Location $env:APPDATA\ProjectLeia
Get-ChildItem -Filter "memory.json.unreadable-*"
```

Wipe from **Privacy → Wipe all local data → Confirm wipe**. The private files and the install key are gone. `notes.txt`, if you added one, is still there:

```powershell
Set-Location $env:APPDATA\ProjectLeia
Get-ChildItem -Force
```

Quit the player before deleting files by hand. A running player can write them back.
