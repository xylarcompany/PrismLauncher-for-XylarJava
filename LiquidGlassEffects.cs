using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace XylarJavaLauncher;

/// <summary>
/// Advanced LIQUID GLASS effects for Avalonia UI
/// Authentic glassmorphism with multi-layer gradients, breathing animations,
/// reflection effects, and fluid transitions
/// </summary>
public static class LiquidGlassEffects
{
    /// <summary>Primary colors for glass effects - monochrome palette</summary>
    public static class PrimaryColors
    {
        public const string Highlight = "#F2EFE9";
        public const string MidTone = "#C9C4BC";
        public const string DeepTone = "#9A958E";
        public const string SoftLight = "#D8D4CC";
        public const string BluDark = "#1A1A1E";
        public const string BluMid = "#2A2A30";
        public const string BluDeep = "#1E1E22";
        public const string NavyBlack = "#121214";
    }

    /// <summary>Apply complete liquid glass effect to a Border with gradient and border</summary>
    public static void ApplyLiquidGlassStyle(Border border, bool withAnimation = true)
        => ApplyLiquidGlassStyle(border, new CornerRadius(28), withAnimation);

    /// <summary>Vertical sidebar navbar: softer radius, stronger glass read.</summary>
    public static void ApplySidebarLiquidGlass(Border border, bool withAnimation = true)
        => ApplyLiquidGlassStyle(border, new CornerRadius(18), withAnimation);

    public static void ApplyLiquidGlassStyle(Border border, CornerRadius cornerRadius, bool withAnimation = true)
    {
        if (border == null) return;

        var backgroundGradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop { Color = Color.Parse("#18FFFFFF"), Offset = 0 },
                new GradientStop { Color = Color.Parse("#2A2825AA"), Offset = 0.45 },
                new GradientStop { Color = Color.Parse("#0C0C0E99"), Offset = 1 }
            }
        };

        var borderGradient = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop { Color = Color.Parse(PrimaryColors.Highlight), Offset = 0 },
                new GradientStop { Color = Color.Parse(PrimaryColors.MidTone), Offset = 0.35 },
                new GradientStop { Color = Color.Parse(PrimaryColors.DeepTone), Offset = 0.55 },
                new GradientStop { Color = Color.Parse(PrimaryColors.MidTone), Offset = 0.75 },
                new GradientStop { Color = Color.Parse(PrimaryColors.Highlight), Offset = 1 }
            }
        };

        border.Background = backgroundGradient;
        border.BorderBrush = borderGradient;
        border.BorderThickness = new Thickness(1.5);
        border.CornerRadius = cornerRadius;
        border.Opacity = 1;

        if (withAnimation)
            AnimateBorderBreathe(border);
    }

    /// <summary>Create shimmer overlay effect that simulates light reflection on glass</summary>
    public static Border CreateShimmerOverlay(double width = 200, double height = 50)
    {
        var shimmer = new Border
        {
            Width = width,
            Height = height,
            CornerRadius = new CornerRadius(20),
            Opacity = 0.2,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop { Color = Colors.Transparent, Offset = 0 },
                    new GradientStop { Color = Colors.White, Offset = 0.5 },
                    new GradientStop { Color = Colors.Transparent, Offset = 1 }
                }
            }
        };
        return shimmer;
    }

    /// <summary>Animate border with breathing effect - cycles through color palette every 2 seconds</summary>
    private static async void AnimateBorderBreathe(Border border)
    {
        if (border == null) return;

        var colors = new[]
        {
            PrimaryColors.Highlight,
            PrimaryColors.MidTone,
            PrimaryColors.DeepTone,
            PrimaryColors.SoftLight,
            PrimaryColors.MidTone
        };

        int colorIndex = 0;
        try
        {
            while (true)
            {
                await Task.Delay(2000);

                var nextColor = Color.Parse(colors[colorIndex % colors.Length]);
                var nextColorAlt = Color.Parse(colors[(colorIndex + 1) % colors.Length]);

                var animatedGradient = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = new GradientStops
                    {
                        new GradientStop { Color = nextColor, Offset = 0 },
                        new GradientStop { Color = nextColorAlt, Offset = 0.5 },
                        new GradientStop { Color = nextColor, Offset = 1 }
                    }
                };

                border.BorderBrush = animatedGradient;
                colorIndex++;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Animation error: {ex.Message}");
        }
    }

    /// <summary>Create glow shadow effect around an element</summary>
    public static BoxShadow CreateGlowShadow(string color, double spread = 20)
    {
        var colorValue = Color.Parse(color);
        return new BoxShadow
        {
            Color = colorValue,
            Blur = spread,
            Spread = 0,
            OffsetX = 0,
            OffsetY = 0
        };
    }

    /// <summary>Apply glass text style to a TextBlock</summary>
    public static void ApplyGlassTextStyle(TextBlock textBlock, bool isBold = false)
    {
        if (textBlock == null) return;

        textBlock.Foreground = new SolidColorBrush(Color.Parse("#F6F8FF"));
        textBlock.FontSize = isBold ? 28 : 18;
        textBlock.FontWeight = isBold ? FontWeight.Bold : FontWeight.SemiBold;
    }

    /// <summary>Create a button with liquid glass effect</summary>
    public static Button CreateLiquidGlassButton(string content, bool isPrimary = true)
    {
        var button = new Button
        {
            Content = content,
            Padding = new Thickness(20, 12),
            CornerRadius = new CornerRadius(14),
            FontWeight = isPrimary ? FontWeight.Bold : FontWeight.SemiBold
        };

        if (isPrimary)
        {
            button.Background = new SolidColorBrush(Color.Parse(PrimaryColors.MidTone));
            button.Foreground = new SolidColorBrush(Color.Parse("#07111D"));
        }
        else
        {
            button.Background = new SolidColorBrush(Color.Parse("#353540"));
            button.Foreground = new SolidColorBrush(Color.Parse("#F6F8FF"));
        }

        return button;
    }
}
