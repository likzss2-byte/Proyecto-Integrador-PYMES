using InventorySystem.Domain;
using SkiaSharp;
using ZXing;
using ZXing.Common;

namespace InventorySystem.Infrastructure.Services;

public sealed class BarcodeScannerService
{
    private static readonly IReadOnlyList<BarcodeFormat> SupportedFormats =
    [
        BarcodeFormat.EAN_13,
        BarcodeFormat.EAN_8,
        BarcodeFormat.UPC_A,
        BarcodeFormat.UPC_E,
        BarcodeFormat.CODE_128,
        BarcodeFormat.CODE_39,
        BarcodeFormat.CODE_93,
        BarcodeFormat.ITF,
        BarcodeFormat.CODABAR
    ];

    public BarcodeScanResult ReadManual(string? value)
    {
        var code = InventoryRules.NormalizeScannedCode(value);
        return code.Length == 0
            ? BarcodeScanResult.Failed("No se recibió ningún código.")
            : BarcodeScanResult.Found(code, "Manual/USB");
    }

    public Task<BarcodeScanResult> DecodeImageAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return Task.FromResult(BarcodeScanResult.Failed("No se seleccionó una imagen."));
        }

        return Task.Run(() => DecodeImage(imagePath), cancellationToken);
    }

    public BarcodeScanResult DecodeRgb24(byte[] rgb, int width, int height, string sourceFormat)
    {
        if (rgb.Length == 0 || width <= 0 || height <= 0)
        {
            return BarcodeScanResult.Failed("No se recibió un fotograma válido.");
        }

        var source = new RGBLuminanceSource(rgb, width, height, RGBLuminanceSource.BitmapFormat.RGB24);
        return Decode(source, sourceFormat);
    }

    public BarcodeScanResult DecodeBgra32(byte[] bgra, int width, int height, string sourceFormat)
    {
        if (bgra.Length == 0 || width <= 0 || height <= 0)
        {
            return BarcodeScanResult.Failed("No se recibió un fotograma válido.");
        }

        var rgb = new byte[width * height * 3];
        var pixelCount = Math.Min(width * height, bgra.Length / 4);
        for (var pixel = 0; pixel < pixelCount; pixel++)
        {
            var sourceOffset = pixel * 4;
            var targetOffset = pixel * 3;
            rgb[targetOffset] = bgra[sourceOffset + 2];
            rgb[targetOffset + 1] = bgra[sourceOffset + 1];
            rgb[targetOffset + 2] = bgra[sourceOffset];
        }

        return DecodeRgb24(rgb, width, height, sourceFormat);
    }

    public BarcodeScanResult DecodeLuminance(byte[] luminance, int width, int height, string sourceFormat)
    {
        if (luminance.Length == 0 || width <= 0 || height <= 0)
        {
            return BarcodeScanResult.Failed("No se recibió un fotograma válido.");
        }

        var source = new PlanarYUVLuminanceSource(luminance, width, height, 0, 0, width, height, false);
        return Decode(source, sourceFormat);
    }

    private BarcodeScanResult DecodeImage(string imagePath)
    {
        if (!File.Exists(imagePath))
        {
            return BarcodeScanResult.Failed("No se encontró la imagen seleccionada.");
        }

        try
        {
            using var bitmap = SKBitmap.Decode(imagePath);
            if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                return BarcodeScanResult.Failed("La imagen no pudo abrirse o no es válida.");
            }

            var pixels = bitmap.Pixels;
            var rgb = new byte[pixels.Length * 3];
            for (var index = 0; index < pixels.Length; index++)
            {
                var offset = index * 3;
                rgb[offset] = pixels[index].Red;
                rgb[offset + 1] = pixels[index].Green;
                rgb[offset + 2] = pixels[index].Blue;
            }

            return DecodeRgb24(rgb, bitmap.Width, bitmap.Height, "Imagen");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return BarcodeScanResult.Failed($"No se pudo leer la imagen: {error.Message}");
        }
    }

    private static BarcodeScanResult Decode(LuminanceSource source, string sourceFormat)
    {
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                TryInverted = true,
                PossibleFormats = SupportedFormats.ToList()
            }
        };
        var result = reader.Decode(source);
        return result is null || string.IsNullOrWhiteSpace(result.Text)
            ? BarcodeScanResult.Failed("No se encontró un código compatible.")
            : BarcodeScanResult.Found(result.Text.Trim(), $"{sourceFormat}/{result.BarcodeFormat}");
    }
}
