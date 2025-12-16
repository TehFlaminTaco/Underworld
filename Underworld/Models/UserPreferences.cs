using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Underworld.ViewModels;
using Underworld.Views;

namespace Underworld.Models;

[Serializable]
public class UserPreferences
{
    //////////////////// Preferences ////////////////////
    [Property("Preferred Run Game Launch Method")]
    public LaunchPreference PreferredLaunchMethod { get; set; } = LaunchPreference.Run;
    [Property("Exit Launcher when Game Starts")]
    public bool ExitLauncherOnRun { get; set; } = false;
    [Label("Show a warning that you have no profile when:")]
    [Property("...Launching")]
    public bool ShowNoProfileLaunchWarning { get; set; } = true;
    [Property("...Exiting")]
    public bool ShowNoProfileExitWarning { get; set; } = true;



    //////////////////// Methods ////////////////////
    public void Save()
    {
        var configEntry = Config.Setup("UserPreferences", this);
        configEntry.Set(this);
    }

    public static UserPreferences Load()
    {
        var entry = Config.Setup("UserPreferences", new UserPreferences());
        return entry.Get() ?? new UserPreferences();
    }

    public void ShowPreferencesWindow(MainWindow parent)
    {
        bool shouldRevert = true;
        var windowBackground = GetBrush("WindowBackgroundBrush");
        var cardBackground = GetBrush("CardBackgroundBrush");
        var cardBorder = GetBrush("CardBorderBrush");
        var dialog = new Window
        {
            Title = "User Preferences",
            Width = 400,
            Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = windowBackground
        };
        var grid = new Grid()
        {
            RowDefinitions = new RowDefinitions("*,Auto")
        };
        var scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Background = cardBackground
        };
        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(0),
            Spacing = 8
        };

        var preferencesAsIs = Load();
        dialog.Closed += (_, _) =>
        {
            if (shouldRevert)
            {
                // Restore previous settings
                preferencesAsIs.Save();
                MainWindowViewModel.UserPreferences = preferencesAsIs;
            }
        };

        // Make the panel and the scroller as big as possible
        panel.HorizontalAlignment = HorizontalAlignment.Stretch;
        panel.VerticalAlignment = VerticalAlignment.Stretch;
        scroller.HorizontalAlignment = HorizontalAlignment.Stretch;
        scroller.VerticalAlignment = VerticalAlignment.Stretch;
        scroller.Content = panel;
        grid.Children.Add(scroller);
        var contentBorder = new Border
        {
            Background = cardBackground ?? windowBackground,
            BorderBrush = cardBorder,
            BorderThickness = cardBorder is null ? new Avalonia.Thickness(0) : new Avalonia.Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Avalonia.Thickness(12)
        };
        contentBorder.Child = grid;
        dialog.Content = contentBorder;

        var attributes = typeof(UserPreferences)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(prop => prop.GetCustomAttributes<ControlAttribute>()
                .Select(attr => (attribute: attr, property: prop)));
        foreach (var attr in attributes)
        {
            var ctrl = attr.attribute.CreateControl(panel, this, attr.property);
            panel.Children.Add(ctrl);
        }


        // Cancel/OK buttons
        var buttonPanel = new StackPanel
        { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Avalonia.Thickness(0, 10, 0, 0),
            Spacing = 8
        };
        var okButton = new Button
        { Content = "OK",
            IsDefault = true
        };
        okButton.Classes.Add("Primary");
        okButton.Click += (_, _) => {
            shouldRevert = false;
            Save();
            MainWindowViewModel.UserPreferences = this;
            dialog.Close();
        };
        var cancelButton = new Button
        { Content = "Cancel",
            IsCancel = true
        };
        cancelButton.Classes.Add("Secondary");
        cancelButton.Click += (_, _) => {
            dialog.Close();
        };
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        grid.Children.Add(buttonPanel);

        Grid.SetRow(buttonPanel, 1);
        
        dialog.ShowDialog(parent);
    }

    private static IBrush? GetBrush(string resourceKey)
    {
        return Avalonia.Application.Current?.FindResource(resourceKey) as IBrush;
    }

    private static IBrush? PrimaryText => GetBrush("PrimaryTextBrush");
    private static IBrush? MutedText => GetBrush("MutedTextBrush");
    private static IBrush? ControlBackground => GetBrush("ControlBackgroundBrush");
    private static IBrush? ControlBorder => GetBrush("ControlBorderBrush");

    ///////////////// Helper Classes ////////////////////
    public enum LaunchPreference
    {
        Run,
        LoadLastSave,
        [Name("Show Mini-Launcher")]
        ShowMiniLauncher
    }
    
    private class NameAttribute : Attribute
    {
        public string Text { get; }

        public NameAttribute(string text)
        {
            Text = text;
        }
    }

    private class LabelAttribute : ControlAttribute
    {
        public string Text { get; }

        public override Control CreateControl(Panel parent, object dataContext, PropertyInfo property)
        {
            return new TextBlock
            {
                Text = Text,
                Margin = new Avalonia.Thickness(0, 0, 0, 5),
                Foreground = MutedText ?? Brushes.Gray
            };
        }

        public LabelAttribute(string text)
        {
            Text = text;
        }
    }

    private static readonly Regex lowerUpperRegex = new Regex("([a-z])([A-Z])");
    private class PropertyAttribute : ControlAttribute
    {
        public string Name { get; }
        public override Control CreateControl(Panel parent, object dataContext, PropertyInfo property)
        {
            if (property.PropertyType == typeof(bool))
            {
                // Label on the left, checkbox on the right
                var panel = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*, Auto"),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Avalonia.Thickness(0, 0, 0, 5)
                };
                var label = new TextBlock
                {
                    Text = Name,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = PrimaryText ?? Brushes.White
                };
                var checkbox = new CheckBox
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    IsChecked = (bool)property.GetValue(dataContext)!,
                    Foreground = PrimaryText ?? Brushes.White,
                    Background = ControlBackground ?? Brushes.Transparent
                };
                Grid.SetColumn(label, 0);
                Grid.SetColumn(checkbox, 1);
                checkbox.IsCheckedChanged += (_, _) =>
                {
                    property.SetValue(dataContext, checkbox.IsChecked == true);
                };
                panel.Children.Add(label);
                panel.Children.Add(checkbox);
                return panel;
            }

            if (property.PropertyType.IsEnum)
            {
                // Label on the left, combobox on the right
                var panel = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*, Auto"),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Avalonia.Thickness(0, 0, 0, 5)
                };
                var label = new TextBlock
                {
                    Text = Name,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = PrimaryText ?? Brushes.White
                };
                var comboBox = new ComboBox
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    ItemsSource = Enum.GetValues(property.PropertyType).Cast<object>().Select(e =>
                    {
                        var nameAttr = e.GetType()
                            .GetField(e.ToString()!)!
                            .GetCustomAttribute<NameAttribute>();
                        return nameAttr != null ? nameAttr.Text : lowerUpperRegex.Replace(e.ToString()!, "$1 $2");
                    }).ToList(),
                    Foreground = PrimaryText ?? Brushes.White,
                    Background = ControlBackground ?? Brushes.Transparent,
                    BorderBrush = ControlBorder ?? Brushes.Gray
                };
                comboBox.SelectedIndex = Array.IndexOf(Enum.GetValues(property.PropertyType), property.GetValue(dataContext)!);
                Grid.SetColumn(label, 0);
                Grid.SetColumn(comboBox, 1);
                comboBox.SelectionChanged += (_, _) =>
                {
                    var selectedValue = Enum.GetValues(property.PropertyType).GetValue(comboBox.SelectedIndex);
                    property.SetValue(dataContext, selectedValue);
                };
                panel.Children.Add(label);
                panel.Children.Add(comboBox);
                return panel;
            }

            throw new NotSupportedException($"Property type '{property.PropertyType}' is not supported by PropertyAttribute.");
        }

        public PropertyAttribute(string name)
        {
            Name = name;
        }
    }
}

public abstract class ControlAttribute : Attribute
{
    public abstract Control CreateControl(Panel parent, object dataContext, PropertyInfo property);
}