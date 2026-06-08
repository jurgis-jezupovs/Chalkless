using System;
using System.Collections.Generic;
using Avalonia;
using Chalkless.Models;

namespace Chalkless.Controls;

public interface ICanvasAction
{
    void Execute(InkCanvas canvas);
    void Undo(InkCanvas canvas);
}

/// <summary>
/// Generic action for adding a single item to the canvas
/// </summary>
public class AddItemAction<T> : ICanvasAction where T : class
{
    private readonly T _item;
    private readonly Action<InkCanvas, T> _addMethod;
    private readonly Action<InkCanvas, T> _removeMethod;

    public AddItemAction(T item, Action<InkCanvas, T> addMethod, Action<InkCanvas, T> removeMethod)
    {
        _item = item;
        _addMethod = addMethod;
        _removeMethod = removeMethod;
    }

    public void Execute(InkCanvas canvas)
    {
        _addMethod(canvas, _item);
    }

    public void Undo(InkCanvas canvas)
    {
        _removeMethod(canvas, _item);
    }
}

/// <summary>
/// Generic action for removing a single item from the canvas
/// </summary>
public class RemoveItemAction<T> : ICanvasAction where T : class
{
    private readonly T _item;
    private readonly Action<InkCanvas, T> _addMethod;
    private readonly Action<InkCanvas, T> _removeMethod;

    public RemoveItemAction(T item, Action<InkCanvas, T> addMethod, Action<InkCanvas, T> removeMethod)
    {
        _item = item;
        _addMethod = addMethod;
        _removeMethod = removeMethod;
    }

    public void Execute(InkCanvas canvas)
    {
        _removeMethod(canvas, _item);
    }

    public void Undo(InkCanvas canvas)
    {
        _addMethod(canvas, _item);
    }
}

/// <summary>
/// Generic action for removing multiple items from the canvas
/// </summary>
public class RemoveItemsAction<T> : ICanvasAction where T : class
{
    private readonly List<T> _items;
    private readonly Action<InkCanvas, T> _addMethod;
    private readonly Action<InkCanvas, T> _removeMethod;

    public RemoveItemsAction(List<T> items, Action<InkCanvas, T> addMethod, Action<InkCanvas, T> removeMethod)
    {
        _items = new List<T>(items);
        _addMethod = addMethod;
        _removeMethod = removeMethod;
    }

    public void Execute(InkCanvas canvas)
    {
        foreach (var item in _items)
        {
            _removeMethod(canvas, item);
        }
    }

    public void Undo(InkCanvas canvas)
    {
        foreach (var item in _items)
        {
            _addMethod(canvas, item);
        }
    }
}

/// <summary>
/// Action for moving an image to a new position
/// </summary>
public class MoveImageAction : ICanvasAction
{
    private readonly CanvasImage _image;
    private readonly Rect _oldBounds;
    private readonly Rect _newBounds;

    public MoveImageAction(CanvasImage image, Rect oldBounds, Rect newBounds)
    {
        _image = image;
        _oldBounds = oldBounds;
        _newBounds = newBounds;
    }

    public void Execute(InkCanvas canvas)
    {
        _image.Bounds = _newBounds;
    }

    public void Undo(InkCanvas canvas)
    {
        _image.Bounds = _oldBounds;
    }
}

/// <summary>
/// Compound action that executes multiple actions in sequence
/// </summary>
public class CompoundAction : ICanvasAction
{
    private readonly List<ICanvasAction> _actions;

    public CompoundAction(List<ICanvasAction> actions)
    {
        _actions = new List<ICanvasAction>(actions);
    }

    public void Execute(InkCanvas canvas)
    {
        foreach (var action in _actions)
        {
            action.Execute(canvas);
        }
    }

    public void Undo(InkCanvas canvas)
    {
        for (int i = _actions.Count - 1; i >= 0; i--)
        {
            _actions[i].Undo(canvas);
        }
    }
}
