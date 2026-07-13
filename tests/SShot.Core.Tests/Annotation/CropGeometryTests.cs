using System.Windows;
using SShot.Core.Annotation;

namespace SShot.Core.Tests.Annotation;

public class CropGeometryTests
{
    [Fact]
    public void Clamp_WithinBounds_ReturnsRectUnchanged()
    {
        var result = CropGeometry.Clamp(10, 20, 30, 40, imageWidth: 200, imageHeight: 200);
        Assert.Equal(new Int32Rect(10, 20, 30, 40), result);
    }

    [Fact]
    public void Clamp_NegativeOrigin_ClampsToZeroAndShrinksSizeByClippedAmount()
    {
        // Origin clamps to 0, but width/height must shrink by the amount clipped off the
        // left/top (5px each here) - otherwise the clamped rect would bulge 5px past the
        // originally intended right/bottom edge.
        var result = CropGeometry.Clamp(-5, -5, 30, 40, imageWidth: 200, imageHeight: 200);
        Assert.Equal(new Int32Rect(0, 0, 25, 35), result);
    }

    [Fact]
    public void Clamp_SizeExceedsImage_ClampsToRemainingArea()
    {
        var result = CropGeometry.Clamp(150, 150, 100, 100, imageWidth: 200, imageHeight: 200);
        Assert.Equal(new Int32Rect(150, 150, 50, 50), result);
    }

    [Fact]
    public void Clamp_OriginAtOrBeyondImageEdge_ReturnsNull()
    {
        Assert.Null(CropGeometry.Clamp(200, 0, 50, 50, imageWidth: 200, imageHeight: 200));
        Assert.Null(CropGeometry.Clamp(0, 500, 50, 50, imageWidth: 200, imageHeight: 200));
    }

    [Fact]
    public void Clamp_ZeroOrNegativeSize_ReturnsNull()
    {
        Assert.Null(CropGeometry.Clamp(0, 0, 0, 40, imageWidth: 200, imageHeight: 200));
        Assert.Null(CropGeometry.Clamp(0, 0, 30, -10, imageWidth: 200, imageHeight: 200));
    }
}
