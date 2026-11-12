# Submission check

Read-only review of the saved project and Windows output on 2026-11-01. Unsaved Editor changes may differ.

## Fix before the next build

1. **Upgrade site definitions:** SolarSite01, Recycling site, and Tree site all reference the Solar Panel definition in the saved scene. Keep SolarSite01 as Solar Panel. Change **Recycling site > Eco Upgrade Site > Upgrade** to **Recycling**, and **Tree site > Upgrade** to **Afforestation**. Their Built Visual references are already different. Without these changes the recycling/tree buttons have no matching location, while solar purchases can reveal the wrong model.
2. **Timer display:** ClimateManager has Enable Survival Goal enabled and Survival Duration = 180, but Round Timer Text is unassigned. The countdown/win rule runs without a label. Add a UI TextMeshPro under GameScreen and assign it so players can see the goal. Warming Rate Text is a separate optional label.
3. **Rebuild after saving:** The latest Editor log reports a successful Windows build at 22:11, but the exported Build/Sastermate_Data/Managed/Assembly-CSharp.dll does not contain the new ResumeGame method name. The source Close/Escape fix was compiled separately during this session. Save the scene and rebuild to include the latest code and wiring.

## Package the correct output

The existing Build directory mixes the old game-up-game-jam.exe / game-up-game-jam_Data with the newer Sastermate.exe / Sastermate_Data, plus shared runtime files. Use a fresh output folder for the final build. Zip its complete runtime contents together; do not upload an exe alone. Leave the *_BurstDebugInformation_DoNotShip folders out of the submission. Include any generated D3D12, MonoBleedingEdge, Data, and runtime DLL folders/files from that fresh build.

Extract the final ZIP into a different folder and launch that extracted executable once. Check menu -> Play, movement/aim/water, each upgrade type, Close and Escape restoring input, a finished round, end-screen sound, Replay, and Quit. Verify the HUD and buttons fit the actual game window. A successful compiler/build does not prove these native interactions work in the exported player.

## Confirmed in the saved setup

- DemoScene is enabled in EditorBuildSettings and GameManager starts in MainMenu.
- LoseScreen is mapped to GameOver, WinScreen to GameWon, and the upgrade overlay is manual.
- Win/Lose SoundData references are assigned on GameAudioPresenter; the project audio setting is enabled.
- Fredoka Regular/SemiBold SDF assets were generated and saved scene text uses the new regular font.
- Fredoka's license notice is included in StreamingAssets/ThirdPartyNotices for the build.
- The intended round duration is 180 seconds, with the gentler first-minute fire schedule in code.

The Editor font command logged characters not preloaded: ĞğİŞş. If Turkish TMP text is shown, inspect those characters in the final player; the font-file cmap check alone does not verify native TMP rendering. This is not evidence of a problem with the English timer/button strings.

## Explain the game to judges

Supply a short player-facing description rather than only the draft GDD. The implemented keyboard/mouse controls are WASD or arrows to move, mouse position to aim, left mouse to spray, Q/E to rotate the camera, and the Upgrades button to buy infrastructure. Explain the goal: survive 3 minutes below +2 degrees, earn CP by extinguishing fires, and buy upgrades to slow warming. See PlayerInstructions.txt for a short copyable version.

Use the jam's current required submission format/template and check that judges can access the download link without your account. The repository README contains a supplied draft delivery instruction, not an independently verified current organizer rule.
