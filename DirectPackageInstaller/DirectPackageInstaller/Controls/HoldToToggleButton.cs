using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;

namespace DirectPackageInstaller.Controls;

public class HoldToToggleButton : Button, IStyleable
{
    Type IStyleable.StyleKey => typeof(ToggleButton);

    public ScrollViewer? Parent
    {
        get => GetValue(ParentProperty);
        set => SetValue(ParentProperty, value);
    }
    
    /// <summary>
    /// Defines the <see cref="Parent"/> property.
    /// </summary>
    public static readonly StyledProperty<ScrollViewer> ParentProperty =
        AvaloniaProperty.Register<HoldToToggleButton, ScrollViewer>(nameof(Parent));

    private DateTime? PressBegin;
    
    /// <summary>
    /// Defines the <see cref="IsChecked"/> property.
    /// </summary>
    public static readonly DirectProperty<HoldToToggleButton, bool?> IsCheckedProperty =
        AvaloniaProperty.RegisterDirect<HoldToToggleButton, bool?>(
            nameof(IsChecked),
            o => o.IsChecked,
            (o, v) => o.IsChecked = v,
            unsetValue: null,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Defines the <see cref="IsThreeState"/> property.
    /// </summary>
    public static readonly StyledProperty<bool> IsThreeStateProperty =
        AvaloniaProperty.Register<HoldToToggleButton, bool>(nameof(IsThreeState));

    /// <summary>
    /// Defines the <see cref="Checked"/> event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> CheckedEvent =
        RoutedEvent.Register<HoldToToggleButton, RoutedEventArgs>(nameof(Checked), RoutingStrategies.Bubble);

    /// <summary>
    /// Defines the <see cref="Unchecked"/> event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> UncheckedEvent =
        RoutedEvent.Register<HoldToToggleButton, RoutedEventArgs>(nameof(Unchecked), RoutingStrategies.Bubble);

    /// <summary>
    /// Defines the <see cref="Unchecked"/> event.
    /// </summary>
    public static readonly RoutedEvent<RoutedEventArgs> IndeterminateEvent =
        RoutedEvent.Register<HoldToToggleButton, RoutedEventArgs>(nameof(Indeterminate), RoutingStrategies.Bubble);

    private bool? _isChecked = false;

    static HoldToToggleButton()
    {
        IsCheckedProperty.Changed.AddClassHandler<HoldToToggleButton>((x, e) => x.OnIsCheckedChanged(e));
    }

    public HoldToToggleButton()
    {
        UpdatePseudoClasses(IsChecked);
        base.ClickMode = ClickMode.Release;
    }

    public new ClickMode ClickMode { get; }

    /// <summary>
    /// Raised when a <see cref="HoldToToggleButton"/> is checked.
    /// </summary>
    public event EventHandler<RoutedEventArgs> Checked
    {
        add => AddHandler(CheckedEvent, value);
        remove => RemoveHandler(CheckedEvent, value);
    }

    /// <summary>
    /// Raised when a <see cref="HoldToToggleButton"/> is unchecked.
    /// </summary>
    public event EventHandler<RoutedEventArgs> Unchecked
    {
        add => AddHandler(UncheckedEvent, value);
        remove => RemoveHandler(UncheckedEvent, value);
    }

    /// <summary>
    /// Raised when a <see cref="HoldToToggleButton"/> is neither checked nor unchecked.
    /// </summary>
    public event EventHandler<RoutedEventArgs> Indeterminate
    {
        add => AddHandler(IndeterminateEvent, value);
        remove => RemoveHandler(IndeterminateEvent, value);
    }

    /// <summary>
    /// Gets or sets whether the <see cref="HoldToToggleButton"/> is checked.
    /// </summary>
    public bool? IsChecked
    {
        get => _isChecked;
        set 
        { 
            SetAndRaise(IsCheckedProperty, ref _isChecked, value);
            UpdatePseudoClasses(IsChecked);
        }
    }

    /// <summary>
    /// Gets or sets a value that indicates whether the control supports three states.
    /// </summary>
    public bool IsThreeState
    {
        get => GetValue(IsThreeStateProperty);
        set => SetValue(IsThreeStateProperty, value);
    }

    private double InitialYOffset = 0;
    private double InitialPressY = 0;
    private bool PointerPressed = false;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (Parent != null)
        {
            InitialYOffset = Parent.Offset.Y;
            InitialPressY = e.GetPosition(null).Y;
        }

        PointerPressed = true;
        PressBegin = DateTime.Now;
        base.OnPointerPressed(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (PointerPressed && Parent != null)
        {
            var Diff = InitialPressY - e.GetPosition(null).Y;
            Parent!.Offset = new Vector(Parent!.Offset.X, InitialYOffset + Diff);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        PointerPressed = false;
        
        if (PressBegin is not null && (DateTime.Now - PressBegin!.Value).TotalMilliseconds >= 500)
            Toggle();
        else
            base.OnPointerReleased(e);
        
        Parent?.RaiseEvent(e);
    }

    /// <summary>
    /// Toggles the <see cref="IsChecked"/> property.
    /// </summary>
    public virtual void Toggle()
    {
        if (IsChecked.HasValue)
        {
            if (IsChecked.Value)
            {
                if (IsThreeState)
                {
                    IsChecked = null;
                }
                else
                {
                    IsChecked = false;
                }
            }
            else
            {
                IsChecked = true;
            }
        }
        else
        {
            IsChecked = false;
        }
    }

    /// <summary>
    /// Called when <see cref="IsChecked"/> becomes true.
    /// </summary>
    /// <param name="e">Event arguments for the routed event that is raised by the default implementation of this method.</param>
    protected virtual void OnChecked(RoutedEventArgs e)
    {
        RaiseEvent(e);
    }

    /// <summary>
    /// Called when <see cref="IsChecked"/> becomes false.
    /// </summary>
    /// <param name="e">Event arguments for the routed event that is raised by the default implementation of this method.</param>
    protected virtual void OnUnchecked(RoutedEventArgs e)
    {
        RaiseEvent(e);
    }

    /// <summary>
    /// Called when <see cref="IsChecked"/> becomes null.
    /// </summary>
    /// <param name="e">Event arguments for the routed event that is raised by the default implementation of this method.</param>
    protected virtual void OnIndeterminate(RoutedEventArgs e)
    {
        RaiseEvent(e);
    }

    private void OnIsCheckedChanged(AvaloniaPropertyChangedEventArgs e)
    {
        var newValue = (bool?)e.NewValue;

        switch (newValue)
        {
            case true:
                OnChecked(new RoutedEventArgs(CheckedEvent));
                break;
            case false:
                OnUnchecked(new RoutedEventArgs(UncheckedEvent));
                break;
            default:
                OnIndeterminate(new RoutedEventArgs(IndeterminateEvent));
                break;
        }
    }

    private void UpdatePseudoClasses(bool? isChecked)
    {
        PseudoClasses.Set(":checked", isChecked == true);
        PseudoClasses.Set(":unchecked", isChecked == false);
        PseudoClasses.Set(":indeterminate", isChecked == null);
    }
}