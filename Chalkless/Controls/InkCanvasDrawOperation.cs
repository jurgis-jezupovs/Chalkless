using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Chalkless.Models;
using SkiaSharp;

namespace Chalkless.Controls;

internal class InkCanvasDrawOperation : ICustomDrawOperation
{
    private readonly Rect _bounds;
    private readonly List<InkStroke> _strokes;
    private readonly InkStroke? _currentStroke;
    private readonly bool _showGrid;
    private readonly double _gridCellSize;
    private readonly Point _panOffset;
    private readonly double _zoomLevel;
    private readonly List<CanvasImage> _images;
    private readonly bool _isInteracting;

    public InkCanvasDrawOperation(Rect bounds, List<InkStroke> strokes, InkStroke? currentStroke, bool showGrid,
        double gridCellSize, Point panOffset, double zoomLevel, List<CanvasImage> images, bool isInteracting)
    {
        _bounds = bounds;
        _strokes = new List<InkStroke>(strokes);
        _currentStroke = currentStroke;
        _showGrid = showGrid;
        _gridCellSize = gridCellSize;
        _panOffset = panOffset;
        _zoomLevel = zoomLevel;
        _images = new List<CanvasImage>(images);
        _isInteracting = isInteracting;
    }

    public Rect Bounds => _bounds;

    public void Dispose()
    {
    }

    public bool Equals(ICustomDrawOperation? other) => false;

    public bool HitTest(Point p) => _bounds.Contains(p);

    public void Render(ImmediateDrawingContext context)
    {
        var leaseFeature = context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) as ISkiaSharpApiLeaseFeature;
        if (leaseFeature == null)
            return;

        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;

        canvas.Save();
        canvas.Translate((float)_panOffset.X, (float)_panOffset.Y);
        canvas.Scale((float)_zoomLevel, (float)_zoomLevel);

        if (_showGrid)
        {
            DrawGrid(canvas);
        }

        // Draw images first (behind strokes) - only render visible images
        // Use Low quality for maximum sharpness when zoomed out (less blur from interpolation)
        // High quality for smooth scaling when zoomed in
        using var imagePaint = new SKPaint
        {
            IsAntialias = _zoomLevel >= 0.8,
        };
        var imageSampling = _zoomLevel < 0.8 ? SKSamplingOptions.Default : new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);

        foreach (var image in _images)
        {
            if (IsImageInViewport(image))
            {
                DrawImage(canvas, image, imagePaint, imageSampling);
            }
        }

        var viewportRect = GetViewportRect();

        foreach (var stroke in _strokes)
        {
            if (IsStrokeInViewport(stroke, viewportRect))
            {
                DrawStroke(canvas, stroke);
            }
        }

        if (_currentStroke != null)
        {
            DrawStroke(canvas, _currentStroke);
        }

        canvas.Restore();
    }

    private Rect GetViewportRect()
    {
        var viewportLeft = -_panOffset.X / _zoomLevel;
        var viewportTop = -_panOffset.Y / _zoomLevel;
        var viewportWidth = _bounds.Width / _zoomLevel;
        var viewportHeight = _bounds.Height / _zoomLevel;

        return new Rect(viewportLeft, viewportTop, viewportWidth, viewportHeight);
    }

    private bool IsStrokeInViewport(InkStroke stroke, Rect viewport)
    {
        var strokeBounds = stroke.Bounds;
        return viewport.Intersects(strokeBounds);
    }

    private bool IsImageInViewport(CanvasImage image)
    {
        var viewportLeft = -_panOffset.X / _zoomLevel;
        var viewportTop = -_panOffset.Y / _zoomLevel;
        var viewportRight = (_bounds.Width - _panOffset.X) / _zoomLevel;
        var viewportBottom = (_bounds.Height - _panOffset.Y) / _zoomLevel;

        var imageBounds = image.Bounds;

        return !(imageBounds.Right < viewportLeft ||
                 imageBounds.Left > viewportRight ||
                 imageBounds.Bottom < viewportTop ||
                 imageBounds.Top > viewportBottom);
    }

    private void DrawImage(SKCanvas canvas, CanvasImage canvasImage, SKPaint paint, SKSamplingOptions sampling)
    {
        var bounds = canvasImage.Bounds;
        var skBitmap = canvasImage.CachedSKBitmap;

        // Use cached SKBitmap for performance
        if (skBitmap != null)
        {
            var destRect = new SKRect(
                (float)bounds.X,
                (float)bounds.Y,
                (float)(bounds.X + bounds.Width),
                (float)(bounds.Y + bounds.Height));

            canvas.DrawImage(SKImage.FromBitmap(skBitmap), destRect, sampling, paint);

            // Draw selection border if selected
            if (canvasImage.IsSelected)
            {
                using var borderPaint = new SKPaint
                {
                    Color = new SKColor(0, 120, 215, 255),
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 2 / (float)_zoomLevel,
                    PathEffect = SKPathEffect.CreateDash(new float[] { 5, 5 }, 0)
                };

                canvas.DrawRect(destRect, borderPaint);
            }
        }
    }

    private void DrawGrid(SKCanvas canvas)
    {
        using var paint = new SKPaint
        {
            Color = new SKColor(60, 60, 60),
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1 / (float)_zoomLevel
        };

        var cellSize = (float)_gridCellSize;

        var viewportLeft = (float)(-_panOffset.X / _zoomLevel);
        var viewportTop = (float)(-_panOffset.Y / _zoomLevel);
        var viewportRight = (float)((_bounds.Width - _panOffset.X) / _zoomLevel);
        var viewportBottom = (float)((_bounds.Height - _panOffset.Y) / _zoomLevel);

        var startX = (float)(Math.Floor(viewportLeft / cellSize) * cellSize);
        var startY = (float)(Math.Floor(viewportTop / cellSize) * cellSize);

        for (float x = startX; x <= viewportRight; x += cellSize)
        {
            canvas.DrawLine(x, viewportTop, x, viewportBottom, paint);
        }

        for (float y = startY; y <= viewportBottom; y += cellSize)
        {
            canvas.DrawLine(viewportLeft, y, viewportRight, y, paint);
        }
    }

    private void DrawStroke(SKCanvas canvas, InkStroke stroke)
    {
        if (stroke.Points.Count == 0)
            return;

        using var paint = new SKPaint
        {
            Color = new SKColor(stroke.Color.R, stroke.Color.G, stroke.Color.B, stroke.Color.A),
            IsAntialias = !_isInteracting,
            Style = SKPaintStyle.Stroke,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };

        if (stroke.Points.Count == 1)
        {
            var point = stroke.Points[0];
            var thickness = stroke.GetThicknessAtPoint(point.Pressure);
            paint.StrokeWidth = (float)thickness;
            paint.Style = SKPaintStyle.Fill;
            var path = stroke.GetOrCreatePath();
            canvas.DrawPath(path, paint);
        }
        else
        {
            var avgThickness = 0.0;
            foreach (var p in stroke.Points)
            {
                avgThickness += stroke.GetThicknessAtPoint(p.Pressure);
            }

            avgThickness /= stroke.Points.Count;

            paint.StrokeWidth = (float)avgThickness;
            var path = stroke.GetOrCreatePath();
            canvas.DrawPath(path, paint);
        }
    }
}