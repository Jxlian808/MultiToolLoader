using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MultiToolLoader.Helpers
{
    public static class AnimationHelper
    {
        public static Task FadeIn(UIElement element, double duration = 0.3)
        {
            var tcs = new TaskCompletionSource<bool>();
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(duration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            animation.Completed += (s, e) => tcs.SetResult(true);
            element.BeginAnimation(UIElement.OpacityProperty, animation);

            return tcs.Task;
        }

        public static Task FadeOut(UIElement element, double duration = 0.3)
        {
            var tcs = new TaskCompletionSource<bool>();
            var animation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromSeconds(duration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            animation.Completed += (s, e) => tcs.SetResult(true);
            element.BeginAnimation(UIElement.OpacityProperty, animation);

            return tcs.Task;
        }

        public static Task SlideIn(UIElement element, double offset, double duration = 0.3)
        {
            var tcs = new TaskCompletionSource<bool>();
            var transform = new TranslateTransform();
            element.RenderTransform = transform;

            var animation = new DoubleAnimation
            {
                From = offset,
                To = 0,
                Duration = TimeSpan.FromSeconds(duration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            animation.Completed += (s, e) => tcs.SetResult(true);
            transform.BeginAnimation(TranslateTransform.XProperty, animation);

            return tcs.Task;
        }

        public static Task SlideOut(UIElement element, double offset, double duration = 0.3)
        {
            var tcs = new TaskCompletionSource<bool>();
            var transform = element.RenderTransform as TranslateTransform ?? new TranslateTransform();
            element.RenderTransform = transform;

            var animation = new DoubleAnimation
            {
                From = 0,
                To = offset,
                Duration = TimeSpan.FromSeconds(duration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            animation.Completed += (s, e) => tcs.SetResult(true);
            transform.BeginAnimation(TranslateTransform.XProperty, animation);

            return tcs.Task;
        }

        public static Task ScaleIn(UIElement element, double duration = 0.3)
        {
            var tcs = new TaskCompletionSource<bool>();
            var transform = new ScaleTransform(0.8, 0.8);
            element.RenderTransform = transform;

            var animation = new DoubleAnimation
            {
                From = 0.8,
                To = 1,
                Duration = TimeSpan.FromSeconds(duration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            animation.Completed += (s, e) => tcs.SetResult(true);
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);

            return tcs.Task;
        }

        public static Task ScaleOut(UIElement element, double duration = 0.3)
        {
            var tcs = new TaskCompletionSource<bool>();
            var transform = new ScaleTransform(1, 1);
            element.RenderTransform = transform;

            var animation = new DoubleAnimation
            {
                From = 1,
                To = 0.8,
                Duration = TimeSpan.FromSeconds(duration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            animation.Completed += (s, e) => tcs.SetResult(true);
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);

            return tcs.Task;
        }

        public static Task ColorTransition(Brush currentBrush, Color targetColor, Action<Brush> updateAction, double duration = 0.3)
        {
            var tcs = new TaskCompletionSource<bool>();
            var solidBrush = currentBrush as SolidColorBrush ?? new SolidColorBrush(Colors.Transparent);

            var animation = new ColorAnimation
            {
                From = solidBrush.Color,
                To = targetColor,
                Duration = TimeSpan.FromSeconds(duration),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            animation.Completed += (s, e) => tcs.SetResult(true);

            var animatingBrush = new SolidColorBrush(solidBrush.Color);
            animatingBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
            updateAction(animatingBrush);

            return tcs.Task;
        }
    }
}