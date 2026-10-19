using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Petaurum.Windows.Router;

namespace Petaurum.Windows.Pages;

/// <summary>
/// A router table as a card with column headings, scrolling sideways when it's wider than the page.
/// WinUI has no data grid, and these tables are small and read-only, so a plain Grid does.
/// </summary>
public sealed partial class RouterTableView : UserControl
{
    public static readonly DependencyProperty BlockProperty = DependencyProperty.Register(
        nameof(Block), typeof(RouterBlock), typeof(RouterTableView),
        new PropertyMetadata(null, (d, _) => ((RouterTableView)d).Build()));

    public RouterBlock? Block
    {
        get => (RouterBlock?)GetValue(BlockProperty);
        set => SetValue(BlockProperty, value);
    }

    private void Build()
    {
        if (Block is not { Columns: { } columns, Rows: { } rows })
        {
            Content = null;
            return;
        }

        var grid = new Grid { Padding = new Thickness(16, 8, 16, 8) };
        var columnCount = Math.Max(columns.Count, rows.Count == 0 ? 0 : rows.Max(r => r.Count));
        for (var c = 0; c < columnCount; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        AddRow(grid, columns, isHeader: true);
        foreach (var row in rows) AddRow(grid, row, isHeader: false);

        Content = new Border
        {
            Background = Brush("CardBackgroundFillColorDefaultBrush"),
            BorderBrush = Brush("CardStrokeColorDefaultBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = (CornerRadius)Application.Current.Resources["ControlCornerRadius"],
            Child = new ScrollViewer
            {
                HorizontalScrollMode = ScrollMode.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollMode = ScrollMode.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = grid,
            },
        };
    }

    private static void AddRow(Grid grid, IReadOnlyList<string> cells, bool isHeader)
    {
        var rowIndex = grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        if (rowIndex > 0)
        {
            var divider = new Border
            {
                BorderBrush = Brush("DividerStrokeColorDefaultBrush"),
                BorderThickness = new Thickness(0, 1, 0, 0),
            };
            Grid.SetRow(divider, rowIndex);
            Grid.SetColumnSpan(divider, Math.Max(1, grid.ColumnDefinitions.Count));
            grid.Children.Add(divider);
        }

        for (var c = 0; c < cells.Count; c++)
        {
            var text = new TextBlock
            {
                Text = cells[c],
                Margin = new Thickness(0, 8, 24, 8),
                IsTextSelectionEnabled = true,
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (isHeader)
            {
                text.Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"];
                text.Foreground = Brush("TextFillColorSecondaryBrush");
            }
            else if (c == 0)
            {
                // The first column usually names the row (a property, a port, a MAC).
                text.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            }
            Grid.SetRow(text, rowIndex);
            Grid.SetColumn(text, c);
            grid.Children.Add(text);
        }
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
