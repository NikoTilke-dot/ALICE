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
            FontFamily = (FontFamilyBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Sans",
            Author = AuthorBox?.Text?.Trim() ?? string.Empty,
            Copyright = CopyrightBox?.Text?.Trim() ?? string.Empty,
            HeaderText = HeaderBox?.Text?.Trim() ?? string.Empty,
            FooterText = FooterBox?.Text?.Trim() ?? string.Empty,
            IncludeNotes = NotesBox?.IsChecked == true,
        };
        DialogResult = true;
    }

    private void Preset_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Vorlagen: Format + Kern-Optionen setzen (nur manuell abgeleitet)
        var idx = PresetBox.SelectedIndex;
        if (idx <= 0) return;
        void SetFormat(int fIdx) => FormatBox.SelectedIndex = fIdx;
        switch (idx)
        {
            case 1: SetFormat(1); FontFamilyBox.SelectedIndex = 1; FontSizeBox.SelectedIndex = 2; TitlePageBox.IsChecked = true; TocBox.IsChecked = true; PageNumbersBox.IsChecked = true; ChapterPageBox.IsChecked = true; break;
            case 2: SetFormat(0); FontFamilyBox.SelectedIndex = 1; FontSizeBox.SelectedIndex = 2; TitlePageBox.IsChecked = true; TocBox.IsChecked = true; PageNumbersBox.IsChecked = true; ChapterPageBox.IsChecked = true; break;
            case 3: SetFormat(7); TocBox.IsChecked = true; break;
            case 4: SetFormat(4); break;
            case 5: SetFormat(5); break;
            case 6: SetFormat(3); break;
            case 7: SetFormat(8); ScopeBox.SelectedIndex = 0; TocBox.IsChecked = true; break;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
