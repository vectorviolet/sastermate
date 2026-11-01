# Win and lose screens

The game now has distinct terminal states. Existing serialized enum values are preserved: **GameOver = 5** is the loss state, and **GameWon = 6** is victory. GameManager's **HasWon**, **HasLost**, and **IsGameOver** properties distinguish the result while allowing common end-of-run behavior.

**Climate Manager reaching +2.00 degrees automatically calls GameManager.LoseGame().** The game's success objective should call **GameManager.WinGame()**. There was no existing victory condition in the project when these screens were split. Neither method finishes an inactive MainMenu, and the first result wins: a subsequent WinGame/LoseGame call cannot replace a completed run's outcome.

## Manual editor wiring

The saved scene already contains **LoseScreen** (CanvasPanel Id **LoseScreen**) and **WinScreen** (Id **WinScreen**). LoseScreen is already assigned to the presenter's GameOver entry. WinScreen needs its own entry.

1. Outside Play mode, select **GamePanels > Game Panel Presenter**.
2. Keep **LoseScreen > Visible In States = GameOver only**.
3. Add a Panels entry, drag **WinScreen's CanvasPanel** into its Panel field, and set **Visible In States = GameWon only**.
4. Keep both screen GameObjects active and their CanvasGroups initially **Alpha = 0, Interactable off, Blocks Raycasts off**. Keep the panel IDs unique as above. The presenter owns their runtime alpha and interaction, so it also hides the gameplay HUD and upgrade overlay on either result.
5. On both end screens, wire **Play Again/Replay > On Click()** to **GameManager.Replay()**. Both Replay callbacks were already present in the saved scene. Save the scene and keep it in the active Build Profile's Scene List.

The game remains stopped after either outcome: gameplay input, water, fires, traffic, and fire ambience use the existing non-Gameplay behavior. Climate progression and eco purchases are blocked, including the legacy instant-cooling purchase method. Pause/Back cannot resume a finished run. Replay works from both outcomes and reloads the level directly into Gameplay with a fresh climate, task wallet, and build sites.

To integrate a mission or timer that already knows its result:

```csharp
GameManager.Instance.WinGame();  // successful objective
GameManager.Instance.LoseGame(); // failed objective (temperature already calls this)
```

These calls set the game state; the presenter selects the screen. No scene panel references belong on the persistent GameManager.
