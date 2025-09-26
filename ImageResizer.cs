using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// 图片处理工具类
/// </summary>
public static class ImageResizer
{
    /// <summary>
    /// 按比例缩小图片，确保缩小后的图片要么高度不超过 maxHeight，要么宽度不超过 maxWidth
    /// </summary>
    /// <param name="inputPath">输入图片路径</param>
    /// <param name="outputPath">输出图片路径</param>
    /// <param name="maxWidth">最大宽度</param>
    /// <param name="maxHeight">最大高度</param>
    /// <param name="quality">JPEG 质量 (1-100)，默认 80</param>
    public static Size ResizeImage(string inputPath, string outputPath, int maxWidth, int maxHeight, int quality = 80)
    {
        using var image = Image.Load(inputPath);
        var format = Image.DetectFormat(inputPath);
        return ResizeImage(image, outputPath, maxWidth, maxHeight, format, quality);
    }

    /// <summary>
    /// 按比例缩小图片，确保缩小后的图片要么高度不超过 maxHeight，要么宽度不超过 maxWidth
    /// </summary>
    /// <param name="image">ImageSharp 图片对象</param>
    /// <param name="outputPath">输出图片路径</param>
    /// <param name="maxWidth">最大宽度</param>
    /// <param name="maxHeight">最大高度</param>
    /// <param name="format">图片格式</param>
    /// <param name="quality">JPEG 质量 (1-100)</param>
    public static Size ResizeImage(Image image, string outputPath, int maxWidth, int maxHeight, IImageFormat format, int quality)
    {
        // 计算缩放比例
        var scale = CalculateScale(image.Width, image.Height, maxWidth, maxHeight);
        
        if (scale < 1.0) // 只有当图片需要缩小时才处理
        {
            var newWidth = (int)(image.Width * scale);
            var newHeight = (int)(image.Height * scale);
            
            // 调整图片大小
            image.Mutate(x => x.Resize(newWidth, newHeight));
        }
        try
        {
            // 保存图片，保持原始格式
            SaveImageWithFormat(image, outputPath, format, quality);
            return image.Size;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存图片时出错: {ex.Message}");
            return Size.Empty;
        }
    }

    /// <summary>
    /// 按比例缩小图片并返回处理后的图片对象，确保缩小后的图片要么高度不超过 maxHeight，要么宽度不超过 maxWidth
    /// </summary>
    /// <param name="inputPath">输入图片路径</param>
    /// <param name="maxWidth">最大宽度</param>
    /// <param name="maxHeight">最大高度</param>
    /// <returns>处理后的图片对象和格式信息</returns>
    public static (Image<Rgba32> Image, IImageFormat Format) ResizeImage(string inputPath, int maxWidth, int maxHeight)
    {
        using var originalImage = Image.Load<Rgba32>(inputPath);
        var format = Image.DetectFormat(inputPath);
        var scale = CalculateScale(originalImage.Width, originalImage.Height, maxWidth, maxHeight);
        
        if (scale < 1.0)
        {
            var newWidth = (int)(originalImage.Width * scale);
            var newHeight = (int)(originalImage.Height * scale);
            
            var resizedImage = originalImage.Clone(x => x.Resize(newWidth, newHeight));
            return (resizedImage, format);
        }
        
        return (originalImage.Clone(), format); // 返回副本
    }

    /// <summary>
    /// 按比例缩小图片并返回处理后的图片对象
    /// </summary>
    /// <param name="image">ImageSharp 图片对象</param>
    /// <param name="format">图片格式</param>
    /// <param name="maxWidth">最大宽度</param>
    /// <param name="maxHeight">最大高度</param>
    /// <returns>处理后的图片对象</returns>
    public static Image<Rgba32> ResizeImage(Image<Rgba32> image, IImageFormat format, int maxWidth, int maxHeight)
    {
        var scale = CalculateScale(image.Width, image.Height, maxWidth, maxHeight);
        
        if (scale < 1.0)
        {
            var newWidth = (int)(image.Width * scale);
            var newHeight = (int)(image.Height * scale);
            
            var resizedImage = image.Clone(x => x.Resize(newWidth, newHeight));
            return resizedImage;
        }
        
        return image.Clone(); // 返回副本
    }

    /// <summary>
    /// 计算缩放比例，确保缩小后的图片要么高度不超过 maxHeight，要么宽度不超过 maxWidth
    /// </summary>
    /// <param name="width">原图宽度</param>
    /// <param name="height">原图高度</param>
    /// <param name="maxWidth">最大宽度</param>
    /// <param name="maxHeight">最大高度</param>
    /// <returns>缩放比例</returns>
    private static double CalculateScale(int width, int height, int maxWidth, int maxHeight)
    {
        // 如果宽度和高度都小于等于限制，则不需要缩放
        if (width <= maxWidth && height <= maxHeight)
        {
            return 1.0;
        }
        
        // 计算按宽度和高度分别缩放的比例
        double widthScale = (double)maxWidth / width;
        double heightScale = (double)maxHeight / height;
        
        // 选择较小的缩放比例，确保至少有一个维度不超过限制
        return Math.Min(widthScale, heightScale);
    }

    /// <summary>
    /// 按指定格式保存图片
    /// </summary>
    /// <param name="image">图片对象</param>
    /// <param name="outputPath">输出路径</param>
    /// <param name="quality">JPEG 质量</param>
    /// <param name="format">图片格式</param>
    private static void SaveImageWithFormat(Image image, string outputPath, IImageFormat format, int quality)
    {
        // 根据原始格式选择编码器
        switch (format?.Name?.ToLowerInvariant())
        {
            case "jpeg":
            case "jpg":
                image.Save(outputPath, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder()
                {
                    Quality = quality
                });
                break;
                
            case "png":
                image.Save(outputPath, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
                break;
                
            case "gif":
                image.Save(outputPath, new SixLabors.ImageSharp.Formats.Gif.GifEncoder());
                break;
                
            case "bmp":
                image.Save(outputPath, new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder());
                break;
                
            case "tiff":
            case "tif":
                image.Save(outputPath, new SixLabors.ImageSharp.Formats.Tiff.TiffEncoder());
                break;
                
            case "webp":
                image.Save(outputPath, new SixLabors.ImageSharp.Formats.Webp.WebpEncoder()
                {
                    Quality = quality
                });
                break;
                
            default:
                // 如果无法识别格式，尝试从文件扩展名判断
                SaveImageByExtension(image, outputPath, quality);
                break;
        }
    }

    /// <summary>
    /// 根据文件扩展名保存图片
    /// </summary>
    /// <param name="image">图片对象</param>
    /// <param name="outputPath">输出路径</param>
    /// <param name="quality">JPEG 质量</param>
    private static void SaveImageByExtension(Image image, string outputPath, int quality)
    {
        var extension = Path.GetExtension(outputPath)?.ToLowerInvariant() ?? string.Empty;
        
        switch (extension)
        {
            case ".jpg":
            case ".jpeg":
                image.Save(outputPath, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder()
                {
                    Quality = quality
                });
                break;
                
            case ".png":
                image.Save(outputPath, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
                break;
                
            case ".gif":
                image.Save(outputPath, new SixLabors.ImageSharp.Formats.Gif.GifEncoder());
                break;
                
            case ".bmp":
                image.Save(outputPath, new SixLabors.ImageSharp.Formats.Bmp.BmpEncoder());
                break;
                
            case ".tiff":
            case ".tif":
                image.Save(outputPath, new SixLabors.ImageSharp.Formats.Tiff.TiffEncoder());
                break;
                
            case ".webp":
                image.Save(outputPath, new SixLabors.ImageSharp.Formats.Webp.WebpEncoder()
                {
                    Quality = quality
                });
                break;
                
            default:
                // 默认使用 JPEG
                image.Save(outputPath, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder()
                {
                    Quality = quality
                });
                break;
        }
    }
}