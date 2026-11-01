using VectorViolet.ProAppState;

namespace FactorySabotage.Input
{
    public static class InputProvider
    {
        private static readonly InputControlsProvider<GameControls> Provider = new();

        public static GameControls Controls => Provider.Controls;

        public static void Shutdown() => Provider.Shutdown();
    }
}