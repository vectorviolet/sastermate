# Cartoon UI font

Fredoka Regular is used for body/button text and Fredoka SemiBold for objects named Title, Heading, or Header (including descendants). Its rounded forms suit the game's flat cartoon presentation. The Google Fonts source is [Fredoka](https://github.com/google/fonts/tree/main/ofl/fredoka), licensed under [SIL Open Font License 1.1](https://github.com/google/fonts/blob/main/ofl/fredoka/OFL.txt), which permits bundling with commercial software while retaining the notice/license.

The files in `Assets/_Project/UI/Fonts/Fredoka` are static Regular (400) and SemiBold (600) TrueType instances generated from the upstream variable font at width 100. Their outlines are unchanged; the variable axes were resolved for reliable Unity importing. The source hash, instance hashes, and Turkish/HUD glyph checks are recorded in `Artifacts/CartoonFonts/Source.json`. Both contain `ÇçĞğİıÖöŞşÜü`, digits, punctuation, and the degree symbol used by the temperature HUD. The original OFL notice is included next to the fonts and in StreamingAssets/ThirdPartyNotices so it accompanies builds.

## Apply in Unity

1. Stop Play mode and let Unity import/compile.
2. Run **Tools > Sastermate > Apply Cartoon UI Fonts**.
3. Save the open scene with Ctrl+S.

The command creates dynamic TMP SDF font assets with preloaded English/Turkish/HUD characters, material and atlas subassets, multi-atlas support, and the existing LiberationSans asset as a symbol fallback. It assigns the new fonts/materials to TMP text in open scenes and saves project-owned UI prefabs under `Assets/_Project/UI`. Package/plugin prefab widgets are excluded. Existing labels such as **Buy**, font sizes, colors, and alignment are preserved. The new default font materials replace the old font materials so they point at the correct atlas.

Open-scene changes support Undo and are marked dirty for your review/save. Prefab/font asset changes are saved by the command. It reuses already-created font assets on subsequent runs. The operation runs only when you invoke the menu outside Play mode; downloading the font sources alone does not change scene fonts.

Unity's native FontEngine must generate the TMP atlas, so the menu command has been compiled but not executed in the disconnected editor session. Check the tooltip, CP floating text, countdown, buttons, and win/lose screens in Game view after applying it. Text baked into an image is independent of TMP fonts.
