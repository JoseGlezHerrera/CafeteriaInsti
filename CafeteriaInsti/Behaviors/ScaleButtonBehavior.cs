// Behaviors/ScaleButtonBehavior.cs
using Microsoft.Maui.Controls;

namespace CafeteriaInsti.Behaviors
{
    public class ScaleButtonBehavior : Behavior<Button>
    {
        private Button? _button;

        protected override void OnAttachedTo(Button bindable)
        {
            base.OnAttachedTo(bindable);
            _button = bindable;
            _button.Pressed += OnPressed;
            _button.Released += OnReleased;
        }

        protected override void OnDetachingFrom(Button bindable)
        {
            base.OnDetachingFrom(bindable);
            if (_button != null)
            {
                _button.Pressed -= OnPressed;
                _button.Released -= OnReleased;
            }
        }

        private async void OnPressed(object? sender, EventArgs e)
        {
            if (sender is Button button)
            {
                await button.ScaleTo(0.95, 100, Easing.CubicOut);
            }
        }

        private async void OnReleased(object? sender, EventArgs e)
        {
            if (sender is Button button)
            {
                await button.ScaleTo(1, 100, Easing.CubicIn);
            }
        }
    }
}
