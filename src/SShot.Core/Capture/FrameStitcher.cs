using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SShot.Core.Capture;

/// <summary>
/// Detects vertical overlap between consecutive scroll-capture frames (by pixel comparison,
/// since the actual scroll-to-pixel ratio varies per app/OS setting and can't be assumed) and
/// stitches the non-overlapping rows into one tall image. Known limitations: sticky
/// headers/footers get duplicated in every frame unless excluded, animated/parallax content
/// breaks pixel matching.
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
    /// <param name="ignoreTopRows">Rows to skip at the start of each compared band, and
    /// <paramref name="ignoreBottomRows"/> at its end. A window capture includes chrome that never
    /// scrolls (tab strip, toolbar, status bar); at the true overlap those rows sit against
    /// scrolled content and score as a mismatch, which can push the correct candidate past
    /// <paramref name="maxMeanDifference"/> and make the whole search report "no reliable overlap".
    /// Both default to 0 so the raw geometry stays unchanged for callers that compare whole
    /// frames.</param>
    public static int FindOverlap(
        byte[] previousPixels, byte[] nextPixels, int width, int height, int previousStride, int nextStride,
        int minOverlap = 8, double maxMeanDifference = 12.0, int ignoreTopRows = 0, int ignoreBottomRows = 0)
    {
        int bestOverlap = 0;
        double bestScore = double.MaxValue;

        for (int overlap = height; overlap >= minOverlap; overlap--)
        {
            double score = SampledMeanAbsoluteDifference(
                previousPixels, nextPixels, width, height, previousStride, nextStride, overlap,
                ignoreTopRows, ignoreBottomRows);
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
        byte[] previousPixels, byte[] nextPixels, int width, int height, int previousStride, int nextStride,
        int overlap, int ignoreTopRows, int ignoreBottomRows)
    {
        long sum = 0;
        long count = 0;
        int previousStartRow = height - overlap;

        // Row r of the band reads next[r] and previous[height - overlap + r], so skipping the
        // band's first ignoreTopRows drops the rows where either frame is still showing its top
        // chrome, and skipping its last ignoreBottomRows drops the rows where the previous frame
        // is showing its bottom chrome (the tighter of the two bounds on that side).
        int firstRow = ignoreTopRows;
        int lastRowExclusive = overlap - ignoreBottomRows;
        if (lastRowExclusive <= firstRow)
        {
            // The excluded margins swallow the whole band - too little is left to judge this
            // candidate, so it must not be allowed to win by scoring an empty (perfect) match.
            return double.MaxValue;
        }

        int rowStep = Math.Max(1, (lastRowExclusive - firstRow) / SampleRows);

        for (int row = firstRow; row < lastRowExclusive; row += rowStep)
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
    /// <param name="chromeTopRows">Rows of non-scrolling window chrome at the top of every frame,
    /// and <paramref name="chromeBottomRows"/> at the bottom. Both default to 0, which stitches
    /// the frames verbatim. A single frame is still returned unchanged - a one-frame scroll
    /// capture is just a window shot, chrome included.</param>
    public static BitmapSource Stitch(
        IReadOnlyList<BitmapSource> frames, IReadOnlyList<int> overlaps,
        int chromeTopRows = 0, int chromeBottomRows = 0)
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

        // Every frame carries the window's chrome on the same rows, so appending each frame's
        // trailing rows verbatim splices the bottom chrome into the middle of the result - and
        // displaces exactly as many rows of real content, which are then missing from the output
        // even though the capture did see them. Cut the chrome away instead: the first frame keeps
        // its top chrome so the result still opens like the window it came from, and no frame
        // contributes its bottom chrome.
        var segments = new List<(BitmapSource Frame, int StartRow, int RowCount)>();
        int firstRowCount = frames[0].PixelHeight - chromeBottomRows;
        if (firstRowCount > 0)
        {
            segments.Add((frames[0], 0, firstRowCount));
        }

        for (int i = 1; i < frames.Count; i++)
        {
            var next = frames[i];
            int contentEndRow = next.PixelHeight - chromeBottomRows;

            // The scroll advanced by (height - overlap) rows, so that many rows at the end of this
            // frame's content band are new. Clamping at chromeTopRows covers a scroll longer than
            // the content band, where the whole band is new (and rows in between were never seen).
            int startRow = Math.Max(chromeTopRows, contentEndRow - (next.PixelHeight - overlaps[i - 1]));
            int rowCount = contentEndRow - startRow;

            if (rowCount > 0)
            {
                segments.Add((next, startRow, rowCount));
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
