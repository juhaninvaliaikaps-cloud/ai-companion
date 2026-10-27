# Avatars

The overlay shows one humanoid at a time. `AvatarMount` loads it and hides the pink capsule while a model is showing. Drag and click-through follow the rendered silhouette (the framebuffer pixel under the cursor), so the empty space around the mesh stays click-through. The capsule comes back if nothing loads.

`Assets/_Project/Settings/AvatarSelection.asset` chooses the model. Preference `Auto` uses the first source that exists:

1. **Local prefab.** A reference you assign on this machine. This is where Asuna goes.
2. **VRM sample.** `Assets/StreamingAssets/Avatars/CompanionSample.vrm`, loaded at runtime with UniVRM (`Vrm10.LoadPathAsync`, including VRM 0.x migration).
3. **FBX sample.** `Assets/_Project/Avatars/Resources/Avatars/CompanionSample.fbx`, loaded with `Resources.Load`.
4. **Capsule.** The placeholder already in `Main`.

Press **F7** to cycle the local prefab, the VRM sample, and the FBX sample. **F8** is still the always-on-top toggle.

The console logs one line when a model is ready, for example `Avatar ready: CompanionSample humanoid=True shapes=20 aa=Aa,jawOpen ...`. That line is the blendshape list and the expression map. Code can read the same data from `IAvatarDescriptorService` (`TryGetBlendShapeNames`, `TryGetExpression`).

## Expression names

`ExpressionPresetMap` maps blendshape names onto `Aa`, `Ih`, `Ou`, `Ee`, `Oh`, `Blink`, `Joy`, `Angry`, `Sorrow`, `Fun`, and `Surprised`.

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
| Surprised | `surprised`, `browInnerUp`, `browOuterUp*`, `eyeWide*` |

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

Asuna's own animation FBX files stay on your machine. Do not commit them. The shared controller below retargets onto her Humanoid rig and onto the CC0 samples.

## Samples and licence

`CompanionSample.vrm` and `CompanionSample.fbx` are an original low-poly humanoid made for this repo, dedicated under **CC0 1.0**. The licence file sits next to each copy:

- `Assets/StreamingAssets/Avatars/LICENSE.txt`
- `Assets/_Project/Avatars/Resources/Avatars/LICENSE.txt`

They are not Mixamo, VRoid Hub, or Asset Store models. The VRM is VRM 1.0. Both files include VRM-style names (`Aa`, `Blink`, `Joy`) and a few ARKit names (`jawOpen`, `eyeBlinkLeft`, `mouthSmileLeft`).

## Transparency

The project uses **URP 17.6.0**, the Universal RP version bundled with Unity 6000.6.6f1. The built-in pipeline cannot show Asuna's materials on Unity 6.

These settings keep the desktop visible around the model:

- URP asset: **HDR off**, **MSAA disabled**, depth and opaque textures off, no extra renderer features.
- Renderer: **Intermediate Texture** Auto, so a camera with post-processing off draws straight to the framebuffer.
- Camera: solid clear color `(0, 0, 0, 0)`, HDR off, MSAA off, post-processing off, HDR output off. `UniversalAdditionalCameraData` is on `Main Camera`.
- Player: **Preserve Framebuffer Alpha** on, **DXGI Flip Model** off, Windows graphics API Direct3D11. See [OVERLAY.md](OVERLAY.md).

MToon 1.0 (UniVRM's URP shader) writes alpha 1 for opaque materials and the texture alpha for blended hair, with alpha blend-op Max, so the body stays solid and the clear color shows through elsewhere. URP Lit and Unlit opaque materials also write alpha 1. A material whose color alpha is 0 is lifted to 1 so it does not punch a hole in the window.

If the built player is a solid rectangle, check **Support HDR** on `UniversalRenderPipelineAsset` is off and the camera's **Post Processing** is off, then rebuild.

## Animator

`AvatarAnimationController` on `AppBootstrap` plays one Animator Controller on the humanoid that is showing: the CC0 VRM, the CC0 FBX, or a local model such as Asuna. The capsule has no rig. The debug keys still change the label, and no body clip plays.

The controller is `Assets/_Project/Animations/Companion.controller`.

| State | When it plays |
| --- | --- |
| Idle | Default. Emotion parameter `0` (Neutral). Loops. |
| Happy, Sad, Surprised, Thinking | Emotion parameter `1` to `4`. The crossfade is 0.3 seconds. |
| Talk | Additive layer while the `Talking` bool is true, so an emotion keeps playing underneath. |

`IAvatarAnimator.SetTalking(bool)` and `SetEmotion(Emotion, intensity)` write `Talking`, `Emotion`, and `EmotionIntensity` (0 to 1). Root motion is off. The Animator stays on Normal update mode, so the clips keep advancing while the overlay is throttled to 15 fps. The motion looks a little choppier at that rate.

The overlay camera starts at `(0, 1, -3.2)` and looks along +Z, then each frame moves so the whole body fits the window. A humanoid whose chest faces +Z therefore shows its back until spawn. After the controller is bound, the root yaws so the body faces that camera. Body forward comes from the hips, spine, and upper legs when those bones exist, and from the root's forward otherwise. The yaw is on the model root. Root motion stays off, so the yaw remains while the clips play. The fit uses the posed humanoid bones, so a clip or a yaw does not push the head, hands, or feet outside the 480×720 window.

Right-click a visible pixel to turn on rotate mode. The label reads `Rotate: on (right-click to finish)`. Drag horizontally with the left button to yaw around world up. Right-click her again, or press **Esc**, to leave the mode. While the mode is off, left-drag still moves the window (on her or on the debug label). Middle-click a visible pixel, or press **R** while rotate mode is on, clears the extra yaw and she faces the camera again. Middle-click also clears the camera zoom back to the full-body fit. Scroll over a visible pixel to zoom toward that point. The close zoom stops before the camera enters her meshes, and skinned renderers stay in view so hair and the body do not cull away. Hold Ctrl and scroll to scale her. The chat panel and the debug labels stay the same size. The window grows only when she would otherwise be clipped. The size and the zoom are saved. See [OVERLAY.md](OVERLAY.md). The extra yaw is saved per source (local prefab, VRM, FBX) and restored on the next launch. **F7** loads the other source's own yaw. **1–5**, **T**, **F7**, and **F8** stay on their own keys. Right-click toggles only on visible pixels. Empty pixels stay click-through.

The clips are original Humanoid muscle curves, released under CC0. The licence file is `Assets/_Project/Animations/LICENSE.txt`. They are not Mixamo, not Quaternius, and not Asuna's Asset Store animations. To swap one later, replace the `.anim` or drop another Humanoid clip onto that state. Leave third-party clips whose licence does not allow redistribution out of this public repository. If a character stays on the bind pose after import, use **AI Companion > Rebuild Basic Animator** once. That writes the same curves through the Unity animation API.

The face uses the expression map. Happy drives Joy (`mouthSmile*`). Sad drives Sorrow (`mouthFrown*`) and Angry (`browDown*`). Surprised drives `browInnerUp`, `browOuterUp*`, and `eyeWide*`, plus a little jaw. Thinking presses the mouth closed a bit. While TTS audio is playing, lip sync replaces the mouth shapes (jaw, and a small vowel accent). It does not add them on top of the emotion mouth, and it does not drive `mouthClose` while the jaw is open. Smiles, frowns, and brows stay on the emotion. Pressing **T** with no audio still plays the old procedural jaw. Weights for emotions ease in over about 0.3 seconds. The mouth itself follows the audio with a short attack and a slightly slower release. A model with no blendshapes and no jaw bone keeps the body clips and skips the face.

## Lip sync

`LipSyncDriver` on **AppBootstrap** reads the `AudioSource` that `VoiceSpeaker` is playing (`GetOutputData` and `GetSpectrumData`). It does not read the reply text.

When the avatar is ready the Console logs one line that starts with `LipSync:`:

| Line | Target |
| --- | --- |
| `LipSync: VRM expressions Aa, Ih, ...` | UniVRM expression weights (A/I/U/E/O), or a VRM 0.x blendshape proxy if that is what the model has. |
| `LipSync: blendshapes jawOpen, mouthFunnel, ...` | Mouth and jaw blendshapes found by name. Asuna's ARKit set (`jawOpen`, `mouthClose`, `mouthFunnel`, `mouthPucker`, `mouthStretch*`) uses this. |
| `LipSync: jaw bone Jaw` | No mouth shapes. The jaw bone pitches a few degrees. A full bone face is JUH-26. |
| `LipSync: none` | Nothing to drive. |

VRM wins over blendshapes, then the jaw bone. `ILipSyncTarget` is the plug-in for a later VRoid or Daz mouth: call `LipSyncDriver.SetTarget`. The tunables live on the **Lip Sync Driver** component and are logged once on the `LipSync:` line. Defaults are strength `0.55`, sensitivity `3`, max jaw open `0.40`, vowel amount `0.20`, attack `0.04` s, release `0.16` s, viseme hold `0.08` s, silence `0.012`, jaw pitch `-12` degrees. The same caps apply to named blendshapes, VRM A/I/U/E/O, and the jaw bone. Loud samples pass through a soft knee, so a normal syllable stays a modest jaw (about 0.15 to 0.40) instead of driving `jawOpen` to 1. Funnel and pucker stay at or below vowel amount. `mouthStretch` is a quarter of that, and 0 vowel amount leaves only the jaw.

If the mouth is still too wide, lower **Strength** or **Max Jaw Open**. If the lips still warp, lower **Vowel Amount**. If quiet speech barely moves, raise **Sensitivity** (4 or 5; 8 is hot). If the vowels flicker, raise **Viseme Hold**. These sliders apply during Play mode.

With the window focused, **1** is Neutral, **2** Happy, **3** Sad, **4** Surprised, **5** Thinking, and **T** toggles talking. The keypad numbers do the same. The debug label shows the emotion, the intensity, and whether talk is on. **F7** and **F8** are unchanged.

## What to check

1. Play mode shows the CC0 VRM (or Asuna, if the local prefab is assigned) instead of the capsule. The Console has an `Avatar ready` line with `humanoid=True` and the mapped shapes.
2. Press **F7**. The other sample appears. Press **F7** again to return. Clearing **Local Prefab** and both sample paths brings the capsule back.
3. Build the Windows player (`unity command build_windows_player`, then poll `build_status`) and run `Builds/Windows/AICompanion.exe`. The desktop shows around the model. Drag her body, not the empty space beside it. Clicks in that empty space, close to the mesh as well as farther away, reach the window behind. **F8** still toggles topmost. **F7** still cycles avatars. The frame-rate label still drops to about 15 after the pointer leaves for about 3 seconds.
4. With Asuna focused, Idle is already playing. Press **2**, **3**, **4**, **5**, then **1**. The body eases between those poses, and her face smiles, frowns and lowers the brows, raises the brows, then returns to neutral. Press **T**. The mouth moves and the emotion stays. Press **T** again to stop. Move the pointer off the window until the label says about 15 fps. She should still be moving, a bit choppier.
5. Press **F7** until a CC0 sample is showing. The same controller and the same keys apply. Empty space around the mesh still clicks through. Drag, **F8**, and the frame-rate drop still work. If a model has no blendshapes, the body still plays and the Console has no errors. The `LipSync:` line then says `jaw bone` or `none`.
6. Send a short spoken line (see [VOICE.md](VOICE.md)). Her mouth opens and closes with the audio and is shut again shortly after it ends. Happy or surprised can stay on the brows and the smile while the mouth follows the words. The Console has one `LipSync:` line. Paste that line if the mouth does not move.
7. Asuna and each CC0 sample should spawn facing the camera (face visible), with hair and clothes on. Right-click her body. The label says `Rotate: on (right-click to finish)`. Drag horizontally. She yaws, and the window stays put. Right-click again, or press **Esc**. Left-drag moves the window again. Quit and relaunch. That source comes back at the yaw you left. **F7** to another source uses that source's own yaw. Middle-click her, or press **R** in rotate mode, and she faces the camera again. Middle-click also zooms back out to the full body. Scroll over her to zoom in. Her torso stays visible with the boots, then scroll out or middle-click until her feet, hair, and the top of her head are all inside the window. Hold Ctrl and scroll over her. She changes size, the `Size` label updates, and the chat panel stays the same pixel size. Empty pixels still click through, and the wheel does nothing there. At about 15 fps she is still moving.
