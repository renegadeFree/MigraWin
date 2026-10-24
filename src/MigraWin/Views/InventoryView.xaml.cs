using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace MigraWin.Views;

public partial class InventoryView : UserControl
{
    public InventoryView() => InitializeComponent();

    private void ApplicationList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            DependencyObject? current = this;
            while (current is not null)
            {
                if (current is ScrollViewer { HorizontalScrollBarVisibility: ScrollBarVisibility.Auto } scroller)
                {
                    scroller.ScrollToHorizontalOffset(0);
                    break;
                }

                current = VisualTreeHelper.GetParent(current);
            }
        }, DispatcherPriority.ContextIdle);
    }
}
