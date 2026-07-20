using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace RepoGalaxy.Desktop.Controls;

public sealed class MetroRevealButton : Button
{
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        Background = new RadialGradientBrush
        {
            Center = new RelativePoint(point, RelativeUnit.Absolute),
            GradientOrigin = new RelativePoint(point, RelativeUnit.Absolute),
            RadiusX = new RelativeScalar(.8, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(.8, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.FromArgb(54, 255, 255, 255), 0),
                new GradientStop(Color.FromArgb(20, 255, 255, 255), .45),
                new GradientStop(Colors.Transparent, 1)
            ]
        };
        BorderBrush = new SolidColorBrush(Color.FromArgb(72, 255, 255, 255));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        ClearValue(BackgroundProperty);
        ClearValue(BorderBrushProperty);
    }
}
