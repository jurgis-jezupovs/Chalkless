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
        if (e.Key == Key.M)
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
}