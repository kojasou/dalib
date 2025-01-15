using System;

namespace DALib.Utility;

/// <summary>
///     A collection of <see cref="SKImageCache{TKey}" /> caches that can be used as a shared cache for rendering multiple
///     maps.
/// </summary>
public sealed class MapImageCache : IDisposable
{
    /// <summary>
    ///     The background cache
    /// </summary>
    public SKImageCache<int> BackgroundCache { get; }

    /// <summary>
    ///     The left foreground cache
    /// </summary>
    public SKImageCache<int> LeftForegroundCache { get; }

    /// <summary>
    ///     The right foreground cache
    /// </summary>
    public SKImageCache<int> RightForegroundCache { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MapImageCache" /> class.
    /// </summary>
    public MapImageCache()
    {
        BackgroundCache = new SKImageCache<int>();
        LeftForegroundCache = new SKImageCache<int>();
        RightForegroundCache = new SKImageCache<int>();
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MapImageCache" /> class using the specified caches.
    /// </summary>
    /// <param name="bgCache">
    ///     The background cache
    /// </param>
    /// <param name="lfgCache">
    ///     The left foreground cache
    /// </param>
    /// <param name="rfgCache">
    ///     The right foreground cache
    /// </param>
    public MapImageCache(SKImageCache<int> bgCache, SKImageCache<int> lfgCache, SKImageCache<int> rfgCache)
    {
        BackgroundCache = bgCache;
        LeftForegroundCache = lfgCache;
        RightForegroundCache = rfgCache;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        BackgroundCache.Dispose();
        LeftForegroundCache.Dispose();
        RightForegroundCache.Dispose();
    }
}