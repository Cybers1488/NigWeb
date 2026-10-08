using Content.Shared._NigWeb.Lifeweb;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client._NigWeb.Lifeweb
{
    public sealed class LifewebControlBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private LifewebControlWindow? _window;

        public LifewebControlBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            _window = new LifewebControlWindow();
            _window.OnClose += Close;
            _window.OpenCentered();
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is not LifewebControlBuiState uiState || _window == null)
            {
                return;
            }

            _window.UpdateState(uiState);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _window?.Dispose();
            }
        }
    }
}
