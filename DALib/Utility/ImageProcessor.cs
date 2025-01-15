using System;
using System.Collections.Generic;
using System.Linq;
using DALib.Definitions;
using DALib.Extensions;
using KGySoft.Drawing.Imaging;
using KGySoft.Drawing.SkiaSharp;
using SkiaSharp;
using Palette = DALib.Drawing.Palette;

namespace DALib.Utility;

/// <summary>
///     Provides methods for processing images
/// </summary>
public static class ImageProcessor
{
    /// <summary>
    ///     Creates a mosaic image by combining multiple images horizontally.
    /// </summary>
    /// <param name="padding">
    ///     The padding between each image in pixels. Default value is 1.
    /// </param>
    /// <param name="images">
    ///     The images to be combined into a mosaic.
    /// </param>
    public static SKImage CreateMosaic(int padding = 1, params SKImage[] images)
    {
        var width = images.Sum(img => img.Width) + (images.Length - 1) * padding;
        var height = images.Max(img => img.Height);

        using var bitmap = new SKBitmap(width, height);

        using (var canvas = new SKCanvas(bitmap))
        {
            var x = 0;

            foreach (var image in images)
            {
                canvas.DrawImage(image, x, 0);

                x += image.Width + padding;
            }
        }

        return SKImage.FromBitmap(bitmap);
    }

    /// <summary>
    ///     Preserves non-transparent black pixels in the given image by converting them to a very dark gray (1, 1, 1)
    /// </summary>
    /// <param name="image">
    ///     The image whose black pixels to preserve
    /// </param>
    public static SKImage PreserveNonTransparentBlacks(SKImage image)
    {
        using var bitmap = SKBitmap.FromImage(image);

        PreserveNonTransparentBlacks(bitmap);

        var ret = SKImage.FromBitmap(bitmap);

        image.Dispose();

        return ret;
    }

    /// <summary>
    ///     Preserves non-transparent black pixels in the given image by converting them to a very dark gray (1, 1, 1)
    /// </summary>
    /// <param name="bitmap">
    ///     The image whose black pixels to preserve
    /// </param>
    public static void PreserveNonTransparentBlacks(SKBitmap bitmap)
    {
        using var canvas = new SKCanvas(bitmap);

        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);

                if (color.IsNearBlack())
                    canvas.DrawPoint(x, y, CONSTANTS.RGB555_ALMOST_BLACK);
            }
    }

    /// <summary>
    ///     Preserves non-transparent black pixels in the given images by converting them to a very dark gray (1, 1, 1)
    /// </summary>
    /// <param name="images">
    ///     The images whose black pixels to preserve
    /// </param>
    public static void PreserveNonTransparentBlacks(IList<SKImage> images)
    {
        for (var i = 0; i < images.Count; i++)
            images[i] = PreserveNonTransparentBlacks(images[i]);
    }

    /// <summary>
    ///     Quantizes the given image using the specified quantizer options.
    /// </summary>
    /// <param name="options">
    ///     The quantizer options.
    /// </param>
    /// <param name="image">
    ///     The image to be quantized.
    /// </param>
    /// <remarks>
    ///     Quantization is the process of reducing the number of colors in an image. This method uses the Wu algorithm
    /// </remarks>
    public static Palettized<SKImage> Quantize(QuantizerOptions options, SKImage image)
    {
        using var bitmap = SKBitmap.FromImage(image);
        IQuantizer quantizer = OptimizedPaletteQuantizer.Wu(options.MaxColors, alphaThreshold: 0);
        var source = bitmap.GetReadableBitmapData();

        using var qSession = quantizer.Initialize(source);
        using var quantizedBitmap = new SKBitmap(image.Info);
        var existingPixels = bitmap.Pixels;

        var colorCount = existingPixels.Distinct()
                                       .Count();

        using var quantizedPixMap = quantizedBitmap.PeekPixels();

        var pixelBuffer = quantizedPixMap.GetPixelSpan<SKColor>();
        pixelBuffer.Fill(CONSTANTS.Transparent);

        //don't quantize/dither if the image already has less than maximum number of colors
        if (colorCount <= options.MaxColors)
        {
            //all colors must be fully opaque or fully transparent
            existingPixels = existingPixels.Select(
                                               pixel =>
                                               {
                                                   //normally the quantizer would set all fully transparent pixels to black
                                                   //but since we didn't quantize the image, we have to do it manually
                                                   if (pixel.Alpha == 0)
                                                       return SKColors.Black;

                                                   //all other colors are set to be fully opaque
                                                   return pixel.WithAlpha(byte.MaxValue);
                                               })
                                           .ToArray();

            existingPixels.CopyTo(pixelBuffer);
        } else if (options.Ditherer is not null)
        {
            using var dSession = options.Ditherer!.Initialize(source, qSession);

            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
                {
                    var color = bitmap.GetPixel(x, y);

                    var ditheredColor = dSession.GetDitheredColor(color.ToColor32(), x, y)
                                                .ToSKColor();

                    pixelBuffer[y * image.Width + x] = ditheredColor;
                }
            }
        } else
            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
                {
                    var color = bitmap.GetPixel(x, y);

                    pixelBuffer[y * image.Width + x] = qSession.GetQuantizedColor(color.ToColor32())
                                                               .ToSKColor();
                }
            }

        var quantizedImage = SKImage.FromBitmap(quantizedBitmap);
        Palette? palette;

        if (colorCount <= options.MaxColors)
            palette = new Palette(
                existingPixels.Distinct()
                              .OrderBy(c => c.GetLuminance()));
        else
            palette = qSession.Palette!.ToDALibPalette();

        return new Palettized<SKImage>
        {
            Entity = quantizedImage,
            Palette = palette
        };
    }

    /// <summary>
    ///     Quantizes the given images using the specified quantizer options
    /// </summary>
    /// <param name="options">
    ///     The quantizer options.
    /// </param>
    /// <param name="images">
    ///     The images to be quantized.
    /// </param>
    /// <remarks>
    ///     Quantization is the process of reducing the number of colors in an image. This method uses the Wu algorithm. All
    ///     provided images will be quantized together so that the resulting palette is the same for all images
    /// </remarks>
    public static Palettized<SKImageCollection> QuantizeMultiple(QuantizerOptions options, params SKImage[] images)
    {
        const int PADDING = 1;

        //create a mosaic of all the individual images
        using var mosaic = CreateMosaic(PADDING, images);
        using var quantizedMosaic = Quantize(options, mosaic);
        using var bitmap = SKBitmap.FromImage(quantizedMosaic.Entity);

        var quantizedImages = new SKImageCollection([]);
        var x = 0;

        for (var i = 0; i < images.Length; i++)
        {
            var originalImage = images[i];
            using var quantizedBitmap = new SKBitmap(originalImage.Info);

            //extract the quantized parts out of the mosaic
            bitmap.ExtractSubset(
                quantizedBitmap,
                new SKRectI(
                    x,
                    0,
                    x + originalImage.Width,
                    originalImage.Height));

            x += quantizedBitmap.Width + PADDING;

            var image = SKImage.FromBitmap(quantizedBitmap);
            quantizedImages.Add(image);
        }

        return new Palettized<SKImageCollection>
        {
            Entity = quantizedImages,
            Palette = quantizedMosaic.Palette
        };
    }
}