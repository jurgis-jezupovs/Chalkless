using Avalonia;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Chalkless.Models;

[XmlRoot("ChalkDocument")]
public class ChalkDocument
{
    [XmlArray("Strokes")]
    [XmlArrayItem("Stroke")]
    public List<SerializableStroke> Strokes { get; set; } = new();

    [XmlArray("Images")]
    [XmlArrayItem("Image")]
    public List<SerializableImage> Images { get; set; } = new();

    public static ChalkDocument FromCanvas(List<InkStroke> strokes, List<SerializableImage> images)
    {
        var doc = new ChalkDocument();
        
        foreach (var stroke in strokes)
        {
            doc.Strokes.Add(SerializableStroke.FromInkStroke(stroke));
        }
        
        doc.Images = images;
        
        return doc;
    }

    public void SaveToFile(string filePath)
    {
        var serializer = new XmlSerializer(typeof(ChalkDocument));
        using var writer = new StreamWriter(filePath);
        serializer.Serialize(writer, this);
    }

    public static ChalkDocument LoadFromFile(string filePath)
    {
        var serializer = new XmlSerializer(typeof(ChalkDocument));
        using var reader = new StreamReader(filePath);
        return (ChalkDocument)serializer.Deserialize(reader)!;
    }
}

[XmlType("Stroke")]
public class SerializableStroke
{
    [XmlArray("Points")]
    [XmlArrayItem("Point")]
    public List<SerializablePoint> Points { get; set; } = new();

    [XmlElement("Color")]
    public string ColorHex { get; set; } = "#FFFFFFFF";

    [XmlElement("Thickness")]
    public double BaseThickness { get; set; } = 2.0;

    public SerializableStroke()
    {
    }

    public static SerializableStroke FromInkStroke(InkStroke stroke)
    {
        var serializable = new SerializableStroke
        {
            ColorHex = $"#{stroke.Color.A:X2}{stroke.Color.R:X2}{stroke.Color.G:X2}{stroke.Color.B:X2}",
            BaseThickness = stroke.BaseThickness
        };

        foreach (var point in stroke.Points)
        {
            serializable.Points.Add(new SerializablePoint
            {
                X = point.Position.X,
                Y = point.Position.Y,
                Pressure = point.Pressure
            });
        }

        return serializable;
    }

    public InkStroke ToInkStroke()
    {
        var stroke = new InkStroke
        {
            Color = Color.Parse(ColorHex),
            BaseThickness = BaseThickness
        };

        foreach (var point in Points)
        {
            stroke.AddPoint(new Point(point.X, point.Y), point.Pressure);
        }

        return stroke;
    }
}

[XmlType("Point")]
public class SerializablePoint
{
    [XmlAttribute("X")]
    public double X { get; set; }

    [XmlAttribute("Y")]
    public double Y { get; set; }

    [XmlAttribute("Pressure")]
    public double Pressure { get; set; }
}

[XmlType("Image")]
public class SerializableImage
{
    [XmlElement("ImageData")]
    public string ImageDataBase64 { get; set; } = string.Empty;

    [XmlElement("X")]
    public double X { get; set; }

    [XmlElement("Y")]
    public double Y { get; set; }

    [XmlElement("Width")]
    public double Width { get; set; }

    [XmlElement("Height")]
    public double Height { get; set; }
}
