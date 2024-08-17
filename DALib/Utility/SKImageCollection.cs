using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SkiaSharp;
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace DALib.Utility;

/// <summary>
///     Represents a disposable collection of SKImages.
/// </summary>
public class SKImageCollection(IEnumerable<SKImage> images) : Collection<SKImage>(
                                                                  images.Where(frame => frame is not null)
                                                                        .ToList()),
                                                              IDisposable
{
    /// <inheritdoc />
    public virtual void Dispose()
    {
        foreach (var image in Items)
            image.Dispose();

        GC.SuppressFinalize(this);
    }
}