using System.Windows;
using SShot.Core.Capture.Dpi;

namespace SShot.Core.Tests.Capture.Dpi;

public class DpiHelperTests
{
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void PhysicalToDip_ThenDipToPhysical_RoundTrips(double scale)
    {
        var original = new Int32Rect(100, 200, 640, 480);

        var dip = DpiHelper.PhysicalToDip(original, scale);
        var roundTripped = DpiHelper.DipToPhysical(dip, scale);

        Assert.Equal(original, roundTripped);
    }

    [Fact]
    public void PhysicalToDip_At150Percent_ScalesDown()
    {
        var physical = new Int32Rect(0, 0, 300, 150);

        var dip = DpiHelper.PhysicalToDip(physical, 1.5);

        Assert.Equal(200, dip.Width);
        Assert.Equal(100, dip.Height);
    }

    [Fact]
    public void DipToPhysical_At200Percent_ScalesUp()
    {
        var dipRect = new Rect(10, 20, 100, 50);

        var physical = DpiHelper.DipToPhysical(dipRect, 2.0);

        Assert.Equal(new Int32Rect(20, 40, 200, 100), physical);
    }
}
