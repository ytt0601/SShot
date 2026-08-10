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
    /// picking the one with the lowest mean per-channel difference (stopping early only once a
    /// candidate scores a perfect 0.0 - the mathematical floor for a mean of absolute differences,
    /// so no later candidate could ever score lower; anything above 0.0, however small, can still
    /// be beaten by the true overlap and must not short-circuit the search). BitBlt frames are
    /// pixel-exact, so a real match after a genuine scroll typically does score exactly 0.0,
    /// making this a real (if narrower) speedup in the common case. Returns 0 if nothing scores
    /// at or below <paramref name="maxMeanDifference"/> (treat as "no reliable overlap found").
    /// Operates on raw BGRA32 buffers (rather than BitmapSource) so it's cheaply unit-testable
    /// with synthetic pixel data of known offsets.
    /// </summary>
    public static int FindOverlap(
        byte[] previousPixels, byte[] nextPixels, int width, int height, int previousStride, int nextStride,
        int minOverlap = 8, double maxMeanDifference = 12.0)
    {
        int bestOverlap = 0;
        double bestScore = double.MaxValue;

        for (int overlap = height; overlap >= minOverlap; overlap--)
        {
            double score = SampledMeanAbsoluteDifference(previousPixels, nextPixels, width, height, previousStride, nextStride, overlap);
            if (score < bestScore)
            {
                bestScore = score;
                bestOverlap = overlap;

                if (score == 0)
                {
                    break;
                }
            }
        }

        return bestScore <= maxMeanDifference ? bestOverlap : 0;
    }

    private static double SampledMeanAbsoluteDifference(
        byte[] previousPixels, byte[] nextPixels, int width, int height, int previousStride, int nextStride, int overlap)
    {
        long sum = 0;
        long count = 0;
        int previousStartRow = height - overlap;
        int rowStep = Math.Max(1, overlap / SampleRows);

        for (int row = 0; row < overlap; row += rowStep)
        {
            int previousRowOffset = (previousStartRow + row) * previousStride;
            int nextRowOffset = row * nextStride;

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

    /// <summary>
    /// Stitches with each frame-pair's overlap already known (as computed live by
    /// <see cref="ScrollingCaptureService"/> during capture), so no frame needs its pixels
    /// re-extracted or its overlap re-searched here. <paramref name="overlaps"/>[i] is the
    /// overlap between frames[i] and frames[i+1].
    /// </summary>
    public static BitmapSource Stitch(IReadOnlyList<BitmapSource> frames, IReadOnlyList<int> overlaps)
    {
        if (frames.Count == 0)
        {
            throw new ArgumentException("At least one frame is required.", nameof(frames));
        }

        if (frames.Count == 1)
        {
            return frames[0];
        }

        if (overlaps.Count != frames.Count - 1)
        {
            throw new ArgumentException("One overlap per consecutive frame pair is required.", nameof(overlaps));
        }

        // The captured window's width can legitimately change between scroll-capture ticks -
        // sizing the destination to the narrowest frame guarantees every frame's rows fit,
        // since CopyRows additionally clamps its own write width to this.
        int width = frames.Min(f => f.PixelWidth);

        var segments = new List<(BitmapSource Frame, int StartRow, int RowCount)>
        {
            (frames[0], 0, frames[0].PixelHeight),
        };

        for (int i = 1; i < frames.Count; i++)
        {
            var next = frames[i];
            int overlap = overlaps[i - 1];
            int newRowCount = next.PixelHeight - overlap;

            if (newRowCount > 0)
            {
                segments.Add((next, overlap, newRowCount));
            }
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

        // Clamp to the destination's width, not just the source's: a later frame can be wider than
        // the narrowest frame the destination bitmap was sized for (see Stitch's width = Min(...)),
        // and WritePixels throws if the write rect exceeds the destination bounds.
        int copyWidth = Math.Min(converted.PixelWidth, destination.PixelWidth);
        int stride = converted.PixelWidth * 4;
        var buffer = new byte[stride * rowCount];
        converted.CopyPixels(new Int32Rect(0, startRow, converted.PixelWidth, rowCount), buffer, stride, 0);
        destination.WritePixels(new Int32Rect(0, destY, copyWidth, rowCount), buffer, stride, 0);
    }

    internal static (byte[] Pixels, int Stride) ToBgra32Pixels(BitmapSource source)
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
