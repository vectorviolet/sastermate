using VectorViolet.ProAppState;

namespace FactorySabotage.Core.App
{
    public abstract class GameStateListener : AppStateListener<GameManager, GameState>
    {
        protected override GameManager GetManagerInstance() => GameManager.Instance;

        protected sealed override void OnAppStateChanged(GameState newState) => OnGameStateChanged(newState);

        protected abstract void OnGameStateChanged(GameState newState);
    }
}