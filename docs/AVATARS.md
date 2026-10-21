# Avatars

The overlay shows one humanoid at a time. `AvatarMount` loads it, hides the pink capsule while a model is showing, and registers the model's renderer bounds so drag and click-through hit the mesh. The capsule comes back if nothing loads.

`Assets/_Project/Settings/AvatarSelection.asset` chooses the model. Preference `Auto` uses the first source that exists:

1. **Local prefab.** A reference you assign on this machine. This is where Asuna goes.
2. **VRM sample.** `Assets/StreamingAssets/Avatars/CompanionSample.vrm`, loaded at runtime with UniVRM (`Vrm10.LoadPathAsync`, including VRM 0.x migration).
3. **FBX sample.** `Assets/_Project/Avatars/Resources/Avatars/CompanionSample.fbx`, loaded with `Resources.Load`.
4. **Capsule.** The placeholder already in `Main`.

Press **F7** to cycle the local prefab, the VRM sample, and the FBX sample. **F8** is still the always-on-top toggle.

The console logs one line when a model is ready, for example `Avatar ready: CompanionSample humanoid=True shapes=20 aa=Aa,jawOpen ...`. That line is the blendshape list and the expression map. Code can read the same data from `IAvatarDescriptorService` (`TryGetBlendShapeNames`, `TryGetExpression`).

## Expression names

`ExpressionPresetMap` maps blendshape names onto `Aa`, `Ih`, `Ou`, `Ee`, `Oh`, `Blink`, `Joy`, `Angry`, `Sorrow`, and `Fun`.

| Preset | Names it accepts |
| --- | --- |
| Aa | `aa`, `a`, `Fcl_MTH_A`, `jawOpen` |
| Ih | `ih`, `i`, `Fcl_MTH_I`, `mouthClose` |
| Ou | `ou`, `u`, `Fcl_MTH_U`, `mouthPucker` |
| Ee | `ee`, `e`, `Fcl_MTH_E`, `mouthStretch*` |
| Oh | `oh`, `o`, `Fcl_MTH_O`, `mouthFunnel` |
| Blink | `blink`, `Fcl_EYE_Close`, `eyeBlinkLeft`, `eyeBlinkRight` |
| Joy | `joy`, `happy`, `Fcl_ALL_Joy`, `mouthSmile*` |
| Angry | `angry`, `Fcl_ALL_Angry`, `browDown*` |
| Sorrow | `sorrow`, `sad`, `Fcl_ALL_Sorrow`, `mouthFrown*` |
| Fun | `fun`, `relaxed`, `Fcl_ALL_Fun` |

A VRM expression preset is also recorded when its clip points at a blendshape, even if that shape's name is custom. Names that do not match stay in the full list and are not forced into a preset. Asuna's 52 ARKit shapes (`jawOpen`, `eyeBlinkLeft`, `mouthFunnel`, and the rest) use this table.

## Add a VRM

1. Put a `.vrm` file in `Assets/StreamingAssets/Avatars/`.
2. Set **Streaming Vrm Relative Path** on `AvatarSelection` to `Avatars/YourModel.vrm`, and set **Preference** to `Vrm Sample` or leave `Auto`.
3. Enter Play mode. UniVRM builds the humanoid. VRM 0.x files are migrated while loading (`canLoadVrm0X`). The `com.vrmc.univrm` package is installed so the editor can import 0.x files as well.

The project pins UniVRM **v0.131.4** (`com.vrmc.gltf`, `com.vrmc.vrm`, `com.vrmc.univrm`).

## Add an FBX

1. Put the `.fbx` under `Assets/_Project/Avatars/` or `Assets/LocalAvatars/`. A file loaded by `Resources.Load` also needs to sit in a `Resources` folder. The sample path is `Avatars/CompanionSample` because the file is `Assets/_Project/Avatars/Resources/Avatars/CompanionSample.fbx`.
2. Unity imports it as **Humanoid** and auto-maps bones that are not already mapped. Required bones: Hips, Spine, Head, both upper and lower arms, both hands, both upper and lower legs, both feet. `LeftArm` and `mixamorig:Hips` count.
3. If the rig cannot be mapped, the Console has an error that names the missing bones. The mesh still appears when the file itself loads. Shared animations cannot retarget until the rig is Humanoid.
4. Point **Fbx Resource Path** at the Resources path, or assign a prefab as the local prefab.

`Assets/_Project/Models/` is included in the same importer, so a character dropped there is treated as Humanoid too.

## Unrigged meshes

An AI mesh with no skeleton will not map. This project does not auto-rig it. Run it through Mixamo, AccuRIG, or Blender first, export a Humanoid FBX, then import that file. That pipeline is [JUH-25](https://linear.app/juhani-mustola/issue/JUH-25), separate from this importer.

## Asuna (local only)

[Free Test Character Asuna](https://assetstore.unity.com/packages/3d/characters/humanoids/sci-fi/free-test-character-asuna-205897) by MSGDI is the local avatar. It is Humanoid, has 52 ARKit blendshapes, and ships with URP materials. The Asset Store EULA does not allow committing it to this public repo. It is gitignored.

1. Open this project in Unity **6000.6.6f1** and wait until the Universal RP and UniVRM packages finish importing.
2. Download Asuna from the Asset Store and import the `.unitypackage` (**Assets > Import Package > Custom Package**, or Package Manager if it is in your account).
3. If the imported root is not already gitignored, drag that folder onto `Assets/LocalAvatars` **in the Project window**. Moving it in the Editor keeps the prefab GUIDs. Likely roots that are already ignored: `Assets/Asuna`, `Assets/MSGDI`, `Assets/FreeTestCharacterAsuna`, and `Assets/Free Test Character Asuna`.
4. Select `Assets/_Project/Settings/AvatarSelection.asset`.
5. Drag the Asuna character prefab into **Local Prefab**. Leave **Preference** on `Auto`. Auto prefers that prefab over the CC0 samples.
6. The character FBX should already be Humanoid. If the Console reports missing bones, open the FBX, set **Rig > Animation Type** to **Humanoid**, and apply.
7. Enter Play mode. The capsule hides, Asuna is in the window, and the Console lists the ARKit shapes (`jawOpen` as `aa`, both `eyeBlink*` as `blink`, `mouthSmile*` as `joy`, and so on).
8. Do not commit the Asuna folder, its `.meta` files, or a change that only stores the Asuna prefab GUID if you want other clones to stay on the CC0 sample. A committed missing prefab reference is treated as empty and the sample is used, so CI still runs.

Asuna's animation FBX files are Humanoid clips. They are not wired to an Animator controller yet. Any Humanoid clip can retarget onto this avatar and onto the CC0 samples once a controller exists.

## Samples and licence

`CompanionSample.vrm` and `CompanionSample.fbx` are an original low-poly humanoid made for this repo, dedicated under **CC0 1.0**. The licence file sits next to each copy:

- `Assets/StreamingAssets/Avatars/LICENSE.txt`
- `Assets/_Project/Avatars/Resources/Avatars/LICENSE.txt`

They are not Mixamo, VRoid Hub, or Asset Store models. The VRM is VRM 1.0. Both files include VRM-style names (`Aa`, `Blink`, `Joy`) and a few ARKit names (`jawOpen`, `eyeBlinkLeft`, `mouthSmileLeft`).

## Transparency

The project uses **URP 17.5.0** (the Universal RP release for Unity 6000.6). The built-in pipeline cannot show Asuna's materials on Unity 6.

These settings keep the desktop visible around the model:

- URP asset: **HDR off**, **MSAA disabled**, depth and opaque textures off, no extra renderer features.
- Renderer: **Intermediate Texture** Auto, so a camera with post-processing off draws straight to the framebuffer.
- Camera: solid clear color `(0, 0, 0, 0)`, HDR off, MSAA off, post-processing off, HDR output off. `UniversalAdditionalCameraData` is on `Main Camera`.
- Player: **Preserve Framebuffer Alpha** on, **DXGI Flip Model** off, Windows graphics API Direct3D11. See [OVERLAY.md](OVERLAY.md).

MToon 1.0 (UniVRM's URP shader) writes alpha 1 for opaque materials and the texture alpha for blended hair, with alpha blend-op Max, so the body stays solid and the clear color shows through elsewhere. URP Lit and Unlit opaque materials also write alpha 1. A material whose color alpha is 0 is lifted to 1 so it does not punch a hole in the window.

If the built player is a solid rectangle, check **Support HDR** on `UniversalRenderPipelineAsset` is off and the camera's **Post Processing** is off, then rebuild.

## What to check

1. Play mode shows the CC0 VRM (or Asuna, if the local prefab is assigned) instead of the capsule. The Console has an `Avatar ready` line with `humanoid=True` and the mapped shapes.
2. Press **F7**. The other sample appears. Press **F7** again to return. Clearing **Local Prefab** and both sample paths brings the capsule back.
3. Build the Windows player (`unity command build_windows_player`, then poll `build_status`) and run `Builds/Windows/AICompanion.exe`. The desktop shows around the model. Drag the model. Click empty window space and the click reaches the window behind. **F8** still toggles topmost. The frame-rate label still drops to about 15 after the pointer leaves for about 3 seconds.
