using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows;

namespace Chalkless.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    public ObservableCollection<InkCanvas> Canvases { get; } = new();
    private int _selectedIndex;
    private InkCanvasEditingMode _editingMode = InkCanvasEditingMode.Ink;
    private Brush _brushColor = Brushes.Orange;
    private DrawingBrush _drawingBrush = new DrawingBrush
    {
        TileMode = TileMode.Tile,
        Viewport = new Rect(-10, -10, 45, 45),
        ViewportUnits = BrushMappingMode.Absolute,
        Drawing = new GeometryDrawing
        {
            Brush = new SolidColorBrush(Color.FromArgb(0xFF,0x26, 0x26, 0x26)),  // #262626
            Geometry = new RectangleGeometry(new Rect(0, 0, 50, 50)),
            Pen = new Pen(new SolidColorBrush(Color.FromArgb(0xFF, 0x45, 0x45, 0x45)), 1) // #454545, Thickness 1
        }
    };

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < 0) return;
            if (value >= Canvases.Count)
            {
                Canvases.Add(new InkCanvas
                {
                    EditingMode = _editingMode,
                    DefaultDrawingAttributes = new DrawingAttributes
                    {
                        Color = ((SolidColorBrush)_brushColor).Color,
                    },
                    Background = _drawingBrush
                });
                OnPropertyChanged(nameof(CanvasCount));
            }
            _selectedIndex = value;
            OnPropertyChanged(nameof(DisplayedIndex));
            OnPropertyChanged(nameof(SelectedCanvas));
        } 
    }

    public int CanvasCount => Canvases.Count;
    public int DisplayedIndex => SelectedIndex + 1;
    public InkCanvasEditingMode EditingMode
    {
        get => _editingMode;
        set
        {
            _editingMode = value;
            foreach (var canvas in Canvases)
                canvas.EditingMode = value;
        }
    }

    public Brush BrushColor
    {
        get => _brushColor;
        set
        {
            _brushColor = value;
            foreach (var canvas in Canvases)
                canvas.DefaultDrawingAttributes.Color = ((SolidColorBrush)value).Color;
        }
    }

    public InkCanvas SelectedCanvas => Canvases[SelectedIndex];

    public ICommand NextCanvasCommand { get; }
    public ICommand PrevCanvasCommand { get; }
    public ICommand EditingModeInkCommand { get; }
    public ICommand EditingModeDeleteCommand { get; }
    public ICommand EditingModeSelectCommand { get; }
    public ICommand OrangeColorCommand { get; }
    public ICommand GreenColorCommand { get; }
    public ICommand BlueColorCommand { get; }
    public ICommand RedColorCommand { get; }


    public MainViewModel()
    {
        Canvases.Add(new InkCanvas
        {
            EditingMode = _editingMode,
            DefaultDrawingAttributes = new DrawingAttributes
            {
                Color = ((SolidColorBrush)_brushColor).Color,
            },
            Background = _drawingBrush
        });
        NextCanvasCommand = new RelayCommand(_ => SelectedIndex++);
        PrevCanvasCommand = new RelayCommand(_ => SelectedIndex--);
        EditingModeInkCommand = new RelayCommand(_ => EditingMode = InkCanvasEditingMode.Ink);
        EditingModeDeleteCommand = new RelayCommand(_ => EditingMode = InkCanvasEditingMode.EraseByStroke);
        EditingModeSelectCommand = new RelayCommand(_ => EditingMode = InkCanvasEditingMode.Select);
        OrangeColorCommand = new RelayCommand(_ => BrushColor = Brushes.Orange);
        GreenColorCommand = new RelayCommand(_ => BrushColor = Brushes.LimeGreen);
        BlueColorCommand = new RelayCommand(_ => BrushColor = Brushes.DodgerBlue);
        RedColorCommand = new RelayCommand(_ => BrushColor = Brushes.Red);
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class RelayCommand : ICommand
{
    private readonly Action<object> _execute;
    public RelayCommand(Action<object> execute) => _execute = execute;
    public bool CanExecute(object parameter) => true;
    public void Execute(object parameter) => _execute(parameter);
    public event EventHandler CanExecuteChanged { add {} remove {} }
}
