// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Game.ML;

namespace osu.Game.Rulesets.UI
{
    public partial class GameplayCursorContainer : CursorContainer
    {
        /// <summary>
        /// Because Show/Hide are executed by a parent, <see cref="VisibilityContainer.State"/> is updated immediately even if the cursor
        /// is in a non-updating state (via <see cref="FrameStabilityContainer"/> limitations).
        ///
        /// This holds the true visibility value.
        /// </summary>
        public Visibility LastFrameState;

        public MlBridgeInstance Ai = MlBridgeInstance.GetInstance();

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Ai.RegisterCursorContainer(this);
        }

        protected override void Update()
        {
            base.Update();
            LastFrameState = State.Value;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            Ai.ClearCursorContainer();
        }
    }
}
