using System.Globalization;
using System.Windows;
using SShot.App.Resources;
using SShot.Core.Annotation;

namespace SShot.App.Views;

public partial class CropCoordinatesWindow : Window
{
    private readonly int _imageWidth;
    private readonly int _imageHeight;

    public Int32Rect? ResultRect { get; private set; }

    public CropCoordinatesWindow(int imageWidth, int imageHeight)
    {
        InitializeComponent();
        _imageWidth = imageWidth;
        _imageHeight = imageHeight;

        XTextBox.Text = "0";
        YTextBox.Text = "0";
        WidthTextBox.Text = imageWidth.ToString(CultureInfo.InvariantCulture);
        HeightTextBox.Text = imageHeight.ToString(CultureInfo.InvariantCulture);
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(XTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) ||
            !int.TryParse(YTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int y) ||
            !int.TryParse(WidthTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int width) ||
            !int.TryParse(HeightTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int height))
        {
            ShowError(Strings.CropInvalidRangeMessage);
            return;
        }

        var clamped = CropGeometry.Clamp(x, y, width, height, _imageWidth, _imageHeight);
        if (clamped is null)
        {
            ShowError(Strings.CropInvalidRangeMessage);
            return;
        }

        ResultRect = clamped;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}
