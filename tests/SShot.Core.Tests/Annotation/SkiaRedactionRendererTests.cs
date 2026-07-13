using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SShot.Core.Annotation.Rendering;

namespace SShot.Core.Tests.Annotation;

public class SkiaRedactionRendererTests
{
    private static BitmapSource CreateCheckerboard(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = (y * width + x) * 4;
                bool on = ((x / 4) + (y / 4)) % 2 == 0;
                byte v = (byte)(on ? 255 : 0);
                pixels[i + 0] = v; // B
                pixels[i + 1] = v; // G
                pixels[i + 2] = v; // R
                pixels[i + 3] = 255; // A
            }
        }

        var bmp = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bmp.Freeze();
        return bmp;
    }

    private static byte[] ReadPixels(BitmapSource source)
    {
        int stride = source.PixelWidth * 4;
        var pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    [Fact]
    public void Mosaic_Apply_OnOffsetSubRegion_ProducesNonEmptyPixelatedPatch()
    {
        var source = CreateCheckerboard(200, 150);
        var region = new Int32Rect(60, 40, 80, 60); // offset, non-zero region well inside the image

        var result = SkiaMosaicRenderer.Apply(source, region, 8);

        Assert.Equal(region.Width, result.PixelWidth);
        Assert.Equal(region.Height, result.PixelHeight);

        var pixels = ReadPixels(result);
        // The patch must actually contain visible (non-transparent, non-uniform-black) content.
        Assert.Contains(pixels, b => b != 0);
    }

    [Fact]
    public void Mosaic_Apply_FlattensEachBlockToUniformColor()
    {
        var source = CreateCheckerboard(200, 150);
        var region = new Int32Rect(60, 40, 64, 64);
        int blockSize = 16;

        var result = SkiaMosaicRenderer.Apply(source, region, blockSize);
        int stride = result.PixelWidth * 4;
        var pixels = new byte[stride * result.PixelHeight];
        result.CopyPixels(pixels, stride, 0);

        // Within a single blockSize x blockSize cell, all pixels should be identical (flat block).
        int cellX = 0, cellY = 0;
        byte refB = pixels[(cellY * result.PixelWidth + cellX) * 4];
        for (int y = cellY; y < cellY + blockSize && y < result.PixelHeight; y++)
        {
            for (int x = cellX; x < cellX + blockSize && x < result.PixelWidth; x++)
            {
                int i = (y * result.PixelWidth + x) * 4;
                Assert.Equal(refB, pixels[i]);
            }
        }
    }

    [Fact]
    public void Blur_Apply_OnOffsetSubRegion_ProducesNonEmptyPatch()
    {
        var source = CreateCheckerboard(200, 150);
        var region = new Int32Rect(60, 40, 80, 60);

        var result = SkiaBlurRenderer.Apply(source, region, 10);

        Assert.Equal(region.Width, result.PixelWidth);
        Assert.Equal(region.Height, result.PixelHeight);

        var pixels = ReadPixels(result);
        Assert.Contains(pixels, b => b != 0);
    }

    [Fact]
    public void Blur_Apply_NearImageEdge_DoesNotThrowAndProducesContent()
    {
        var source = CreateCheckerboard(100, 100);
        var region = new Int32Rect(80, 80, 20, 20); // touches the bottom-right edge

        var result = SkiaBlurRenderer.Apply(source, region, 10);

        Assert.Equal(region.Width, result.PixelWidth);
        Assert.Equal(region.Height, result.PixelHeight);
        var pixels = ReadPixels(result);
        Assert.Contains(pixels, b => b != 0);
    }
}
