using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SShot.Core.Capture;

namespace SShot.Core.Tests.Capture;

public class FrameStitcherTests
{
    private const int Width = 64;

    /// <summary>Each row gets a distinct, deterministic solid color so overlap/order can be
    /// verified exactly by sampling pixels, with no anti-aliasing ambiguity.</summary>
    private static WriteableBitmap CreateStripedMaster(int height)
    {
        var bitmap = new WriteableBitmap(Width, height, 96, 96, PixelFormats.Bgra32, null);
        int stride = Width * 4;
        var buffer = new byte[stride * height];

        for (int row = 0; row < height; row++)
        {
            byte r = (byte)(row % 256);
            byte g = (byte)((row / 256) % 256);
            const byte b = 128;
            for (int x = 0; x < Width; x++)
            {
                int offset = (row * stride) + (x * 4);
                buffer[offset] = b;
                buffer[offset + 1] = g;
                buffer[offset + 2] = r;
                buffer[offset + 3] = 255;
            }
        }

        bitmap.WritePixels(new Int32Rect(0, 0, Width, height), buffer, stride, 0);
        return bitmap;
    }

    private static BitmapSource Crop(BitmapSource master, int startRow, int height)
    {
        var cropped = new CroppedBitmap(master, new Int32Rect(0, startRow, Width, height));
        cropped.Freeze();
        return cropped;
    }

    private static Color GetPixelColor(BitmapSource bitmap, int x, int y)
    {
        var cropped = new CroppedBitmap(bitmap, new Int32Rect(x, y, 1, 1));
        var converted = new FormatConvertedBitmap(cropped, PixelFormats.Bgra32, null, 0);
        var buffer = new byte[4];
        converted.CopyPixels(buffer, 4, 0);
        return Color.FromRgb(buffer[2], buffer[1], buffer[0]);
    }

    [Fact]
    public void FindOverlap_DetectsKnownVerticalOffset()
    {
        var master = CreateStripedMaster(300);
        var previousFrame = Crop(master, 0, 100);
        var nextFrame = Crop(master, 40, 100); // scrolled down by 40 -> 60 rows overlap

        var (previousPixels, stride) = ToBgra32(previousFrame);
        var (nextPixels, _) = ToBgra32(nextFrame);

        int overlap = FrameStitcher.FindOverlap(previousPixels, nextPixels, Width, 100, stride);

        Assert.Equal(60, overlap);
    }

    [Fact]
    public void HasNewContent_TrueWhenFrameScrolled_FalseWhenIdentical()
    {
        var master = CreateStripedMaster(300);
        var frameA = Crop(master, 0, 100);
        var frameB = Crop(master, 40, 100);
        var frameBAgain = Crop(master, 40, 100);

        Assert.True(FrameStitcher.HasNewContent(frameA, frameB));
        Assert.False(FrameStitcher.HasNewContent(frameB, frameBAgain));
    }

    [Fact]
    public void Stitch_ReconstructsFullHeightImage_WithCorrectContentAtEachRow()
    {
        var master = CreateStripedMaster(300);
        var frames = new List<BitmapSource>
        {
            Crop(master, 0, 100),
            Crop(master, 40, 100), // 60 rows overlap -> 40 new rows
            Crop(master, 80, 100), // 60 rows overlap -> 40 new rows
        };

        var stitched = FrameStitcher.Stitch(frames);

        Assert.Equal(Width, stitched.PixelWidth);
        Assert.Equal(180, stitched.PixelHeight); // 100 + 40 + 40

        // Spot-check that stitched content matches the master at several points.
        Assert.Equal(GetPixelColor(master, 0, 0), GetPixelColor(stitched, 0, 0));
        Assert.Equal(GetPixelColor(master, 0, 99), GetPixelColor(stitched, 0, 99));
        Assert.Equal(GetPixelColor(master, 0, 150), GetPixelColor(stitched, 0, 150));
        Assert.Equal(GetPixelColor(master, 0, 179), GetPixelColor(stitched, 0, 179));
    }

    [Fact]
    public void Stitch_SingleFrame_ReturnsItUnchanged()
    {
        var master = CreateStripedMaster(100);
        var frame = Crop(master, 0, 100);

        var stitched = FrameStitcher.Stitch([frame]);

        Assert.Same(frame, stitched);
    }

    [Fact]
    public void FindOverlap_NoSimilarity_ReturnsZero()
    {
        var previousMaster = CreateStripedMaster(100);
        // A completely different content pattern (inverted channel roles) should not match well.
        var unrelated = new WriteableBitmap(Width, 100, 96, 96, PixelFormats.Bgra32, null);
        var stride = Width * 4;
        var buffer = new byte[stride * 100];
        for (int i = 0; i < buffer.Length; i += 4)
        {
            buffer[i] = 10;
            buffer[i + 1] = 200;
            buffer[i + 2] = 30;
            buffer[i + 3] = 255;
        }

        unrelated.WritePixels(new Int32Rect(0, 0, Width, 100), buffer, stride, 0);

        var (previousPixels, previousStride) = ToBgra32(previousMaster);
        var (nextPixels, _) = ToBgra32(unrelated);

        int overlap = FrameStitcher.FindOverlap(previousPixels, nextPixels, Width, 100, previousStride);

        Assert.Equal(0, overlap);
    }

    private static (byte[] Pixels, int Stride) ToBgra32(BitmapSource source)
    {
        var converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = converted.PixelWidth * 4;
        var buffer = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(buffer, stride, 0);
        return (buffer, stride);
    }
}
