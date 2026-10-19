using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Petaurum.Windows.Router;

namespace Petaurum.Windows.Pages;

/// <summary>Picks how to show a block of router data: field cards, record expanders or a table.</summary>
public sealed partial class RouterBlockTemplateSelector : DataTemplateSelector
{
    public DataTemplate? FieldsTemplate { get; set; }
    public DataTemplate? RecordsTemplate { get; set; }
    public DataTemplate? TableTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => (item as RouterBlock)?.Kind switch
    {
        RouterBlockKind.Fields => FieldsTemplate,
        RouterBlockKind.Records => RecordsTemplate,
        RouterBlockKind.Table => TableTemplate,
        _ => null,
    };

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => SelectTemplateCore(item);
}
