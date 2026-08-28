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

    /// <summary>Builds a frame whose top and bottom rows are identical, non-scrolling "chrome"
    /// (a window's tab strip / toolbar / status bar) while the rows between them scroll.</summary>
    private static byte[] CreateFrameWithChrome(int contentStartRow, int height, int chromeTop, int chromeBottom)
    {
        int stride = Width * 4;
        var buffer = new byte[stride * height];

        for (int row = 0; row < height; row++)
        {
            bool isChrome = row < chromeTop || row >= height - chromeBottom;
            byte r = isChrome ? (byte)255 : (byte)((contentStartRow + row) % 256);
            byte g = isChrome ? (byte)255 : (byte)0;
            byte b = isChrome ? (byte)255 : (byte)128;

            for (int x = 0; x < Width; x++)
            {
                int offset = (row * stride) + (x * 4);
                buffer[offset] = b;
                buffer[offset + 1] = g;
                buffer[offset + 2] = r;
                buffer[offset + 3] = 255;
            }
        }

        return buffer;
    }

    [Fact]
    public void FindOverlap_WithNonScrollingChrome_OnlyFindsTheTrueOverlapWhenMarginsAreExcluded()
    {
        // Reproduces what a real window capture looks like: the frame spans the whole window, so
        // its top and bottom carry chrome that stays put while the middle scrolls. Comparing whole
        // frames, the chrome only lines up on the "nothing scrolled" hypothesis, which is exactly
        // the wrong answer - the caller reads a full-height overlap as "reached the end".
        const int height = 100;
        const int scroll = 40;
        const int chromeTop = 12;
        const int chromeBottom = 8;
        int stride = Width * 4;

        byte[] previous = CreateFrameWithChrome(0, height, chromeTop, chromeBottom);
        byte[] next = CreateFrameWithChrome(scroll, height, chromeTop, chromeBottom);

        int wholeFrame = FrameStitcher.FindOverlap(previous, next, Width, height, stride, stride);
        int marginsExcluded = FrameStitcher.FindOverlap(
            previous, next, Width, height, stride, stride,
            ignoreTopRows: chromeTop, ignoreBottomRows: chromeBottom);

        Assert.NotEqual(height - scroll, wholeFrame);
        Assert.Equal(height - scroll, marginsExcluded);
    }

    [Fact]
    public void FindOverlap_MarginsWiderThanTheBand_DoesNotScoreAnEmptyMatch()
    {
        // An empty compared band must not be treated as a perfect match, or every candidate
        // narrower than the margins would beat the real one.
        const int height = 40;
        int stride = Width * 4;
        byte[] previous = CreateFrameWithChrome(0, height, 0, 0);
        byte[] next = CreateFrameWithChrome(10, height, 0, 0);

        int overlap = FrameStitcher.FindOverlap(
            previous, next, Width, height, stride, stride,
            minOverlap: 1, ignoreTopRows: 25, ignoreBottomRows: 25);

        Assert.Equal(0, overlap);
    }

    [Fact]
    public void FindOverlap_DetectsKnownVerticalOffset()
    {
        var master = CreateStripedMaster(300);
        var previousFrame = Crop(master, 0, 100);
        var nextFrame = Crop(master, 40, 100); // scrolled down by 40 -> 60 rows overlap

        var (previousPixels, previousStride) = ToBgra32(previousFrame);
        var (nextPixels, nextStride) = ToBgra32(nextFrame);

        int overlap = FrameStitcher.FindOverlap(previousPixels, nextPixels, Width, 100, previousStride, nextStride);

        Assert.Equal(60, overlap);
    }

    [Fact]
    public void FindOverlap_ReturnsFullHeightForIdenticalFrames_LessForScrolledFrames()
    {
        // ScrollingCaptureService now derives "has new content" from FindOverlap directly
        // (overlap < compareHeight) instead of the removed FrameStitcher.HasNewContent helper.
        var master = CreateStripedMaster(300);
        var frameA = Crop(master, 0, 100);
        var frameB = Crop(master, 40, 100);
        var frameBAgain = Crop(master, 40, 100);

        var (aPixels, aStride) = ToBgra32(frameA);
        var (bPixels, bStride) = ToBgra32(frameB);
        var (bAgainPixels, bAgainStride) = ToBgra32(frameBAgain);

        int scrolledOverlap = FrameStitcher.FindOverlap(aPixels, bPixels, Width, 100, aStride, bStride);
        int identicalOverlap = FrameStitcher.FindOverlap(bPixels, bAgainPixels, Width, 100, bStride, bAgainStride);

        Assert.True(scrolledOverlap < 100);
        Assert.Equal(100, identicalOverlap);
    }

    [Fact]
    public void FindOverlap_DoesNotStopAtNearZeroFalsePositive_ContinuesToExactMatch()
    {
        // Regression test for the early-exit optimization in FindOverlap: a large overlap
        // (little/no scroll) that scores small-but-nonzero due to a single differing pixel must
        // not stop the search before a smaller overlap that scores an exact 0.0 match is reached
        // - only an exact 0.0 score is a safe stopping point, since no score (a mean of absolute
        // differences) can ever go lower than that floor.
        const int width = 1;
        const int height = 10;
        int stride = width * 4;

        byte[] previous = new byte[stride * height];
        byte[] next = new byte[stride * height];

        for (int row = 0; row < height; row++)
        {
            previous[row * stride] = 100; // uniform content in both frames
            next[row * stride] = 100;
        }

        // A single-pixel difference in the bottom row only affects the "almost no scroll"
        // (overlap = height) hypothesis - it used to score just low enough to trigger an
        // immediate early exit there under the old fixed-threshold break, before the search
        // ever reached the smaller overlaps below (all of which exclude this row and are an
        // exact 0.0 match).
        next[(height - 1) * stride] = 101;

        int overlap = FrameStitcher.FindOverlap(previous, next, width, height, stride, stride, minOverlap: 1);

        Assert.Equal(height - 1, overlap);
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

        // Same overlaps ScrollingCaptureService would have computed live during capture.
        var stitched = FrameStitcher.Stitch(frames, [60, 60]);

        Assert.Equal(Width, stitched.PixelWidth);
        Assert.Equal(180, stitched.PixelHeight); // 100 + 40 + 40

        // Spot-check that stitched content matches the master at several points.
        Assert.Equal(GetPixelColor(master, 0, 0), GetPixelColor(stitched, 0, 0));
        Assert.Equal(GetPixelColor(master, 0, 99), GetPixelColor(stitched, 0, 99));
        Assert.Equal(GetPixelColor(master, 0, 150), GetPixelColor(stitched, 0, 150));
        Assert.Equal(GetPixelColor(master, 0, 179), GetPixelColor(stitched, 0, 179));
    }

    /// <summary>Crops the master like a scrolled window would show it, then paints the top and
    /// bottom rows over with chrome that stays put no matter how far the content scrolled.</summary>
    private static BitmapSource CreateWindowFrame(BitmapSource master, int startRow, int height, int chromeTop, int chromeBottom)
    {
        int stride = Width * 4;
        var buffer = new byte[stride * height];
        master.CopyPixels(new Int32Rect(0, startRow, Width, height), buffer, stride, 0);

        for (int row = 0; row < height; row++)
        {
            if (row >= chromeTop && row < height - chromeBottom)
            {
                continue;
            }

            for (int x = 0; x < Width; x++)
            {
                int offset = (row * stride) + (x * 4);
                buffer[offset] = 255;
                buffer[offset + 1] = 255;
                buffer[offset + 2] = 255;
                buffer[offset + 3] = 255;
            }
        }

        var bitmap = new WriteableBitmap(Width, height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, Width, height), buffer, stride, 0);
        bitmap.Freeze();
        return bitmap;
    }

    [Fact]
    public void Stitch_WithChromeRows_DropsThemInsteadOfSplicingThemIntoTheContent()
    {
        // Each frame's trailing rows are its bottom chrome, so appending them verbatim puts a strip
        // of chrome at every seam and pushes out exactly as many rows of content - content the
        // capture did see, which then never appears in the result.
        const int frameHeight = 100;
        const int shift = 30;
        const int chromeTop = 10;
        const int chromeBottom = 8;
        int overlap = frameHeight - shift;

        var master = CreateStripedMaster(300);
        List<BitmapSource> frames =
        [
            CreateWindowFrame(master, 0, frameHeight, chromeTop, chromeBottom),
            CreateWindowFrame(master, shift, frameHeight, chromeTop, chromeBottom),
            CreateWindowFrame(master, shift * 2, frameHeight, chromeTop, chromeBottom),
        ];

        var stitched = FrameStitcher.Stitch(frames, [overlap, overlap], chromeTop, chromeBottom);

        // First frame's content band (rows 0..91, its top chrome kept) plus one shift per frame.
        Assert.Equal(frameHeight - chromeBottom + (shift * 2), stitched.PixelHeight);

        // The rows either side of the first seam must run straight on from the master, with no
        // chrome spliced in and no master row skipped.
        Assert.Equal(GetPixelColor(master, 0, 10), GetPixelColor(stitched, 0, 10));
        Assert.Equal(GetPixelColor(master, 0, 91), GetPixelColor(stitched, 0, 91));
        Assert.Equal(GetPixelColor(master, 0, 92), GetPixelColor(stitched, 0, 92));
        Assert.Equal(GetPixelColor(master, 0, 121), GetPixelColor(stitched, 0, 121));
        Assert.Equal(GetPixelColor(master, 0, 122), GetPixelColor(stitched, 0, 122));
        Assert.Equal(GetPixelColor(master, 0, 151), GetPixelColor(stitched, 0, 151));
    }

    [Fact]
    public void Stitch_WithWrongOverlapCount_Throws()
    {
        var master = CreateStripedMaster(300);
        var frames = new List<BitmapSource> { Crop(master, 0, 100), Crop(master, 40, 100) };

        Assert.Throws<ArgumentException>(() => FrameStitcher.Stitch(frames, [60, 60]));
    }

    [Fact]
    public void Stitch_SingleFrame_ReturnsItUnchanged()
    {
        var master = CreateStripedMaster(100);
        var frame = Crop(master, 0, 100);

        var stitched = FrameStitcher.Stitch([frame], []);

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
        var (nextPixels, nextStride) = ToBgra32(unrelated);

        int overlap = FrameStitcher.FindOverlap(previousPixels, nextPixels, Width, 100, previousStride, nextStride);

        Assert.Equal(0, overlap);
    }

    [Fact]
    public void Stitch_FrameWidensPartway_DoesNotThrowAndStaysWithinNarrowestWidth()
    {
        // Regression test: Stitch used to size the destination bitmap to frames[0].PixelWidth and
        // let CopyRows write each frame's own (unclamped) width into it. If a later frame is wider
        // than frames[0] - the captured window resized wider mid scroll-capture - WritePixels'
        // source rect would exceed the destination bounds and throw. The destination must be sized
        // to (and every frame's copy clamped to) the narrowest frame instead.
        var master = CreateStripedMaster(300);
        var wider = new WriteableBitmap(Width + 16, 100, 96, 96, PixelFormats.Bgra32, null);
        var frames = new List<BitmapSource>
        {
            Crop(master, 0, 100),
            wider, // wider than frames[0] - simulates the captured window resizing wider mid-capture
        };

        // `wider`'s unrelated (blank) content has no reliable overlap with the master, matching
        // what ScrollingCaptureService's live FindOverlap call would have computed for this pair.
        var stitched = FrameStitcher.Stitch(frames, [0]);

        Assert.Equal(Width, stitched.PixelWidth);
    }

    [Fact]
    public void FindOverlap_DifferingFrameWidths_DoesNotThrowAndStaysWithinBounds()
    {
        // Regression test: FindOverlap used to take one shared stride for both buffers, which
        // misaligns rows (or reads past a narrower buffer's bounds) when the captured window's
        // width changes between scroll-capture ticks. Passing each buffer's own stride, with the
        // compare width clamped to the narrower frame (as ScrollingCaptureService/Stitch now do),
        // must stay in-bounds regardless.
        var wide = new WriteableBitmap(64, 100, 96, 96, PixelFormats.Bgra32, null);
        var narrow = new WriteableBitmap(48, 100, 96, 96, PixelFormats.Bgra32, null);

        var (previousPixels, previousStride) = ToBgra32(wide);
        var (nextPixels, nextStride) = ToBgra32(narrow);

        int overlap = FrameStitcher.FindOverlap(previousPixels, nextPixels, 48, 100, previousStride, nextStride);

        Assert.InRange(overlap, 0, 100);
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
