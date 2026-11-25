using System.Windows;
using System.Windows.Data;
using Alice.Core.Enums;
using Alice.Core.Models;

namespace Alice.AppConverters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is Visibility.Visible;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => (value is bool b && !b) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is not Visibility.Visible;
}

public sealed class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => (!string.IsNullOrWhiteSpace(value as string)) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is Visibility.Visible;
}

/// <summary>Manuskript-Art → Segoe-MDL2-Glyph (kein Emoji, konsistent mit Windows).</summary>
public sealed class NodeKindToGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is NodeKind k) return k switch
        {
            NodeKind.Chapter => "\uE8F1", // E823? -> hier Document
            NodeKind.Scene => "\uE8F1",
            NodeKind.SubScene => "\uE8F1",
            NodeKind.Part => "\uE8F1",
            NodeKind.Book => "\uE8F1",
            NodeKind.Series => "\uE8F1",
            NodeKind.Folder => "\uE8B7", // Folder
            NodeKind.Document => "\uE8F1",
            NodeKind.Note => "\uE70B", // Edit
            NodeKind.Research => "\uE721", // Search
            NodeKind.Archive => "\uE8B7",
            _ => "\uE8F1"
        };
        return "\uE8F1";
    }
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Manuskript-Art → kurzes Label.</summary>
public sealed class NodeKindToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is NodeKind k) return k switch
        {
            NodeKind.Chapter => "Kapitel",
            NodeKind.Scene => "Szene",
            NodeKind.SubScene => "Subszene",
            NodeKind.Part => "Teil",
            NodeKind.Book => "Buch",
            NodeKind.Series => "Serie",
            NodeKind.Folder => "Ordner",
            NodeKind.Document => "Dokument",
            NodeKind.Note => "Notiz",
            NodeKind.Research => "Recherche",
            NodeKind.Archive => "Archiv",
            _ => "Dokument"
        };
        return "Dokument";
    }
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Für Corkboard-Farbakzent: ManuscriptNode.Color (Hex) als Brush.</summary>
public sealed class ColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try { return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)); } catch { }
        }
        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Transparent);
    }
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Fortschritt 0-100 → "42 %" für Anzeige.</summary>
public sealed class ProgressToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is double d ? $"{d:F0} %" : "0 %";
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}
