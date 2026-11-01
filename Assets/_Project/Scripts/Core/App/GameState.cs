namespace Sastermate.Core.App
{
    public enum GameState
    {
        Gameplay,
        Cutscene,
        Paused,
        Interaction,

        // Keep existing serialized enum values stable; append new states below.
        MainMenu,
        GameOver,
        GameWon
    }
}
