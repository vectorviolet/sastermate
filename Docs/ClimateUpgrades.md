# Eco upgrades

Solar panels, recycling centers, and planted trees spend the same CP earned from extinguishing fires. Purchases permanently reduce future warming for the current run. They do not instantly lower the current temperature. The existing `SpendCPForEcoBuilding` instant-cooling method remains available for any separate scripted actions.

The three ready-made definitions are in `Assets/_Project/Data/ClimateUpgrades`:

| Asset | CP per purchase | Reduction of original fire warming |
| --- | ---: | ---: |
| Solar Panel | 50 | 5% |
| Recycling | 40 | about 3.33% |
| Afforestation | 30 | 2% |

The table reflects the current renamed/tuned assets. Edit their Description, price, and protection in the Inspector. `0.1` means 10%. Reductions add across purchases, up to **Climate Manager > Maximum Warming Reduction**, initially `0.8` (80%). With three fires, base warming is `0.03` degrees/second. One of each current upgrade gives about 10.33% protection and a rate of `0.0269` degrees/second. At 80% protection, the same fires still add `0.006` degrees/second.

The final purchase can use the remaining protection below the cap; its button shows that effective amount before purchase. At the cap, further purchases are disabled and spend no CP. Purchases also require Gameplay, a living climate system below the +2 degree loss threshold, enough CP, and a free prepared site. No sites or insufficient CP leaves both the balance and the city unchanged.

## Editor setup

1. Outside Play mode, create an active empty **EcoUpgrades** object. Add **Eco Upgrade Manager** and assign your existing **ClimateManager** to **Climate**. Keep both scene-owned so Replay starts a fresh run. The manager discovers scene sites automatically; no prefab catalog is required.
2. Create an empty **EcoSites** root in the scene. Optionally assign it to the manager's **Sites Root** to restrict discovery to that hierarchy.
3. Under EcoSites, create an active empty **SolarSite01**. Add **Eco Upgrade Site**, and assign the **Solar Panel** asset to **Upgrade**. Place your existing `Assets/_Project/Props_Roof Solar Panel.prefab` as a **child** of the site, position/rotate/scale it onto a roof, then drag that child into **Built Visual**. Only this upgrade visual should be a child of the site; do not assign the house or the site root itself. The component hides the visual during Awake and reveals it when paid for.
4. Prepare **RecyclingSite01** and **TreeSite01** the same way, assigning Recycling and Afforestation respectively. Put a suitable recycling-center model under the recycling site; the project has no dedicated recycling model, so choose an existing building as a placeholder or supply your model. For a tree, use a visual tree FBX from the Kenney environment folders. Keep all site roots active. Their visual children may be authored active or inactive.
5. Duplicate these sites and place them wherever each structure should be built. Each site allows one purchase; a button builds the next available site of its type. Roof anchors, parks, and road clearance are authored in the scene, avoiding free-placement overlaps. The order follows the discovered site list. Selected sites show a cyan wire box, turning green after purchase.
6. Under your upgrade screen, create three **UI > Button - TextMeshPro** buttons. Add **Eco Upgrade Button** to each. Assign **Upgrades = EcoUpgrades** and the matching **Upgrade** asset. Keep the button text as **Buy**; the script preserves authored text and object names. Upgrade names and details appear in the shared tooltip described below.
7. Eco Upgrade Button subscribes to its Button automatically. Its **On Click()** can remain empty for buying. Remove inherited Play/Exit/StartGame callbacks if you duplicated a menu button. An optional `GameAudioPresenter.PlayClick()` callback can stay for click audio. The containing Canvas needs its enabled Graphic Raycaster and the scene's existing EventSystem.
8. Optionally create a TMP HUD label and assign **Climate Manager > Warming Rate Text**. It displays the current fire warming rate and total protection. Keep the HUD visible through the existing Game Panel Presenter's Gameplay configuration.
9. Save the scene and test in Gameplay. For a quick test, set **TaskRewards > Starting Carbon Points = 120** outside Play mode; return it to your intended starting balance afterward. Buying one of each currently costs 120 CP. Check that each visual appears once, CP decreases, the warming slope becomes smaller, and purchases stop at Game Over. Replay should restore all plots and the starting balance.

## Build-site example

**Prefab shortcut:** Built Visual also accepts a model prefab directly from the Project window. The site creates its own hidden child instance during Awake and reveals that instance after purchase. Its local position is reset to zero, so place/rotate/scale the **site root** at the intended roof or ground position; any saved world position on the prefab is ignored. The original prefab asset is not changed. A preplaced child model, as below, remains supported for custom local offsets.

The manager and purchase buttons handle payment. A site specifies where a purchased object appears in the 3D city. Create the sites in the scene hierarchy outside the Canvas:

```text
EcoSites
    SolarSite01                 <- Eco Upgrade Site component
        SolarPanelModel         <- the placed solar-panel prefab instance
    TreeSite01                  <- Eco Upgrade Site component
        TreeModel               <- a placed tree model
    RecyclingSite01             <- Eco Upgrade Site component
        RecyclingCenterModel    <- a placed building model
```

To make the first solar site:

1. In the Hierarchy, create an empty **EcoSites**, then create an empty child named **SolarSite01**.
2. Add **Eco Upgrade Site** to SolarSite01. Drag `Assets/_Project/Data/ClimateUpgrades/Solar Panel.asset` from the Project window into **Upgrade**. It must be the same definition assigned to the solar purchase button.
3. Drag `Assets/_Project/Props_Roof Solar Panel.prefab` from the Project window onto SolarSite01 in the Hierarchy. This creates the model as its child. Position/rotate/scale that child onto the desired roof.
4. Drag that **child instance from the Hierarchy** into the site's **Built Visual** field. Leave SolarSite01 active. At Play startup the model is hidden; buying solar panels reveals it and applies its warming protection.
5. Assign EcoSites to **Eco Upgrade Manager > Sites Root**. Duplicate SolarSite01 to prepare more roof positions. Every purchase reveals one available matching site.

Repeat with the Afforestation definition and a tree model at each planting position, or Recycling and a recycling-center model at each building position. All these sites can share the same definition assets; each site only needs its own placed visual.

## Open and close the upgrade screen

The scene already uses **UpgradeScreen > Canvas Panel > Panel Id = UpgradePanel**. The existing **GamePanels > Game Panel Presenter** supplies the open/close methods; no additional controller is required.

1. Keep UpgradeScreen active. Set its CanvasGroup to **Alpha = 0**, **Interactable off**, and **Blocks Raycasts off** outside Play mode.
2. In **GamePanels > Game Panel Presenter > Panels**, find the entry pointing to UpgradeScreen. Set its **Visible In States > Size = 0**. This makes it a manually opened panel. Any game-state change hides it; listing Gameplay there would make it open automatically whenever Gameplay is entered.
3. Create a normal **UI Button** named **OpenUpgrades** under **GameScreen**, with its text set to **Upgrades**. In **Button > On Click()**, add GamePanels, choose **Game Panel Presenter > ShowPanel(string)**, and enter **UpgradePanel** into the string argument.
4. Create a normal **UI Button** inside **UpgradeScreen**, with its text set to **Close** or **X**. Its On Click target is GamePanels, method **Game Panel Presenter > HidePanel(string)**, argument **UpgradePanel**.
   Hiding UpgradePanel also calls the shared **GameManager.ResumeGame()** action. This restores Gameplay/input listeners if the game was paused and reconciles them if it was already Gameplay. Escape while the shop is visible uses the same action. The existing HidePanel button callback does not need to be replaced.
5. Save and test: Gameplay starts with the shop hidden, Upgrades opens it, Close hides it, and Game Over closes it. Purchases use the Eco Upgrade Button components already assigned to the three purchase buttons. Their authored **Buy** text is preserved.

GameScreen and UpgradeScreen remain sibling panels under the Canvas. The game stays in Gameplay while shopping, allowing the existing purchase checks to work. Showing the upgrade screen through ShowPanel preserves the gameplay HUD underneath.

### Water input while shopping

The open panel must have **Panel Id = UpgradePanel**, as in the current scene. GameplayInputAdapter queues attack presses until Update and checks both this panel's visibility and fresh Graphic Raycaster hits under the pointer before forwarding them. Opening the shop or moving onto clickable UI releases an active spray. UI clicks are consumed; closing the shop while holding the mouse does not resume spraying until a fresh press. Water emission, its Fire animation, and water-loop audio are also gated by upgrade-panel visibility. Existing water already in flight can finish its trajectory.

CanvasGroup **Blocks Raycasts** still controls UI hit routing. CanvasPanel enables it when showing the screen and disables it when hiding the screen. The gameplay input filter is needed because UI raycasts do not consume the separate Input System attack action. Keep the shop in Gameplay so purchase checks continue to pass.

## Hover/hold descriptions

The popup and its request pattern are adapted from Soulbonk's `CanvasTooltips.prefab`, TooltipManager, and TooltipTrigger. This project uses its own Sastermate scripts and its existing LiberationSans TMP font. The copied layout is fitted to a bounded width and supplied with a standalone overlay Canvas.

1. Outside Play mode, drag **Assets/_Project/UI/Prefabs/CanvasTooltips.prefab** into the scene at the **Hierarchy root**. Keep one active instance. Its Tooltip Manager, popup CanvasGroup, text, Canvas, and scaler are already assigned. It belongs to the scene and is not registered as a game panel.
2. Keep the existing Eco Upgrade Button components and their manager/upgrade references. Each button can retain its authored **Buy** label; the script does not modify button text. Remove extra status/description text objects you added solely for the old label fields if you no longer want them displayed.
3. Edit each upgrade asset's **Description** for its explanatory text. Its current cost, effective warming protection, built count, and availability are appended automatically.
4. Hover an upgrade for **0.35 seconds**, or press/hold the left mouse button to request its description immediately. Standard Button clicks still perform the purchase on release. Hover also works for unaffordable/maxed-out purchases, showing the reason they are unavailable.

The tooltip follows the pointer, flips/clamps at canvas edges, and updates after CP or purchase changes. Its image/text do not receive UI raycasts, and its CanvasGroup is non-interactive. It hides on pointer exit, disabled/destroyed buttons, hidden parent CanvasGroups, game-state changes, and loss of focus. Checking CanvasGroup alpha is necessary because closing UpgradeScreen keeps its GameObjects enabled. Hover timing uses unscaled time.

The placed models are upgrade visuals. Adding fire/destruction mechanics to the new infrastructure is outside this feature; the protection remains in effect for the rest of the run. Runtime-created sites can be discovered by calling `EcoUpgradeManager.RefreshSites()` after they are created. There is no disk persistence for these purchases; a fresh scene resets them.

## Validation

`Tests/ClimateUpgrades/Run.ps1` compiles the production climate, site, shop, button, task wallet, and GameManager logic against small headless fixtures. It checks CP deductions, failed purchases, warming accumulation, protection limits, UI affordability updates, reentrant callbacks, and Replay state handling. It does not emulate native Unity scene unloading, prefab activation, rendering, raycasts, or audio. Gameplay assembly compilation also uses the installed Unity 6000.3.27f2 references. Scene/UI wiring and the visual result remain manual editor checks.
