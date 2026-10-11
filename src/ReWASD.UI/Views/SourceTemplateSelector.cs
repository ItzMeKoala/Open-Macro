using System.Windows;
using System.Windows.Controls;
using ReWASD.UI.ViewModels;

namespace ReWASD.UI.Views;

/// <summary>Draws a source as a box, or as its own shape when it has one.</summary>
public class SourceTemplateSelector : DataTemplateSelector
{
    public DataTemplate? BoxTemplate { get; set; }
    public DataTemplate? ShapeTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container) =>
        item is ButtonViewModel { HasShape: true } ? ShapeTemplate : BoxTemplate;
}