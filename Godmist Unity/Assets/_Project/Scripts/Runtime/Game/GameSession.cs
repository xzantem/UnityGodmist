using System;
using Godmist.Core.Enums;

namespace Godmist.Runtime.Game
{
    public class GameSession
    {
        public GameModeState CurrentMode { get; private set; }

        public event Action<GameModeState, GameModeState> ModeChanged;

        public GameSession(GameModeState initialMode = GameModeState.Menu)
        {
            CurrentMode = initialMode;
        }

        public bool TrySetMode(GameModeState nextMode)
        {
            if (CurrentMode == nextMode) return false;

            var previousMode = CurrentMode;
            CurrentMode = nextMode;

            ModeChanged?.Invoke(previousMode, nextMode);
            return true;
        }
    }
}