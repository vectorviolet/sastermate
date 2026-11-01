# Carbon points and task rewards

Tasks publish a successful completion with a `TaskDefinition`, a unique instance ID, and an optional world position. `TaskRewardManager` awards the asset's carbon-point value once for that instance and notifies the HUD. The shared `CarbonPointLedger` tracks the balance and prevents duplicate rewards. Future task types use the same completion event.

The fire controller now takes an **Extinguish Task** asset. Each ignition creates a new task instance, including later fires on the same building. Completing the required amount of water clears the fire and completes that task. Partial watering, disabling/destroying a burning object, `ExtinguishAll`, and leaving Play mode do not complete a task or award points. The existing fire-extinguished event still cleans up the VFX and warning icons in all these cases.

## Manual Unity setup

Use these steps when setting up the reward manager and balance label in another scene.

1. Return to Unity and let the scripts compile. If needed, choose **Assets > Refresh**.
2. In the Project window, create a folder such as `Assets/_Project/Tasks`. Inside it, right-click and choose **Create > Climate Guardians > Tasks > Task Definition**. Name the asset **ExtinguishFire**, set **Display Name** to **Extinguish Fire**, and set **Carbon Points** to **100**.
3. Under the scene's **Managers** object, create an empty GameObject named **TaskRewards**. Add the **Task Reward Manager** component. Leave **Starting Carbon Points** at **0**. Keep one active reward manager in the scene.
4. Select **Random Fires**. In **Random Fire Controller > Task Rewards**, drag **ExtinguishFire** into **Extinguish Task**. This covers all targets managed by that controller; individual buildings need no new setup.
5. Under the existing **FireWarningHUD** Canvas, create **UI > Text - TextMeshPro**. Import TMP Essentials if Unity prompts for them. Rename the text **CarbonPointsText**.
6. Set its RectTransform anchors and pivot to the **top left** (`Anchor Min/Max = (0, 1)`, `Pivot = (0, 1)`). Use **Pos X = 24**, **Pos Y = -24**, **Width = 300**, and **Height = 70**. Set the initial text to **CP: 0**, font size **36**, and choose a readable color/outline. Disable the text's **Raycast Target**.
7. Add **Carbon Points Hud** to **CarbonPointsText**. Drag the **TaskRewards** GameObject into its **Rewards** field. Keep this component on the text object itself.
8. Save the scene. Enter Play mode, extinguish a fire with the hose, and verify **CP: 0 → CP: 100**. A second successfully extinguished fire should show **CP: 200**. Partial watering and clearing a fire by disabling its object must leave the balance unchanged.

The balance belongs to this reward manager's scene session. Starting a new Play session or recreating the manager starts from Starting Carbon Points; disk persistence and upgrade purchases are separate follow-up work. `TrySpendCarbonPoints(amount)` is available for future upgrades and refuses negative costs or insufficient funds.

## Floating rewards

`DemoScene` includes **FloatingCarbonPointsHUD** under the existing **FireWarningHUD** Canvas. Successfully extinguishing a fire displays green, dark-outlined text such as **+100 CP** above that object's fire position. The label rises 64 Canvas reference pixels and fades over 1.3 seconds. It follows the object's projected world position as the camera moves, with a consistent screen-space text size.

The amount comes from the task's actual credited reward. `BurnableObject` supplies its fire position with the completion; after the ledger accepts that unique task, `TaskRewardManager` publishes `OnCarbonPointsAwarded`. Duplicate completions, zero-value rewards, partial water application, object cleanup, and CP spending do not spawn text. Completing a task without a world position still awards CP and updates the balance, but does not show an object popup.

Assets:

- `Assets/_Project/UI/Prefabs/FloatingCarbonPointsHUD.prefab`: the shared controller; place it under a Canvas in other scenes.
- `Assets/_Project/UI/Prefabs/FloatingCPText.prefab`: font, size, color, and alignment; its Raycast Target is disabled.
- `Assets/_Project/UI/Materials/FloatingCPText.mat`: the shared TMP outline material.

Tune **Lifetime**, **Rise Distance**, **World Height Offset**, and **Fade Start** on the HUD controller. It prewarms three text instances and reuses them; simultaneous popups are capped at twelve. Each popup stores a position and stays under the HUD, so disabling or deleting the original object cannot cause a reparenting error. Popups hide and freeze outside Gameplay, and off-screen popups remain hidden until they expire or the camera sees their origin. The HUD is below the existing EndScreen in the Canvas hierarchy.

## Adding another task type

Create another Task Definition asset with its own reward. In that mechanic, generate the ID when a new task starts, then publish it on success:

```csharp
using System;
using FactorySabotage.Core.Events;
using FactorySabotage.Gameplay.Tasks;
using UnityEngine;

// Fields on the future task's component:
[SerializeField] private TaskDefinition taskDefinition;
private Guid _taskInstanceId;

// Call once when this particular task begins.
private void BeginTask() => _taskInstanceId = Guid.NewGuid();

// Call after this task's success conditions are met.
private void CompleteTask() =>
    EventBus.Game.RaiseTaskCompleted(taskDefinition, _taskInstanceId, transform.position);
```

Reuse that ID for repeated completion notifications; generate another only when a new task starts. Omit the position argument for rewards that should not show floating text. The reward manager and CP label need no changes for new task types. An unassigned task definition does not award points.

## Validation

The full gameplay assembly compiled successfully with Unity 6000.3.18f1's C# compiler and the project's actual package references. Eleven headless ledger checks passed, covering different reward amounts, repeat completion IDs, new task instances, invalid rewards, spending, and integer overflow.

Floating-reward Play-mode checks passed for successful house and tree extinguishing, partial water, duplicate completions, a different 75-CP reward, zero rewards, unpositioned rewards, spending, target/HUD disable, and pause/resume. Normal gameplay frames moved the popup upward, faded its alpha, and returned all three prewarmed labels to the pool; the next tree reward reused those same text objects. A physical water-ray hit on a house produced one `+100 CP` popup and 100 wallet CP. The Console contained no errors or warnings after leaving Play mode. `Artifacts/FloatingCP/Preview.png` shows the reward above a house.
