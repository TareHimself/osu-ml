// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using osu.Game.ML;

namespace osu.Game.Screens.Play
{
    public partial class KeyCounterAction<T> : KeyCounter
        where T : struct
    {
        public T Action { get; }

        public MlBridgeInstance Ai = MlBridgeInstance.GetInstance();
        public KeyCounterAction(T action)
            : base($"B{(int)(object)action + 1}")
        {
            Action = action;
        }

        public bool OnPressed(T action, bool forwards)
        {
            if (!EqualityComparer<T>.Default.Equals(action, Action))
                return false;

            IsLit = true;

            if (action.ToString() == "LeftButton")
            {
                Ai.SetLeftButtonState(1);
            }
            else
            {
                Ai.SetRightButtonState(1);
            }

            if (forwards)
                Increment();
            return false;
        }

        public void OnReleased(T action, bool forwards)
        {
            if (!EqualityComparer<T>.Default.Equals(action, Action))
                return;

            IsLit = false;
            if (action.ToString() == "LeftButton")
            {
                Ai.SetLeftButtonState(0);
            }
            else
            {
                Ai.SetRightButtonState(0);
            }

            if (!forwards)
                Decrement();
        }
    }
}
