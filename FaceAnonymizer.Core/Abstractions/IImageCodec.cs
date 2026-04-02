using OpenCvSharp;

namespace FaceAnonymizer.Core.Abstractions;

/// <summary>Формат вихідного зображення.</summary>
public enum ImageOutputFormat
{
    Png,
    Jpeg
}

public interface IImageCodec
{
    Mat DecodeToBgr(ReadOnlySpan<byte> imageBytes);
    byte[] EncodePng(Mat bgr);

    /// <summary>Кодує зображення у вказаному форматі. JPEG — якість 95.</summary>
    byte[] Encode(Mat bgr, ImageOutputFormat format);

    /// <summary>Визначає формат вхідного зображення за magic-bytes.</summary>
    static ImageOutputFormat DetectFormat(ReadOnlySpan<byte> imageBytes)
    {
        // PNG: 89 50 4E 47
        if (imageBytes.Length >= 4
            && imageBytes[0] == 0x89 && imageBytes[1] == 0x50
            && imageBytes[2] == 0x4E && imageBytes[3] == 0x47)
            return ImageOutputFormat.Png;

        // Все інше (JPEG, BMP, WebP) → кодуємо як JPEG (компактний lossy)
        return ImageOutputFormat.Jpeg;
    }

    /// <summary>Розширення файлу для формату.</summary>
    static string Extension(ImageOutputFormat format) => format switch
    {
        ImageOutputFormat.Png => ".png",
        ImageOutputFormat.Jpeg => ".jpg",
        _ => ".jpg"
    };

    /// <summary>MIME-тип для формату.</summary>
    static string ContentType(ImageOutputFormat format) => format switch
    {
        ImageOutputFormat.Png => "image/png",
        ImageOutputFormat.Jpeg => "image/jpeg",
        _ => "image/jpeg"
    };
}
