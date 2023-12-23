using System.Collections.Generic;
using System.Linq;
using DALib.Extensions;
using KGySoft.Drawing.Imaging;
using KGySoft.Drawing.SkiaSharp;
using SkiaSharp;

namespace DALib.Utility;

public static class ImageProcessor
{
    public static SKImage CreateMosaic(SKColorType colorType, int padding = 1, params SKImage[] images)
    {
        var width = images.Sum(img => img.Width) + (images.Length - 1) * padding;
        var height = images.Max(img => img.Height);

        using var bitmap = new SKBitmap(
            width,
            height,
            colorType,
            SKAlphaType.Premul);

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

    public static Palettized<SKImage> Quantize(QuantizerOptions options, SKImage image)
    {
        using var bitmap = SKBitmap.FromImage(image);
        IQuantizer quantizer = OptimizedPaletteQuantizer.Wu(options.MaxColors, alphaThreshold: 0);
        var source = bitmap.GetReadableBitmapData();

        using var qSession = quantizer.Initialize(source);
        using var quantizedBitmap = new SKBitmap(image.Info.WithColorType(options.ColorType));

        //if a ditherer was specified, use it
        if (options.Ditherer is not null)
        {
            using var dSession = options.Ditherer!.Initialize(source, qSession);

            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
                {
                    var color = bitmap.GetPixel(x, y);

                    var ditheredColor = dSession.GetDitheredColor(color.ToColor32(), x, y)
                                                .ToSKColor();
                    quantizedBitmap.SetPixel(x, y, ditheredColor);
                }
            }
        } else //otherwise, just quantize the image
            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
                {
                    var color = bitmap.GetPixel(x, y);

                    quantizedBitmap.SetPixel(
                        x,
                        y,
                        qSession.GetQuantizedColor(color.ToColor32())
                                .ToSKColor());
                }
            }

        var quantizedImage = SKImage.FromBitmap(quantizedBitmap);

        return new Palettized<SKImage>
        {
            Entity = quantizedImage,
            Palette = qSession.Palette!.ToDALibPalette()
        };
    }

    public static Palettized<SKImageCollection> QuantizeMultiple(QuantizerOptions options, params SKImage[] images)
    {
        const int PADDING = 1;

        //create a mosaic of all of the individual images
        using var mosaic = CreateMosaic(options.ColorType, PADDING, images);
        using var quantizedMosaic = Quantize(options, mosaic);
        using var bitmap = SKBitmap.FromImage(quantizedMosaic.Entity);

        var quantizedImages = new List<SKImage>();
        var x = 0;

        for (var i = 0; i < images.Length; i++)
        {
            var originalImage = images[i];
            using var quantizedBitmap = new SKBitmap(originalImage.Info.WithColorType(options.ColorType));

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
            Entity = new SKImageCollection(quantizedImages),
            Palette = quantizedMosaic.Palette
        };
    }
}