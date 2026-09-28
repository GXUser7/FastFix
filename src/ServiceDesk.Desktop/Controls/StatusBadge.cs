using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ServiceDesk.Contracts;

namespace ServiceDesk.Desktop.Controls;

// Плашка статуса заявки с цветом из руководства по стилю
public class StatusBadge : Border
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(StatusBadge),
        new PropertyMetadata(null, (d, _) => ((StatusBadge)d).Update()));
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(nameof(Color), typeof(string), typeof(StatusBadge),
        new PropertyMetadata(null, (d, _) => ((StatusBadge)d).Update()));

    private readonly TextBlock _text = new() { Foreground = Brushes.White, FontSize = 12, FontWeight = FontWeights.SemiBold };

    public StatusBadge()
    {
        CornerRadius = new CornerRadius(11);
        Padding = new Thickness(10, 3, 10, 4);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
        Child = _text;
    }

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string Color { get => (string)GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    private void Update()
    {
        _text.Text = Text;
        Background = (Brush)new HexToBrushConverter().Convert(Color, typeof(Brush), null, null);
    }
}

// Шкала этапов ремонта
public class StatusStepper : Grid
{
    private static readonly (string Code, string Title)[] Steps =
    {
        (OrderStatuses.Accepted, "Принята"), (OrderStatuses.Diagnostics, "Диагностика"), (OrderStatuses.Approval, "Согласование"),
        (OrderStatuses.InWork, "В работе"), (OrderStatuses.Ready, "Готова"), (OrderStatuses.Issued, "Выдана"),
    };

    public void Show(string status, string color)
    {
        Children.Clear();
        ColumnDefinitions.Clear();
        RowDefinitions.Clear();
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var cancelled = status == OrderStatuses.Cancelled;
        var current = status == OrderStatuses.WaitingParts ? 3 : Array.FindIndex(Steps, s => s.Code == status);
        var accent = (Brush)new BrushConverter().ConvertFromString("#2D6CDF");
        var gray = (Brush)new BrushConverter().ConvertFromString(cancelled ? "#E9EDF2" : "#D5DCE4");
        var cur = (Brush)new HexToBrushConverter().Convert(color, typeof(Brush), null, null);
        for (var i = 0; i < Steps.Length; i++)
        {
            ColumnDefinitions.Add(new ColumnDefinition());
            if (i < Steps.Length - 1)
            {
                var bar = new Border { Height = 4, Background = !cancelled && i < current ? accent : gray, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                SetColumn(bar, i);
                Children.Add(bar);
            }
            var dot = new Border
            {
                Width = 16, Height = 16, CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Left,
                Background = cancelled ? gray : i < current ? accent : i == current ? cur : gray,
            };
            SetColumn(dot, i);
            Children.Add(dot);
            var label = new TextBlock
            {
                Text = Steps[i].Title, FontSize = 12, Margin = new Thickness(0, 6, 4, 0),
                Foreground = (Brush)new BrushConverter().ConvertFromString(!cancelled && i == current ? "#212121" : "#7A8794"),
                FontWeight = !cancelled && i == current ? FontWeights.SemiBold : FontWeights.Normal,
            };
            SetColumn(label, i);
            SetRow(label, 1);
            Children.Add(label);
        }
    }
}
