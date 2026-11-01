# Canvas panels

`CanvasPanel` reuses VectorViolet's `PanelBase`, as The First Planet does. Show/Hide changes the CanvasGroup's alpha, interactable, and blocksRaycasts values. Its GameObject stays active, so hiding a panel does not interrupt its listeners or coroutines. Calls made before the panel's first Awake are supported.

`GamePanelPresenter` follows the state-to-panel configuration used in Soulbonk. It registers its configured panels with `PanelService`, reconciles the current state when enabled, and closes outgoing panels before opening those configured for the next state. Visibility requests do not change the game state. No tween dependency or fade animation is required.

## Editor wiring

1. Keep the existing **PanelManager** object/component. Create a separate empty **GamePanels** under the scene's Canvas, outside the panels it controls, and add **Game Panel Presenter** to it. The presenter belongs to the scene so it receives fresh panel references after a scene reload; the existing persistent PanelManager only owns the registry.
2. On **EndScreen**, remove **Set Active Panel** and add **Canvas Panel**. Its required **Canvas Group** is added automatically. Keep the EndScreen GameObject **active**, set **Panel Id = GameOver**, and set its CanvasGroup to **Alpha = 0**, **Interactable off**, **Blocks Raycasts off**, **Ignore Parent Groups off**.
3. Do the same on **MenuScreen**, using **Panel Id = MainMenu**. Leave the GameObject active and its CanvasGroup hidden initially.
4. In **Game Panel Presenter > Panels**, add these entries:

| Panel | Visible In States |
| --- | --- |
| EndScreen's CanvasPanel | GameOver |
| MenuScreen's CanvasPanel | MainMenu |

5. Save the scene. Complete this wiring before testing Game Over: ClimateManager now changes the application state, and GamePanelPresenter owns the screen visibility. The old Game Over Panel fields were removed from ClimateManager.

GameManager's Startup State defaults to Gameplay. Set it to MainMenu for menu-first startup and wire the menu Play button to GameManager.StartGame(). The menu appears when the GameManager enters MainMenu; the loss screen appears at +2 degrees through GameOver. Future pause panels can use Paused in their Visible In States array. See [MenuCamera.md](MenuCamera.md) to add an overhead city background.

For a state-controlled HUD, create a full-stretch **GameplayHUD** RectTransform under the Canvas and move only gameplay widgets/marker containers into it. Add CanvasPanel with **Panel Id = GameplayHUD**, then add a presenter entry for Gameplay and Paused (plus Interaction/Cutscene if those states should retain your HUD). Keep MenuScreen and EndScreen outside this HUD group so a hidden parent does not also hide those screens.

Buttons can call the presenter's **ShowPanel(string)**, **HidePanel(string)**, **TogglePanel(string)**, or **ShowExclusivePanel(string)** methods with the relevant Panel Id. ShowExclusivePanel hides all other registered panels. Ordinary game state changes reapply the configured visibility, so persistent menus should follow the application's game states.

For a button-controlled overlay such as **UpgradeScreen** (Panel Id **UpgradePanel**), keep it in the presenter's list with **Visible In States empty** and author its CanvasGroup hidden. Wire an Upgrades button in GameScreen to **Game Panel Presenter.ShowPanel("UpgradePanel")**, and a Close button inside UpgradeScreen to **HidePanel("UpgradePanel")**. It starts hidden, opens on demand, and closes on any game-state change. See [ClimateUpgrades.md](ClimateUpgrades.md) for the exact scene setup and world build sites.

From code, the existing shared API remains available:

```csharp
PanelService.Current?.Show("Settings");
PanelService.Current?.Hide("Settings");
```

Keep the presenter outside any panel it controls and off persistent manager objects. Panels should contain Canvas UI; CanvasGroup does not control world meshes or SpriteRenderers.

## Game-over Replay button

For the separate **WinScreen** and **LoseScreen**, see [GameResults.md](GameResults.md). Map LoseScreen to GameOver and WinScreen to GameWon; Replay works from either state. Earlier EndScreen examples in this document describe the original single-screen setup.

Outside Play mode, select the Replay button under EndScreen. In **Button > On Click()**, add the scene's **GameManager** object and choose **GameManager > Replay()**. Remove any old StartGame or direct panel-visibility callback on that button. Keep its Button Interactable enabled. EndScreen must be a configured CanvasPanel visible in GameOver, as above.

Save DemoScene and include it in the active Build Profile's **Scene List**. Replay reloads the current scene, resets scene-owned CP, temperature, fires, traffic, and upgrades to their authored starting values, restores time scale to 1, and enters Gameplay directly. A persistent GameManager also receives the fresh Gameplay state after loading. Repeated Replay clicks during loading are ignored. Game Audio Presenter reacquires a destroyed player reference after scene loading so water audio still follows the new player.

## Checks after wiring

- Game Over is hidden initially; all panel GameObjects remain active.
- At +2 degrees, EndScreen has alpha 1 and accepts UI input.
- Hidden panels have alpha 0, interactable false, and blocksRaycasts false.
- Entering a different state hides outgoing panels, including after disabling/re-enabling the presenter.
- Pause/Back still cannot resume a finished game.
