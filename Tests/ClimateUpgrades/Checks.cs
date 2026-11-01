using System;
using System.Reflection;
using Sastermate.Core.App;
using Sastermate.Core.Events;
using Sastermate.Gameplay.Climate;
using Sastermate.Gameplay.Tasks;
using Sastermate.Gameplay.Fire;
using Sastermate.UI;
using Sastermate.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using VectorViolet.ProPanelManager;

internal static class Checks
{
    private sealed class PanelProbe : IPanelService
    {
        public bool UpgradeVisible;
        public bool IsVisible(string id) => id == "UpgradePanel" && UpgradeVisible;
    }
    private static int _checks;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); _checks++; Console.WriteLine("PASS " + message); }
    private static bool Near(float a, float b) => Math.Abs(a - b) < 0.00001f;
    private static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Call(object target, string method) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    private static GameManager Reset(GameState state = GameState.Gameplay)
    {
        UnityEngine.Object.All.Clear();
        PanelService.Current = null;
        foreach (var group in new[] { typeof(EventBus.Game), typeof(EventBus.Tooltip) })
            foreach (var field in group.GetFields(BindingFlags.Static | BindingFlags.NonPublic))
                if (typeof(Delegate).IsAssignableFrom(field.FieldType)) field.SetValue(null, null);
        GameManager.Instance = null;
        SceneManager.Reset();
        SceneManager.ActiveScene = new Scene { path = "Assets/_Project/Scenes/DemoScene.unity" };
        typeof(GameManager).GetMethod("ResetReplayState", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        Time.timeScale = Time.deltaTime = 1;
        var game = new GameManager();
        Call(game, "Awake"); Call(game, "OnEnable"); game.SetState(state);
        return game;
    }
    private static (ClimateManager climate, TaskRewardManager wallet, EcoUpgradeManager shop) Setup(int cp = 1000,
        float survivalDuration = 180f, bool survivalGoal = true)
    {
        var wallet = new TaskRewardManager(); Set(wallet, "startingCarbonPoints", cp); Call(wallet, "Awake"); Call(wallet, "OnEnable");
        var climate = new ClimateManager { currentTemperature = 1f, activeFiresCount = 3 };
        Set(climate, "rewards", wallet); Set(climate, "trackSceneFires", false);
        Set(climate, "survivalDuration", survivalDuration); Set(climate, "enableSurvivalGoal", survivalGoal);
        Call(climate, "Awake"); Call(climate, "OnEnable");
        var shop = new EcoUpgradeManager(); Set(shop, "climate", climate); Call(shop, "Awake"); Call(shop, "OnEnable");
        return (climate, wallet, shop);
    }
    private static EcoUpgradeDefinition Definition(int cost = 150, float reduction = 0.1f)
    {
        var definition = new EcoUpgradeDefinition();
        Set(definition, "displayName", "Solar Panels"); Set(definition, "carbonCost", cost); Set(definition, "warmingReduction", reduction);
        return definition;
    }
    private static (EcoUpgradeSite site, GameObject visual) Site(EcoUpgradeDefinition upgrade)
    {
        var site = new EcoUpgradeSite(); var visual = new GameObject(); visual.transform.parent = site.transform;
        Set(site, "upgrade", upgrade); Set(site, "builtVisual", visual); Call(site, "Awake"); return (site, visual);
    }
    public static void Main()
    {
        var game = Reset(); var run = Setup(); var solar = Definition(); var plot = Site(solar); run.shop.RefreshSites();
        Check(!plot.visual.activeSelf && !plot.site.IsBuilt, "Prepared visual starts hidden");
        Check(Near(run.climate.WarmingPerSecond, 0.03f), "Three fires warm at the existing baseline");
        Check(run.shop.TryPurchase(solar) && run.wallet.CarbonPoints == 850 && run.climate.currentCP == 850,
            "A purchase deducts CP once from the shared task wallet");
        Check(plot.site.IsBuilt && plot.visual.activeSelf && run.shop.GetBuiltCount(solar) == 1, "Paid upgrade reveals exactly one site");
        Check(Near(run.climate.currentTemperature, 1f) && Near(run.climate.WarmingPerSecond, 0.027f), "Solar slows future warming without instant cooling");
        Check(!run.shop.TryPurchase(solar) && run.wallet.CarbonPoints == 850, "No free sites prevents repeat charging");

        var tree = Definition(50, 0.05f); Site(tree); var recycling = Definition(300, 0.2f); Site(recycling); run.shop.RefreshSites();
        Check(run.shop.TryPurchase(tree) && run.shop.TryPurchase(recycling) && Near(run.climate.WarmingReduction, 0.35f)
            && Near(run.climate.WarmingPerSecond, 0.0195f), "Different infrastructure types stack additively against base warming");
        Call(run.climate, "Update"); Check(Near(run.climate.currentTemperature, 1.0195f), "Climate Update uses the reduced rate");

        game = Reset(); run = Setup(100); solar = Definition(); plot = Site(solar); run.shop.RefreshSites();
        var ui = new EcoUpgradeButton(); var button = new Button(); var label = new TMPro.TMP_Text { text = "Buy" };
        ui.gameObject.Components[typeof(Button)] = button; ui.gameObject.Components[typeof(TMPro.TMP_Text)] = label;
        Set(ui, "upgrades", run.shop); Set(ui, "upgrade", solar);
        Call(ui, "Awake"); Call(ui, "OnEnable");
        Check(!button.interactable && !run.shop.TryPurchase(solar) && run.wallet.CarbonPoints == 100 && !plot.visual.activeSelf,
            "Unaffordable purchase disables its button and changes no balance or visual");
        Check(label.text == "Buy", "The authored Buy caption is preserved");
        int showRequests = 0, hideRequests = 0; bool immediate = false;
        string shownTooltip = null, updatedTooltip = null; MonoBehaviour tooltipOwner = null;
        EventBus.Tooltip.OnShowRequested += (owner, text, force) =>
        { tooltipOwner = owner; shownTooltip = text; immediate = force; showRequests++; };
        EventBus.Tooltip.OnUpdateRequested += (owner, text) => { if (owner == ui) updatedTooltip = text; };
        EventBus.Tooltip.OnHideRequested += owner => { if (owner == ui) hideRequests++; };
        ui.OnPointerEnter(new PointerEventData());
        Check(tooltipOwner == ui && !immediate && shownTooltip.Contains("150 CP") && shownTooltip.Contains("10%")
            && shownTooltip.Contains("Not enough CP"), "Disabled purchases still request a hover description with cost and reason");
        ui.OnPointerDown(new PointerEventData { button = PointerEventData.InputButton.Left });
        Check(immediate && run.wallet.CarbonPoints == 100 && !plot.site.IsBuilt,
            "A left press shows the tooltip immediately without buying");
        ui.OnPointerDown(new PointerEventData { button = PointerEventData.InputButton.Right });
        Check(showRequests == 2, "Right mouse presses do not trigger the hold description");
        ui.OnPointerExit(new PointerEventData());
        Check(hideRequests == 1, "Pointer exit requests hiding only this button's tooltip");
        var task = new TaskDefinition(); Set(task, "carbonPoints", 100); EventBus.Game.RaiseTaskCompleted(task, Guid.NewGuid()); Call(run.climate, "Update");
        Check(button.interactable && run.wallet.CarbonPoints == 200, "Task rewards unlock the purchase button through the shared wallet");
        game.SetState(GameState.Paused);
        Check(!button.interactable && !run.shop.TryPurchase(solar) && run.wallet.CarbonPoints == 200, "Paused state blocks purchases");
        game.SetState(GameState.MainMenu);
        Check(!run.shop.TryPurchase(solar), "Main menu blocks purchases");
        game.SetState(GameState.Gameplay); button.onClick.Invoke();
        Check(run.wallet.CarbonPoints == 50 && plot.visual.activeSelf && updatedTooltip.Contains("Built: 1")
            && updatedTooltip.Contains("All sites built"), "Button click purchases once and refreshes tooltip count and availability");
        Call(ui, "OnDisable"); button.onClick.Invoke();
        Check(run.wallet.CarbonPoints == 50, "Disabled button component removes its click listener");

        game = Reset(); run = Setup(); solar = Definition();
        var prefab = new GameObject { name = "SolarPrefab", scene = new Scene() };
        prefab.transform.localPosition = new Vector3(17f, 3f, -4f);
        var prefabSite = new EcoUpgradeSite(); Set(prefabSite, "upgrade", solar); Set(prefabSite, "builtVisual", prefab);
        Call(prefabSite, "Awake");
        var instance = (GameObject)typeof(EcoUpgradeSite).GetField("builtVisual", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(prefabSite);
        Check(instance != prefab && instance.transform.parent == prefabSite.transform && !instance.activeSelf
            && instance.scene.IsValid() && Near(instance.transform.localPosition.x, 0f),
            "A prefab asset reference creates a hidden child at the site's position");
        Check(prefab.activeSelf && Near(prefab.transform.localPosition.x, 17f), "Preparing a prefab site does not modify the prefab asset");
        run.shop.RefreshSites();
        Check(run.shop.TryPurchase(solar) && instance.activeSelf && run.wallet.CarbonPoints == 850,
            "A prefab-backed site becomes purchasable and reveals its instance after payment");
        var earlySite = new EcoUpgradeSite(); var earlyUpgrade = Definition(50);
        Set(earlySite, "upgrade", earlyUpgrade); Set(earlySite, "builtVisual", prefab); run.shop.RefreshSites();
        var earlyPurchase = run.shop.TryPurchase(earlyUpgrade); Call(earlySite, "Awake");
        var earlyModel = (GameObject)typeof(EcoUpgradeSite).GetField("builtVisual", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(earlySite);
        Check(earlyPurchase && earlyModel != prefab && earlyModel.activeSelf && earlySite.IsBuilt,
            "Purchasing before site Awake creates an instance and preserves its bought visibility");
        var invalidSite = new EcoUpgradeSite(); var otherUpgrade = Definition(); var unrelatedModel = new GameObject();
        Set(invalidSite, "upgrade", otherUpgrade); Set(invalidSite, "builtVisual", unrelatedModel); Call(invalidSite, "Awake"); run.shop.RefreshSites();
        Check(!run.shop.TryPurchase(otherUpgrade) && run.wallet.CarbonPoints == 800 && unrelatedModel.activeSelf,
            "An unrelated scene object cannot be hidden or charged as a build visual");

        game = Reset(); run = Setup();
        Check(!run.climate.TryPurchaseWarmingReduction(-1, 0.1f) && !run.climate.TryPurchaseWarmingReduction(1, float.NaN)
            && !run.climate.TryPurchaseWarmingReduction(1, float.PositiveInfinity) && run.wallet.CarbonPoints == 1000
            && Near(run.climate.WarmingReduction, 0f), "Malformed purchases cannot alter the economy");
        var big = Definition(100, 0.75f); Site(big); tree = Definition(50, 0.1f); Site(tree); Site(tree); run.shop.RefreshSites();
        Check(run.shop.TryPurchase(big) && run.shop.TryPurchase(tree) && Near(run.climate.WarmingReduction, 0.8f)
            && Near(run.climate.WarmingPerSecond, 0.006f), "Protection caps at 80 percent and preserves remaining fire warming");
        var balance = run.wallet.CarbonPoints;
        Check(!run.shop.TryPurchase(tree) && run.wallet.CarbonPoints == balance, "Protection cap prevents charging for an ineffective upgrade");

        game = Reset(); run = Setup(); solar = Definition(); Site(solar); Site(solar); run.shop.RefreshSites();
        bool reentrantAccepted = false;
        var reentrantShop = run.shop;
        run.climate.EconomyChanged += () => reentrantAccepted |= reentrantShop.TryPurchase(solar);
        Check(run.shop.TryPurchase(solar) && !reentrantAccepted && run.wallet.CarbonPoints == 850
            && run.shop.GetBuiltCount(solar) == 1, "Synchronous currency notifications cannot trigger a second purchase");
        run.climate.currentTemperature = 2f;
        Check(!run.shop.TryPurchase(solar) && run.wallet.CarbonPoints == 850, "A purchase cannot rescue a run already at the loss threshold");
        Call(run.climate, "Update");
        Check(run.climate.IsGameOver && game.CurrentState == GameState.GameOver && !run.shop.TryPurchase(solar), "Game over freezes upgrade purchases");

        game = Reset(); var isolated = new ClimateManager { currentCP = 200 }; Set(isolated, "trackSceneFires", false); Call(isolated, "Awake");
        Check(isolated.TryPurchaseWarmingReduction(50, 0.05f) && isolated.currentCP == 150,
            "A simple scene without TaskRewardManager can still use ClimateManager's local balance");

        game = Reset(GameState.GameOver); Time.timeScale = 0; game.Replay(); game.Replay();
        Check(SceneManager.LoadCalls == 1 && SceneManager.RequestedPath == SceneManager.ActiveScene.path && Near(Time.timeScale, 1f),
            "Replay requests one scene reload and restores normal time");
        SceneManager.Complete("Unrelated.unity");
        Check(game.CurrentState == GameState.GameOver, "Unrelated scene completion cannot consume the pending replay");
        SceneManager.Complete(SceneManager.ActiveScene.path);
        Check(game.CurrentState == GameState.Gameplay, "Persistent GameManager enters Gameplay after the reload completes");
        game.Replay(); Check(SceneManager.LoadCalls == 1, "Replay is ignored outside GameOver");

        game = Reset(GameState.GameOver); game.Replay(); Call(game, "OnDisable"); GameManager.Instance = null;
        var replacement = new GameManager(); Set(replacement, "startupState", GameState.MainMenu); Call(replacement, "Awake"); Call(replacement, "OnEnable");
        Check(replacement.CurrentState == GameState.Gameplay, "A scene-owned replacement manager also starts replay directly in Gameplay");
        SceneManager.Complete(SceneManager.ActiveScene.path);
        Call(replacement, "OnDisable"); GameManager.Instance = null;
        var laterManager = new GameManager(); Set(laterManager, "startupState", GameState.MainMenu); Call(laterManager, "Awake");
        Check(laterManager.CurrentState == GameState.MainMenu, "Completed replay does not override future startup configuration");

        game = Reset(GameState.GameOver); SceneManager.ActiveScene = new Scene { path = "" }; game.Replay();
        Check(SceneManager.LoadCalls == 0, "Unsaved scenes cannot start an invalid replay");
        SceneManager.ActiveScene = new Scene { path = "Assets/_Project/Scenes/DemoScene.unity" }; SceneManager.ThrowOnLoad = true; game.Replay();
        SceneManager.ThrowOnLoad = false; game.Replay();
        Check(SceneManager.LoadCalls == 1, "Failed scene loading releases the replay lock for retry");

        Check((int)GameState.MainMenu == 4 && (int)GameState.GameOver == 5 && (int)GameState.GameWon == 6,
            "Adding victory preserves existing serialized game-state values");
        game = Reset(); run = Setup(); game.WinGame();
        Check(GameManager.HasWon && !GameManager.HasLost && GameManager.IsGameOver,
            "WinGame publishes a distinct terminal victory state");
        game.LoseGame(); game.WinGame();
        Check(game.CurrentState == GameState.GameWon, "The first victory cannot be overwritten by a later loss");
        var victoryTemperature = run.climate.currentTemperature;
        run.climate.SpendCPForEcoBuilding(100, 0.5f); Call(run.climate, "Update");
        Check(run.climate.IsGameOver && !run.climate.CanPurchaseUpgrades && run.wallet.CarbonPoints == 1000
            && Near(run.climate.currentTemperature, victoryTemperature), "Victory freezes warming and all climate purchases");
        run.climate.currentTemperature = 2f; Call(run.climate, "Update");
        Check(game.CurrentState == GameState.GameWon, "A post-victory climate update cannot replace the result");
        Call(game, "HandlePausePressed"); Call(game, "HandleBackPressed");
        Check(game.CurrentState == GameState.GameWon, "Pause and Back cannot resume a completed victory");
        game.Replay(); SceneManager.Complete(SceneManager.ActiveScene.path);
        Check(SceneManager.LoadCalls == 1 && game.CurrentState == GameState.Gameplay && !GameManager.IsGameOver,
            "Replay also restarts from victory and clears the terminal state");
        game = Reset(); game.LoseGame(); game.WinGame();
        Check(GameManager.HasLost && !GameManager.HasWon, "The first loss cannot be overwritten by a later victory");
        game = Reset(GameState.MainMenu); game.WinGame(); game.LoseGame();
        Check(game.CurrentState == GameState.MainMenu, "MainMenu cannot accidentally finish an inactive game");
        game.ResumeGame();
        Check(game.CurrentState == GameState.MainMenu, "Resume cannot leave the main menu");
        game = Reset(GameState.Paused); bool controlsEnabled = false; var resumeNotifications = 0;
        game.StateChanged += state => { controlsEnabled = state == GameState.Gameplay; resumeNotifications++; };
        game.ResumeGame();
        Check(controlsEnabled && GameManager.IsGameplay, "Resume returns a paused run to Gameplay for its input listeners");
        game.ResumeGame();
        Check(resumeNotifications == 2, "Resume reconciles input listeners even when the state is already Gameplay");
        PanelService.Current = new PanelProbe { UpgradeVisible = true }; Call(game, "HandlePausePressed");
        Check(GameManager.IsGameplay && resumeNotifications == 3, "Escape on an open upgrade panel uses the same Gameplay resume path");
        PanelService.Current = null; Call(game, "HandlePausePressed");
        Check(game.CurrentState == GameState.Paused, "Escape without an upgrade panel retains normal pause behavior");
        Call(game, "HandleBackPressed");
        Check(GameManager.IsGameplay, "Back uses the shared resume path");
        game.WinGame(); game.ResumeGame();
        Check(GameManager.HasWon, "Closing a panel cannot resume a completed run");

        game = Reset(GameState.MainMenu); run = Setup(); run.climate.activeFiresCount = 0;
        Time.deltaTime = 60f; Call(run.climate, "Update");
        Check(Near(run.climate.SecondsRemaining, 180f), "Survival timer does not count down in the main menu");
        game.StartGame(); Call(run.climate, "Update");
        Check(Near(run.climate.SecondsRemaining, 120f) && Near(run.climate.ElapsedGameplayTime, 60f),
            "The shared VectorViolet countdown advances once per gameplay update");
        game.SetState(GameState.Paused); Call(run.climate, "Update");
        Check(Near(run.climate.SecondsRemaining, 120f), "Pausing freezes the survival countdown");
        game.SetState(GameState.Gameplay); Time.deltaTime = 119f; Call(run.climate, "Update");
        Check(!GameManager.IsGameOver && Near(run.climate.SecondsRemaining, 1f), "Survival cannot win before the deadline");
        Time.deltaTime = 1f; Call(run.climate, "Update");
        Check(GameManager.HasWon && Near(run.climate.SecondsRemaining, 0f), "Surviving three gameplay minutes triggers victory");
        game = Reset(); run = Setup(1000, 1f); run.climate.currentTemperature = 1.99f;
        Time.deltaTime = 1f; Call(run.climate, "Update");
        Check(GameManager.HasLost, "Temperature loss wins a tie with the survival deadline");
        game = Reset(); run = Setup(1000, 1f, false); run.climate.activeFiresCount = 0;
        Time.deltaTime = 10f; Call(run.climate, "Update");
        Check(GameManager.IsGameplay, "The automatic survival goal can be disabled for a custom objective");
        Check(FireSpawnSchedule.FireLimit(1, 3, FireSpawnSchedule.NormalProgress(59.9f, 60f, 30f, true)) == 1,
            "The first minute is limited to one active fire");
        Check(FireSpawnSchedule.FireLimit(1, 3, FireSpawnSchedule.NormalProgress(75f, 60f, 30f, true)) == 2,
            "Difficulty introduces a second fire during the ramp");
        Check(FireSpawnSchedule.FireLimit(1, 3, FireSpawnSchedule.NormalProgress(90f, 60f, 30f, true)) == 3,
            "Normal fire capacity is reached after the warmup and ramp");
        Check(FireSpawnSchedule.FireLimit(1, 3, FireSpawnSchedule.NormalProgress(0f, 60f, 30f, false)) == 3,
            "Disabling warmup immediately uses normal difficulty");
        Check(FireSpawnSchedule.FireLimit(5, 3, 0f) == 3, "Warmup cannot exceed the configured normal fire capacity");

        var attack = new AttackInputGate();
        attack.QueuePress();
        Check(attack.Update(true) == AttackInputChange.None && attack.Update(false) == AttackInputChange.None && !attack.HasInput,
            "A UI mouse press is consumed instead of firing when the pointer later leaves UI");
        attack.QueuePress();
        Check(attack.Update(false) == AttackInputChange.Pressed && attack.Update(false) == AttackInputChange.None,
            "A world press starts one held attack");
        Check(attack.Update(true) == AttackInputChange.Released && attack.Update(false) == AttackInputChange.None,
            "Opening a modal or moving onto UI releases water without resuming after close");
        attack.QueuePress();
        Check(attack.Update(false) == AttackInputChange.Pressed, "A fresh press can spray after leaving the UI");
        attack.QueuePress();
        Check(attack.Update(false) == AttackInputChange.None, "Repeated performed callbacks cannot duplicate a held attack");
        attack.Release(); attack.QueuePress(); attack.Release();
        Check(attack.Update(false) == AttackInputChange.None && !attack.HasInput, "Releasing before dispatch cancels the queued attack");
        Console.WriteLine($"{_checks} headless gameplay checks passed. Native scene loading, visuals, and editor wiring remain manual.");
    }
}
