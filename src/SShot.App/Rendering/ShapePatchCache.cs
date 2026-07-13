using System.Windows;
using System.Windows.Media.Imaging;

namespace SShot.App.Rendering;

/// <summary>
/// Per-EditorWindow memoization of baked mosaic/blur patches (owned by EditorWindow, cleared
/// implicitly when the window closes). RedrawShapes() rebuilds every shape's visual on every
/// mouse-move tick during a drag/resize gesture and on every undo/redo, which without this would
/// re-run the SkiaSharp convolution (crop, downscale, upscale) for every mosaic/blur shape in
/// the document - not just the one actually being dragged - on every single tick.
/// </summary>
internal sealed class ShapePatchCache
{
    private readonly Dictionary<Guid, (Int32Rect Region, double Param, BitmapSource Patch)> _cache = [];

    public BitmapSource GetOrCreate(Guid shapeId, Int32Rect region, double param, Func<BitmapSource> render)
    {
        if (_cache.TryGetValue(shapeId, out var cached) && cached.Region == region && cached.Param == param)
        {
            return cached.Patch;
        }

        var patch = render();
        _cache[shapeId] = (region, param, patch);
        return patch;
    }

    /// <summary>Drops entries for shapes no longer in the document, so deleted/undone shapes
    /// don't keep their baked patch alive for the rest of the editing session.</summary>
    public void PruneExcept(IEnumerable<Guid> liveShapeIds)
    {
        var live = new HashSet<Guid>(liveShapeIds);
        foreach (var staleId in _cache.Keys.Where(id => !live.Contains(id)).ToList())
        {
            _cache.Remove(staleId);
        }
    }
}
