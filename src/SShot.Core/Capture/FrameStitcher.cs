using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SShot.Core.Capture;

/// <summary>
/// Detects vertical overlap between consecutive scroll-capture frames (by pixel comparison,
/// since the actual scroll-to-pixel ratio varies per app/OS setting and can't be assumed) and
/// stitches the non-overlapping rows into one tall image. Known limitations: sticky
/// headers/footers get duplicated in every frame unless excluded, animated/parallax content
/// breaks pixel matching - see CLAUDE.md.
/// </summary>
public static class FrameStitcher
{
    private const int SampleRows = 24;
    private const int ColumnStep = 8;

    /// <summary>
    /// Searches candidate overlaps from the full compared height down to <paramref name="minOverlap"/>,
    /// picking the one with the lowest mean per-channel difference. Returns 0 if nothing scores
    /// at or below <paramref name="maxMeanDifference"/> (treat as "no reliable overlap found").
    /// Operates on raw BGRA32 buffers (rather than BitmapSource) so it's cheaply unit-testable
    /// with synthetic pixel data of known offsets.
    /// </summary>
    public static int FindOverlap(
        byte[] previousPixels, byte[] nextPixels, int width, int height, int stride,
        int minOverlap = 8, double maxMeanDifference = 12.0)
    {
        int bestOverlap = 0;
        double bestScore = double.MaxValue;

        for (int overlap = height; overlap >= minOverlap; overlap--)
        {
            double score = SampledMeanAbsoluteDifference(previousPixels, nextPixels, width, height, stride, overlap);
            if (score < bestScore)
            {
                bestScore = score;
                bestOverlap = overlap;
            }
        }

        return bestScore <= maxMeanDifference ? bestOverlap : 0;
    }

    private static double SampledMeanAbsoluteDifference(
        byte[] previousPixels, byte[] nextPixels, int width, int height, int stride, int overlap)
    {
        long sum = 0;
        long count = 0;
        int previousStartRow = height - overlap;
        int rowStep = Math.Max(1, overlap / SampleRows);

        for (int row = 0; row < overlap; row += rowStep)
        {
            int previousRowOffset = (previousStartRow + row) * stride;
            int nextRowOffset = row * stride;

            for (int x = 0; x < width; x += ColumnStep)
            {
                int byteOffset = x * 4;
                for (int channel = 0; channel < 3; channel++) // compare B,G,R; skip alpha
                {
                    sum += Math.Abs(previousPixels[previousRowOffset + byteOffset + channel] - nextPixels[nextRowOffset + byteOffset + channel]);
                    count++;
                }
            }
        }

        return count == 0 ? double.MaxValue : (double)sum / count;
    }

    /// <summary>True if the new frame has at least one row of content not already visible at
    /// the bottom of the previous frame - false means scrolling had no effect (likely the end
    /// of the scrollable content was reached).</summary>
    public static bool HasNewContent(BitmapSource previous, BitmapSource next)
    {
        var (previousPixels, stride) = ToBgra32Pixels(previous);
        var (nextPixels, _) = ToBgra32Pixels(next);
        int height = Math.Min(previous.PixelHeight, next.PixelHeight);
        int overlap = FindOverlap(previousPixels, nextPixels, previous.PixelWidth, height, stride);
        return overlap < height;
    }

    public static BitmapSource Stitch(IReadOnlyList<BitmapSource> frames)
    {
        if (frames.Count == 0)
        {
            throw new ArgumentException("At least one frame is required.", nameof(frames));
        }

        int width = frames[0].PixelWidth;
        if (frames.Count == 1)
        {
            return frames[0];
        }

        var segments = new List<(BitmapSource Frame, int StartRow, int RowCount)>
        {
            (frames[0], 0, frames[0].PixelHeight),
        };

        var (previousPixels, previousStride) = ToBgra32Pixels(frames[0]);
        int previousHeight = frames[0].PixelHeight;

        for (int i = 1; i < frames.Count; i++)
        {
            var next = frames[i];
            var (nextPixels, nextStride) = ToBgra32Pixels(next);
            int compareHeight = Math.Min(previousHeight, next.PixelHeight);
            int overlap = FindOverlap(previousPixels, nextPixels, width, compareHeight, nextStride);
            int newRowCount = next.PixelHeight - overlap;

            if (newRowCount > 0)
            {
                segments.Add((next, overlap, newRowCount));
            }

            previousPixels = nextPixels;
            previousStride = nextStride;
            previousHeight = next.PixelHeight;
        }

        int totalHeight = segments.Sum(s => s.RowCount);
        var result = new WriteableBitmap(width, totalHeight, 96, 96, PixelFormats.Bgra32, null);

        int destY = 0;
        foreach (var (frame, startRow, rowCount) in segments)
        {
            CopyRows(frame, startRow, rowCount, result, destY);
            destY += rowCount;
        }

        result.Freeze();
        return result;
    }

    private static void CopyRows(BitmapSource source, int startRow, int rowCount, WriteableBitmap destination, int destY)
    {
        var converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        int stride = converted.PixelWidth * 4;
        var buffer = new byte[stride * rowCount];
        converted.CopyPixels(new Int32Rect(0, startRow, converted.PixelWidth, rowCount), buffer, stride, 0);
        destination.WritePixels(new Int32Rect(0, destY, converted.PixelWidth, rowCount), buffer, stride, 0);
    }

    private static (byte[] Pixels, int Stride) ToBgra32Pixels(BitmapSource source)
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
