using Avalonia;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Chalkless.Models;

public class CanvasImage
{
    public Bitmap Bitmap { get; set; } = null!;
    public SKBitmap? CachedSKBitmap { get; set; }
    public Rect Bounds { get; set; }
    public bool IsSelected { get; set; }
}