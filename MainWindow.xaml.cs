using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Chalkless;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void MainWindow_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.B)
        {
            InkCanvas.EditingMode = InkCanvasEditingMode.Ink;
        }
        else if (e.Key == Key.D)
        {
            InkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
        }
        else if (e.Key == Key.S)
        {
            InkCanvas.EditingMode = InkCanvasEditingMode.Select;
        }
        else if (e.Key == Key.M)
        {
            if (Grid.ColumnDefinitions[0].Width.Value > 0)
            {
                Grid.ColumnDefinitions[0].Width = new GridLength(0);
            }
            else
            {
                Grid.ColumnDefinitions[0].Width = new GridLength(250);
            }
        }
    }

    private void BrushButton_OnClick(object sender, RoutedEventArgs e)
    {
        InkCanvas.EditingMode = InkCanvasEditingMode.Ink;
    }

    private void DeleteButton_OnClick(object sender, RoutedEventArgs e)
    {
        InkCanvas.EditingMode = InkCanvasEditingMode.EraseByStroke;
    }

    private void SelectButton_OnClick(object sender, RoutedEventArgs e)
    {
        InkCanvas.EditingMode = InkCanvasEditingMode.Select;
    }
}