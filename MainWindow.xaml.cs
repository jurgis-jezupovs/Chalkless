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
    }
}