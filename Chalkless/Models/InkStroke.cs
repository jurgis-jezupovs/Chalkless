using Avalonia;
using Avalonia.Media;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace Chalkless.Models;

public class InkPoint
{
    public Point Position { get; set; }
    public double Pressure { get; set; }

    public InkPoint(Point position, double pressure)
    {
        Position = position;
        Pressure = pressure;
    }
}

public class InkStroke
{
    public List<InkPoint> Points { get; set; } = new();
    public Color Color { get; set; } = Colors.Black;
    public double BaseThickness { get; set; } = 2.0;
    
    private Rect? _cachedBounds;
    private SKPath? _cachedPath;
    private bool _isDirty = true;

    public Rect Bounds
    {
        get
        {
            if (_cachedBounds == null)
            {
                CalculateBounds();
            }
            return _cachedBounds!.Value;
        }
    }

    public SKPath GetOrCreatePath()
    {
        if (_cachedPath == null || _isDirty)
        {
            _cachedPath?.Dispose();
            _cachedPath = CreatePath();
            _isDirty = false;
        }
        return _cachedPath;
    }

    public void InvalidateCache()
    {
        _isDirty = true;
        _cachedBounds = null;
    }

    public void AddPoint(Point position, double pressure)
    {
        Points.Add(new InkPoint(position, pressure));
        InvalidateCache();
    }

    public double GetThicknessAtPoint(double pressure)
    {
        return BaseThickness * (0.5 + pressure * 0.5);
    }

    private void CalculateBounds()
    {
        if (Points.Count == 0)
        {
            _cachedBounds = new Rect(0, 0, 0, 0);
            return;
        }

        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        var maxThickness = BaseThickness * 1.5;

        foreach (var point in Points)
        {
            minX = Math.Min(minX, point.Position.X);
            minY = Math.Min(minY, point.Position.Y);
            maxX = Math.Max(maxX, point.Position.X);
            maxY = Math.Max(maxY, point.Position.Y);
        }

        _cachedBounds = new Rect(
            minX - maxThickness,
            minY - maxThickness,
            maxX - minX + maxThickness * 2,
            maxY - minY + maxThickness * 2);
    }

    private SKPath CreatePath()
    {
        var path = new SKPath();
        
        if (Points.Count == 0)
            return path;

        if (Points.Count == 1)
        {
            var point = Points[0];
            var thickness = GetThicknessAtPoint(point.Pressure);
            path.AddCircle((float)point.Position.X, (float)point.Position.Y, (float)(thickness / 2));
        }
        else
        {
            path.MoveTo((float)Points[0].Position.X, (float)Points[0].Position.Y);
            for (int i = 1; i < Points.Count; i++)
            {
                path.LineTo((float)Points[i].Position.X, (float)Points[i].Position.Y);
            }
        }

        return path;
    }

    public void Dispose()
    {
        _cachedPath?.Dispose();
        _cachedPath = null;
    }
}
