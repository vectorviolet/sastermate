# Game audio

SoundManager holds the sound library and playback sources; adding a SoundData to its lists does not start playback. GameAudioPresenter now requests MenuMusic for MainMenu and GameplayMusic for Gameplay. Pause and other overlays retain the current track. Returning from pause does not restart it.

The Click SoundData previously referenced mmm.wav (menu music) with Loop enabled. It now references click.wav and plays once. The UI and water playback calls use the shared service's 2D overloads, so camera height does not attenuate them.

## Editor wiring

1. Keep the existing active **SoundManager** and its Music/SFX mixer assignments. Add **Game Audio Presenter** to the scene's **UIController/GamePanels** object, outside the panels it controls.
2. Assign the SoundData assets from `Assets/_Project/Audio/Data`: **Menu Music = MenuMusic**, **Gameplay Music = GameplayMusic**, **Click Sound = Click**, and **Water Loop = WaterStream**.
3. Assign the player's existing **PlayerWaterSpray** component (on OutPoint) to **Water Spray**. The loop starts while IsSpraying is true and stops on release, leaving Gameplay, disabling the presenter, or losing application focus.
4. For each button that should click, add an On Click entry targeting **Game Audio Presenter.PlayClick()**. On the menu Play button, also assign **GameManager.StartGame()** so it enters Gameplay. These listeners were absent from the saved scene when it was inspected.
5. Enter Play with **Startup State = MainMenu**. MenuMusic should start; pressing the menu Play button should switch to GameplayMusic. Hold LMB to hear water. Keep **Game view > Mute Audio** off and one enabled AudioListener on the output MainCamera.

FireAmbienceController remains responsible for fire sounds. It is intentionally silent outside Gameplay and beyond its six-unit player hearing radius. FireExtinguishAudio plays only after water completely extinguishes an object. Both retain their existing references and SFX mixer routing.

The saved project has audio enabled, an enabled listener in the camera prefab, and no saved Music/SFX mixer mutes. If playback sources report Is Playing but remain inaudible, inspect the live mixer levels, listener state, Game view mute, and operating-system output. These runtime states were not available through the disconnected Unity tool session.
