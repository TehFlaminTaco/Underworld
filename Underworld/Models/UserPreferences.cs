using System;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Layout;
using Underworld.ViewModels;
using Underworld.Views;

namespace Underworld.Models;

[Serializable]
public class UserPreferences
{
    [Label("Show a warning that you have no profile when:")]
    [Property("...Launching")]
    public bool ShowNoProfileLaunchWarning { get; set; } = true;
    [Property("...Exiting")]
    public bool ShowNoProfileExitWarning { get; set; } = true;

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
        var dialog = new Window
        {
            Title = "User Preferences",
            Width = 400,
            Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var grid = new Grid()
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Margin = new Avalonia.Thickness(10)
        };
        var scroller = new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(10, 0)
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
        dialog.Content = grid;

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
            Margin = new Avalonia.Thickness(0, 10, 0, 0)
        };
        var okButton = new Button
        { Content = "OK",
            IsDefault = true,
            Margin = new Avalonia.Thickness(0, 0, 10, 0)
        };
        okButton.Click += (_, _) => {
            shouldRevert = false;
            Save();
            dialog.Close();
        };
        var cancelButton = new Button
        { Content = "Cancel",
            IsCancel = true
        };
        cancelButton.Click += (_, _) => {
            dialog.Close();
        };
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        grid.Children.Add(buttonPanel);

        Grid.SetRow(buttonPanel, 1);
        
        dialog.ShowDialog(parent);
    }

    private class LabelAttribute : ControlAttribute
    {
        public string Text { get; }

        public override Control CreateControl(Panel parent, object dataContext, PropertyInfo property)
        {
            return new TextBlock
            {
                Text = Text,
                Margin = new Avalonia.Thickness(0, 10, 0, 5)
            };
        }

        public LabelAttribute(string text)
        {
            Text = text;
        }
    }

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
                    Margin = new Avalonia.Thickness(0, 10, 0, 5)
                };
                var label = new TextBlock
                {
                    Text = Name,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var checkbox = new CheckBox
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    IsChecked = (bool)property.GetValue(dataContext)!
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