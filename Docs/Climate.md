# Climate manager

`Assets/_Project/Scripts/Gameplay/Climate/ClimateManager.cs` tracks a temperature between 0 and 2 degrees, current carbon points, and the number of active fires. Each fire adds 0.01 degrees per second by default. The slider and labels refresh in Update; temperature text uses an invariant decimal point, for example `+1.25°C`. CP uses the existing HUD's `1,000 CP` format.

The component integrates with this project's current systems. It finds TaskRewardManager if Rewards is unassigned, mirrors its balance in currentCP, and spends through that manager so task rewards, the existing CP HUD, and eco-building purchases use the same wallet. With no reward manager present, currentCP is the balance used directly. Set starting CP on TaskRewardManager when using the shared wallet.

Track Scene Fires is enabled by default. Ignition/extinguishing events update the count, and enabling the component discovers already-burning objects. Duplicate ignition notifications are ignored. Turn Track Scene Fires off before Play if another system will call AddFire and RemoveFire manually. RemoveFire never lets the count go below zero.

## Manual editor setup

1. Create an empty **Climate** under Managers and add **Climate Manager**.
2. In the HUD Canvas, create a **UI > Slider** and a **UI > Text - TextMeshPro** temperature label. Assign them to **Temperature Slider** and **Temperature Text**. The slider is configured at runtime with Min 0, Max 2, Whole Numbers off, and Interactable off.
3. Assign the existing **CarbonPointsText** to **CP Text**. Its existing CarbonPointsHud component can keep displaying the same shared wallet value.
4. Assign **TaskRewards** to **Rewards**, or leave it empty for runtime discovery. Leave **Track Scene Fires** enabled for the existing fire system.
5. Choose the initial **Current Temperature** and save the scene. Existing CP comes from TaskRewards. In Play mode, fires should raise temperature and putting them out should lower the active count; temperature itself drops when an eco-building purchase is made.
6. Add a **Panel Manager** to the scene. On the game-over UI root, add **Set Active Panel**, keep the object inactive, and assign it to **Climate Manager > Game Over Panel**. Use **Game Over Panel Id = GameOver**. ClimateManager registers this inactive panel with PanelService and unregisters it when destroyed.

From a building's successful purchase action, call:

```csharp
climateManager.SpendCPForEcoBuilding(100, 0.25f);
```

With at least 100 CP, this deducts 100 points and immediately reduces the temperature by 0.25, clamped at zero. Insufficient funds change neither value. Negative costs/reductions and non-finite reductions are rejected. A standard Button.onClick cannot directly supply both arguments; call this method from the building/upgrade component's button handler.

At 2 degrees, GameOver logs once and latches IsGameOver. Further climate progression and purchases stop. It switches the application to **GameState.GameOver** and shows the registered **GameOver** panel through PanelService. Gameplay input, water spraying, fire progression, traffic, and fire ambience follow their existing non-Gameplay behavior. Pause/Back input cannot switch a finished game back to Gameplay.

`DemoScene` uses the existing **FireWarningHUD > EndScreen** as this panel, with a **Game Over** title. It starts hidden, and the temperature slider/text and shared CP references are assigned on ClimateManager. New GameState enum values were appended, preserving previous serialized values. A new scene/component instance starts with a fresh climate latch.

The panel and its dimmed background stretch across the Canvas, with the title centered above the gameplay view. `Artifacts/ClimateGameOver/GameOver.png` shows the +2.00°C loss overlay in Play mode.

The climate simulation respects GameManager's Gameplay state; pausing or visiting menus does not warm the city. It also works in a simple test scene without a GameManager. The UI and shared CP balance can continue refreshing while the simulation is paused.

## Verification

The full gameplay assembly compiled against the project's Unity 6000.3.18f1 package references. A live-fire Play-mode test started at 1.999 degrees and crossed the limit through normal Update warming. The screen stayed hidden below 2, opened once at exactly 2, displayed +2.00°C, and entered GameOver state. Repeated updates did not reopen it; Back/Pause input did not resume Gameplay, and an eco purchase could not reduce temperature after the game ended.
