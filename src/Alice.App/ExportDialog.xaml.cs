using System.Windows;
using System.Windows.Controls;
using Alice.Core.Enums;

namespace Alice.App;

/// <summary>Export-Dialog: Format, Umfang, PDF-Optionen. Ergebnis via DialogResult + Eigenschaften.</summary>
public partial class ExportDialog : Window
{
    public ExportFormat Format { get; private set; } = ExportFormat.Pdf;
    public ExportOptions Options { get; private set; } = new();

    public ExportDialog()
    {
        InitializeComponent();
        FormatBox.SelectedIndex = 0; ScopeBox.SelectedIndex = 0; PageFormatBox.SelectedIndex = 0; FontSizeBox.SelectedIndex = 1;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var item = FormatBox.SelectedItem as ComboBoxItem;
        Format = item?.Tag as string switch
        {
            "Docx" => ExportFormat.Docx, "Rtf" => ExportFormat.Rtf, "Odt" => ExportFormat.Odt,
            "Txt" => ExportFormat.Txt, "Markdown" => ExportFormat.Markdown, "Html" => ExportFormat.Html,
            "Epub" => ExportFormat.Epub, "Zip" => ExportFormat.Zip, _ => ExportFormat.Pdf,
        };
        Options = new ExportOptions
        {
            Scope = (ScopeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Gesamtes Buch",
            IncludeToc = TocBox.IsChecked == true,
            TitlePage = TitlePageBox.IsChecked == true,
            PageNumbers = PageNumbersBox.IsChecked == true,
            ChapterNewPage = ChapterPageBox.IsChecked == true,
            PageFormat = (PageFormatBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "A4",
            FontSize = int.TryParse((FontSizeBox.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out var fs) ? fs : 11,
        };
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
