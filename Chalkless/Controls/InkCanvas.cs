using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Chalkless.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input.Platform;

namespace Chalkless.Controls;

public class InkCanvas : Control
{
    private readonly List<InkStroke> _strokes = new();
    private readonly List<CanvasImage> _images = new();
    private InkStroke? _currentStroke;
    private bool _isDrawing;
    private bool _isPanning;
    private Point _lastPanPoint;
    private Point _panOffset = new Point(0, 0);
    private double _zoomLevel = 1.0;
    private bool _isCurrentlyPanning;
    private CanvasImage? _selectedImage;
    private bool _isMovingImage;
    private Point _lastImageMovePoint;
    private Rect _imageStartBounds;
    private readonly Stack<ICanvasAction> _undoStack = new();
    private readonly Stack<ICanvasAction> _redoStack = new();
    private List<ICanvasAction>? _currentEraseActions;
    private bool _wasEraserModeBeforePen;
    private bool _isEraserModeAutoActivated;

    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<InkCanvas, IBrush?>(nameof(Background));

    public static readonly StyledProperty<Color> InkColorProperty =
        AvaloniaProperty.Register<InkCanvas, Color>(nameof(InkColor), Colors.White);

    public static readonly StyledProperty<double> InkThicknessProperty =
        AvaloniaProperty.Register<InkCanvas, double>(nameof(InkThickness), 3.0);

    public static readonly StyledProperty<double> GridCellSizeProperty =
        AvaloniaProperty.Register<InkCanvas, double>(nameof(GridCellSize), 40.0);

    public static readonly StyledProperty<bool> IsEraserModeProperty =
        AvaloniaProperty.Register<InkCanvas, bool>(nameof(IsEraserMode), false);

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<InkCanvas, bool>(nameof(ShowGrid), true);

    public static readonly StyledProperty<bool> IsPanModeProperty =
        AvaloniaProperty.Register<InkCanvas, bool>(nameof(IsPanMode), false);

    public static readonly StyledProperty<bool> IsSelectModeProperty =
        AvaloniaProperty.Register<InkCanvas, bool>(nameof(IsSelectMode), false);

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public Color InkColor
    {
        get => GetValue(InkColorProperty);
        set => SetValue(InkColorProperty, value);
    }

    public double InkThickness
    {
        get => GetValue(InkThicknessProperty);
        set => SetValue(InkThicknessProperty, value);
    }

    public double GridCellSize
    {
        get => GetValue(GridCellSizeProperty);
        set => SetValue(GridCellSizeProperty, value);
    }

    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public bool IsPanMode
    {
        get => GetValue(IsPanModeProperty);
        set => SetValue(IsPanModeProperty, value);
    }

    public bool IsSelectMode
    {
        get => GetValue(IsSelectModeProperty);
        set => SetValue(IsSelectModeProperty, value);
    }

    public bool IsEraserMode
    {
        get => GetValue(IsEraserModeProperty);
        set => SetValue(IsEraserModeProperty, value);
    }

    public InkCanvas()
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Cross);
        Focusable = true;
        
        PropertyChanged += (s, e) =>
        {
            if (e.Property == IsEraserModeProperty || e.Property == IsPanModeProperty || e.Property == IsSelectModeProperty)
            {
                UpdateCursor();
            }
        };
    }

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        
        if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            await PasteImageFromClipboard();
            e.Handled = true;
        }
        else if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Undo();
            e.Handled = true;
        }
        else if (e.Key == Key.Y && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Redo();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && _selectedImage != null)
        {
            var action = new RemoveItemAction<CanvasImage>(_selectedImage, AddImageInternal, RemoveImageInternal);
            ExecuteAction(action);
            _selectedImage = null;
            InvalidateVisual();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            // Escape: deselect any selected image
            if (_selectedImage != null)
            {
                _selectedImage.IsSelected = false;
                _selectedImage = null;
                InvalidateVisual();
                e.Handled = true;
            }
        }
    }

    private async Task PasteImageFromClipboard()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;
        
        try
        {
            Bitmap? bitmap = null;
            
            // Try the built-in bitmap extension method first
            bitmap = await clipboard.TryGetBitmapAsync();
            
            // Fallback: try reading raw bytes from known MIME formats
            if (bitmap == null)
            {
                using var data = await clipboard.TryGetDataAsync();
                if (data != null)
                {
                    var imageFormats = new[] { "image/png", "image/bmp", "image/jpeg" };
                    foreach (var fmt in imageFormats)
                    {
                        var dataFormat = DataFormat.CreateBytesApplicationFormat(fmt);
                        var bytes = await data.TryGetValueAsync(dataFormat);
                        if (bytes is { Length: > 0 })
                        {
                            try
                            {
                                using var ms = new MemoryStream(bytes);
                                bitmap = new Bitmap(ms);
                                break;
                            }
                            catch { }
                        }
                    }
                }
            }
            
            if (bitmap != null)
            {
                var centerX = (-_panOffset.X / _zoomLevel) + (Bounds.Width / _zoomLevel / 2);
                var centerY = (-_panOffset.Y / _zoomLevel) + (Bounds.Height / _zoomLevel / 2);
                
                var imageWidth = bitmap.PixelSize.Width;
                var imageHeight = bitmap.PixelSize.Height;
                
                // Pre-convert to SKBitmap for performance
                SKBitmap? skBitmap = null;
                try
                {
                    using var stream = new MemoryStream();
                    bitmap.Save(stream);
                    stream.Position = 0;
                    skBitmap = SKBitmap.Decode(stream);
                }
                catch { }
                
                var canvasImage = new CanvasImage
                {
                    Bitmap = bitmap,
                    CachedSKBitmap = skBitmap,
                    Bounds = new Rect(
                        centerX - imageWidth / 2,
                        centerY - imageHeight / 2,
                        imageWidth,
                        imageHeight)
                };
                
                var action = new AddItemAction<CanvasImage>(canvasImage, AddImageInternal, RemoveImageInternal);
                ExecuteAction(action);
                InvalidateVisual();
                
                System.Diagnostics.Debug.WriteLine("Image pasted successfully!");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No image found in clipboard");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Clipboard paste error: {ex.Message}");
        }
    }   
    private void UpdateCursor()
    {
        if (IsPanMode)
            Cursor = new Cursor(StandardCursorType.Hand);
        else if (IsSelectMode)
            Cursor = new Cursor(StandardCursorType.Arrow);
        else if (IsEraserMode)
            Cursor = new Cursor(StandardCursorType.Hand);
        else
            Cursor = new Cursor(StandardCursorType.Cross);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var pointer = e.GetPosition(this);
            var delta = e.Delta.Y;
            var zoomFactor = delta > 0 ? 1.1 : 0.9;
            
            var oldZoom = _zoomLevel;
            _zoomLevel = Math.Clamp(_zoomLevel * zoomFactor, 0.1, 10.0);
            
            var zoomChange = _zoomLevel / oldZoom;
            _panOffset = new Point(
                pointer.X - (pointer.X - _panOffset.X) * zoomChange,
                pointer.Y - (pointer.Y - _panOffset.Y) * zoomChange
            );
            
            InvalidateVisual();
            e.Handled = true;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        
        // Detect eraser tip from Wacom tablet (Windows Ink)
        if (point.Pointer.Type == PointerType.Pen)
        {
            if (point.Properties.IsEraser)
            {
                _wasEraserModeBeforePen = IsEraserMode;
                _isEraserModeAutoActivated = true;
                IsEraserMode = true;
            }
            else if (_isEraserModeAutoActivated)
            {
                // Only switch back to brush mode if eraser mode was auto-activated by eraser tip
                IsEraserMode = _wasEraserModeBeforePen;
                _isEraserModeAutoActivated = false;
            }
        }
        
        if (point.Properties.IsMiddleButtonPressed || (IsPanMode && point.Properties.IsLeftButtonPressed))
        {
            _isPanning = true;
            _lastPanPoint = point.Position;
            e.Pointer.Capture(this);
            return;
        }
        
        if (point.Properties.IsLeftButtonPressed)
        {
            var transformedPoint = ScreenToCanvas(point.Position);
            
            // Only allow image selection in select mode
            if (IsSelectMode)
            {
                var clickedImage = GetImageAtPoint(transformedPoint);
                if (clickedImage != null)
                {
                    // Deselect all images
                    foreach (var img in _images)
                        img.IsSelected = false;
                    
                    clickedImage.IsSelected = true;
                    _selectedImage = clickedImage;
                    _isMovingImage = true;
                    _lastImageMovePoint = transformedPoint;
                    _imageStartBounds = clickedImage.Bounds;
                    e.Pointer.Capture(this);
                    InvalidateVisual();
                    Focus();
                    return;
                }
                
                // Deselect images if clicking elsewhere
                if (_selectedImage != null)
                {
                    _selectedImage.IsSelected = false;
                    _selectedImage = null;
                    InvalidateVisual();
                }
                
                e.Pointer.Capture(this);
                return;
            }
            
            // Deselect images when not in select mode
            if (_selectedImage != null)
            {
                _selectedImage.IsSelected = false;
                _selectedImage = null;
                InvalidateVisual();
            }
            
            if (IsEraserMode)
            {
                _isDrawing = true;
                _currentEraseActions = new List<ICanvasAction>();
                PerformErase(transformedPoint);
            }
            else
            {
                _isDrawing = true;
                _currentStroke = new InkStroke
                {
                    Color = InkColor,
                    BaseThickness = InkThickness
                };

                var pressure = point.Properties.Pressure;
                _currentStroke.AddPoint(transformedPoint, pressure);
            }
            
            e.Pointer.Capture(this);
            InvalidateVisual();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var point = e.GetCurrentPoint(this);
        
        if (_isPanning)
        {
            var delta = point.Position - _lastPanPoint;
            _panOffset = new Point(_panOffset.X + delta.X, _panOffset.Y + delta.Y);
            _lastPanPoint = point.Position;
            _isCurrentlyPanning = true;
            InvalidateVisual();
            return;
        }

        if (_isMovingImage && _selectedImage != null)
        {
            var transformedPoint = ScreenToCanvas(point.Position);
            var delta = transformedPoint - _lastImageMovePoint;
            
            _selectedImage.Bounds = new Rect(
                _selectedImage.Bounds.X + delta.X,
                _selectedImage.Bounds.Y + delta.Y,
                _selectedImage.Bounds.Width,
                _selectedImage.Bounds.Height);
            
            _lastImageMovePoint = transformedPoint;
            InvalidateVisual();
            return;
        }

        if (_isDrawing)
        {
            var transformedPoint = ScreenToCanvas(point.Position);
            
            if (IsEraserMode)
            {
                PerformErase(transformedPoint);
            }
            else if (_currentStroke != null)
            {
                var pressure = point.Properties.Pressure;
                _currentStroke.AddPoint(transformedPoint, pressure);
            }
            
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        var point = e.GetCurrentPoint(this);
        
        // Restore eraser mode state if it was automatically set by eraser tip
        if (point.Pointer.Type == PointerType.Pen && point.Properties.IsEraser)
        {
            IsEraserMode = _wasEraserModeBeforePen;
        }

        if (_isPanning)
        {
            _isPanning = false;
            _isCurrentlyPanning = false;
            e.Pointer.Capture(null);
            InvalidateVisual();
            return;
        }

        if (_isMovingImage)
        {
            if (_selectedImage != null && _imageStartBounds != _selectedImage.Bounds)
            {
                var action = new MoveImageAction(_selectedImage, _imageStartBounds, _selectedImage.Bounds);
                ExecuteAction(action);
            }
            _isMovingImage = false;
            e.Pointer.Capture(null);
            return;
        }

        if (_isDrawing)
        {
            if (!IsEraserMode && _currentStroke != null)
            {
                var transformedPoint = ScreenToCanvas(point.Position);
                var pressure = point.Properties.Pressure;
                
                _currentStroke.AddPoint(transformedPoint, pressure);
                var action = new AddItemAction<InkStroke>(_currentStroke, AddStrokeInternal, RemoveStrokeInternal);
                ExecuteAction(action);
                _currentStroke = null;
            }
            else if (IsEraserMode && _currentEraseActions != null && _currentEraseActions.Count > 0)
            {
                var action = new CompoundAction(_currentEraseActions);
                ExecuteAction(action);
                _currentEraseActions = null;
            }
            
            _isDrawing = false;
            e.Pointer.Capture(null);
            InvalidateVisual();
        }
    }

    private Point ScreenToCanvas(Point screenPoint)
    {
        return new Point(
            (screenPoint.X - _panOffset.X) / _zoomLevel,
            (screenPoint.Y - _panOffset.Y) / _zoomLevel
        );
    }

    private Point CanvasToScreen(Point canvasPoint)
    {
        return new Point(
            canvasPoint.X * _zoomLevel + _panOffset.X,
            canvasPoint.Y * _zoomLevel + _panOffset.Y
        );
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Background != null)
        {
            context.FillRectangle(Background, new Rect(0, 0, Bounds.Width, Bounds.Height));
        }

        context.Custom(new InkCanvasDrawOperation(
            new Rect(0, 0, Bounds.Width, Bounds.Height), 
            _strokes, 
            _currentStroke, 
            ShowGrid, 
            GridCellSize,
            _panOffset,
            _zoomLevel,
            _images,
            _isPanning || _isCurrentlyPanning));
    }

    public void Clear()
    {
        foreach (var stroke in _strokes)
        {
            stroke.Dispose();
        }
        _strokes.Clear();
        _currentStroke = null;
        _isDrawing = false;
        _images.Clear();
        _selectedImage = null;
        _undoStack.Clear();
        _redoStack.Clear();
        InvalidateVisual();
    }

    public void Undo()
    {
        if (_undoStack.Count == 0)
            return;

        var action = _undoStack.Pop();
        action.Undo(this);
        _redoStack.Push(action);
        InvalidateVisual();
    }

    public void Redo()
    {
        if (_redoStack.Count == 0)
            return;

        var action = _redoStack.Pop();
        action.Execute(this);
        _undoStack.Push(action);
        InvalidateVisual();
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    internal void AddStrokeInternal(InkCanvas canvas, InkStroke stroke)
    {
        _strokes.Add(stroke);
    }

    internal void RemoveStrokeInternal(InkCanvas canvas, InkStroke stroke)
    {
        _strokes.Remove(stroke);
        stroke.Dispose();
    }

    internal void AddImageInternal(InkCanvas canvas, CanvasImage image)
    {
        _images.Add(image);
    }

    internal void RemoveImageInternal(InkCanvas canvas, CanvasImage image)
    {
        _images.Remove(image);
    }

    private void ExecuteAction(ICanvasAction action)
    {
        action.Execute(this);
        _undoStack.Push(action);
        _redoStack.Clear();
    }

    public void SaveToFile(string filePath)
    {
        var serializableImages = new List<SerializableImage>();
        
        foreach (var image in _images)
        {
            using var stream = new MemoryStream();
            image.Bitmap.Save(stream);
            var imageBytes = stream.ToArray();
            
            serializableImages.Add(new SerializableImage
            {
                ImageDataBase64 = Convert.ToBase64String(imageBytes),
                X = image.Bounds.X,
                Y = image.Bounds.Y,
                Width = image.Bounds.Width,
                Height = image.Bounds.Height
            });
        }
        
        var document = ChalkDocument.FromCanvas(_strokes, serializableImages);
        document.SaveToFile(filePath);
    }

    public void LoadFromFile(string filePath)
    {
        var document = ChalkDocument.LoadFromFile(filePath);
        
        _strokes.Clear();
        _images.Clear();
        _undoStack.Clear();
        _redoStack.Clear();
        
        foreach (var serializableStroke in document.Strokes)
        {
            _strokes.Add(serializableStroke.ToInkStroke());
        }
        
        foreach (var serializableImage in document.Images)
        {
            try
            {
                var imageBytes = Convert.FromBase64String(serializableImage.ImageDataBase64);
                using var ms = new MemoryStream(imageBytes);
                var bitmap = new Bitmap(ms);
                
                SKBitmap? skBitmap = null;
                try
                {
                    ms.Position = 0;
                    skBitmap = SKBitmap.Decode(ms);
                }
                catch { }
                
                var canvasImage = new CanvasImage
                {
                    Bitmap = bitmap,
                    CachedSKBitmap = skBitmap,
                    Bounds = new Rect(
                        serializableImage.X,
                        serializableImage.Y,
                        serializableImage.Width,
                        serializableImage.Height)
                };
                
                _images.Add(canvasImage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load image: {ex.Message}");
            }
        }
        
        InvalidateVisual();
    }

    private CanvasImage? GetImageAtPoint(Point point)
    {
        // Check in reverse order (top to bottom)
        for (int i = _images.Count - 1; i >= 0; i--)
        {
            if (_images[i].Bounds.Contains(point))
                return _images[i];
        }
        return null;
    }

    private void PerformErase(Point position)
    {
        EraseByStroke(position);
    }

    private void EraseByStroke(Point position)
    {
        var eraserRadius = 5.0; // Small fixed radius for precise erasing
        var strokesToRemove = _strokes.Where(stroke => StrokeIntersectsPoint(stroke, position, eraserRadius)).ToList();
        
        if (strokesToRemove.Count > 0)
        {
            var action = new RemoveItemsAction<InkStroke>(strokesToRemove, AddStrokeInternal, RemoveStrokeInternal);
            if (_currentEraseActions != null)
            {
                _currentEraseActions.Add(action);
            }
            action.Execute(this);
        }
    }

    private bool StrokeIntersectsPoint(InkStroke stroke, Point point, double radius)
    {
        if (stroke.Points.Count == 0)
            return false;
            
        // Check single point strokes
        if (stroke.Points.Count == 1)
        {
            var p = stroke.Points[0];
            var dx = p.Position.X - point.X;
            var dy = p.Position.Y - point.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            return distance <= radius;
        }
        
        // Check distance to line segments
        for (int i = 0; i < stroke.Points.Count - 1; i++)
        {
            var p1 = stroke.Points[i];
            var p2 = stroke.Points[i + 1];
            
            var distToSegment = DistanceToLineSegment(point, p1.Position, p2.Position);
            
            if (distToSegment <= radius)
                return true;
        }
        
        return false;
    }
    
    private double DistanceToLineSegment(Point p, Point lineStart, Point lineEnd)
    {
        var dx = lineEnd.X - lineStart.X;
        var dy = lineEnd.Y - lineStart.Y;
        
        if (dx == 0 && dy == 0)
        {
            // Line segment is a point
            var dpx = p.X - lineStart.X;
            var dpy = p.Y - lineStart.Y;
            return Math.Sqrt(dpx * dpx + dpy * dpy);
        }
        
        // Calculate parameter t that represents projection of point onto line
        var t = ((p.X - lineStart.X) * dx + (p.Y - lineStart.Y) * dy) / (dx * dx + dy * dy);
        t = Math.Max(0, Math.Min(1, t)); // Clamp to [0, 1] to stay on segment
        
        // Find closest point on segment
        var closestX = lineStart.X + t * dx;
        var closestY = lineStart.Y + t * dy;
        
        // Return distance to closest point
        var distX = p.X - closestX;
        var distY = p.Y - closestY;
        return Math.Sqrt(distX * distX + distY * distY);
    }
}
