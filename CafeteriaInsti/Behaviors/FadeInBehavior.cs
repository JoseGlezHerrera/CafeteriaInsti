// Behaviors/FadeInBehavior.cs
using Microsoft.Maui.Controls;

namespace CafeteriaInsti.Behaviors
{
    public class FadeInBehavior : Behavior<VisualElement>
    {
        protected override void OnAttachedTo(VisualElement bindable)
        {
            base.OnAttachedTo(bindable);
            bindable.Opacity = 0;
            bindable.Loaded += OnLoaded;
        }

        protected override void OnDetachingFrom(VisualElement bindable)
        {
            base.OnDetachingFrom(bindable);
            bindable.Loaded -= OnLoaded;
        }

        private async void OnLoaded(object? sender, EventArgs e)
        {
            if (sender is VisualElement element)
            {
                await element.FadeTo(1, 500, Easing.CubicInOut);
            }
        }
    }
}
