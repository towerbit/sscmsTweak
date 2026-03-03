using SixLabors.ImageSharp;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace sscmsTweak
{
    internal class Program
    {
        /// <summary>
        /// 扫描上传的重复图片
        /// </summary>
        /// <param name="folder">wwwroot 所在目录</param>
        /// <returns></returns>
        private static List<ImageInfo> ScanDuplicateUploadImages(string folder)
        {
            var ret = new List<ImageInfo>();
            folder = Path.Combine(folder, "upload", "images");

            var files = new List<string>();
            files.AddRange(Directory.GetFiles(folder, "*.jpg", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories));

            // 使用字典按特征分组以提高性能
            var signatureMap = new Dictionary<string, ImageInfo>();

            foreach (var imageFile in files)
            {
                //var buffer = File.ReadAllBytes(imageFile);
                //using (var image = Image.Load(buffer))
                // 加载整个图片太慢，改为仅读取图片的元数据信息
                try
                {
                    var imageInfo = Image.Identify(imageFile);
                    var info = new ImageInfo
                    {
                        Path = imageFile,
                        FileSize = (int)new FileInfo(imageFile).Length,
                        Width = imageInfo.Width,
                        Height = imageInfo.Height,
                    };

                    // 在集合中查找重复图片, 效率太低，改用字典查找
                    //var duplicate = ret.FirstOrDefault(x => info.IsDuplicateWith(x));
                    //if (null != duplicate)
                    //    info.DuplicateWith = duplicate.Path;
                    //else
                    //    info.DuplicateWith = string.Empty;

                    // 使用尺寸和文件大小作为特征签名快速查找重复
                    string signature = $"{info.Width}_{info.Height}_{info.FileSize}";
                    if (signatureMap.TryGetValue(signature, out var duplicate))
                    {
                        // 找到特征相同的图片，标记为重复
                        info.DuplicateWith = duplicate.Path;
                        Console.WriteLine($"==   重复图片: {info.Path} ");
                    }
                    else
                    {
                        // 第一次出现该特征，作为原始图片记录
                        info.DuplicateWith = string.Empty;
                        signatureMap[signature] = info;
                        Console.WriteLine($"==   唯一图片: {info.Path} ");
                    }
                    
                    ret.Add(info);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"读取图片 {imageFile} 时发生错误: {ex.Message}");
                }
            }
            return ret;
        }

        /// <summary>
        /// 直接替换生成的 html 页面中的图片链接
        /// </summary>
        /// <param name="folder">html 所在的 wwwroot 目录</param>
        /// <param name="images"></param>
        /// <param name="debugMode">测试模式</param>
        /// <param name="usedImages">返回用到的图片集合</param>
        /// <returns></returns>
        private static int UpdateHtmlPages(string folder, 
                                           List<ImageInfo> images, 
                                           bool debugMode, 
                                           out List<string> usedImages)
        {
            int iUpdate = 0;
            usedImages = new List<string>();
            const string PREFIX = "/upload/images/";
            var files =Directory.GetFiles(folder, "*.html", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var needUpdate = false;
                var body = File.ReadAllText(file, Encoding.UTF8);
                // 找出网页内容中的图片, <img width="306" height="180" src="/upload/images/2025/9/s_9395e28cd1fa0dcb.jpg">
                var matches = Regex.Matches(body, @"src=""(.*?)""");
                foreach (Match match in matches)
                {
                    var src = match.Groups[1].Value;
                    // 是直接上传的图片，还原 src 对应的图片本地路径
                    if (src.StartsWith(PREFIX, StringComparison.OrdinalIgnoreCase))
                    {
                        // 如 /upload/images/2025/9/s_9395e28cd1fa0dcb.jpg
                        // 注意 Path.Combine 拼合的后续路径中不能以 \ 或 / 开头
                        var imagePath = Path.Combine(folder, src.Substring(1).Replace("/", "\\"));
                        var duplicate = images.FirstOrDefault(x => imagePath.Equals(x.Path));
                        if (null != duplicate && duplicate.IsDuplicate)
                        {
                            // 是重复图片，找到对应的唯一图片
                            if(!usedImages.Contains(duplicate.DuplicateWith))
                                usedImages.Add(duplicate.DuplicateWith); // 记录已使用的图片

                            var newSrc = duplicate.DuplicateWith.Replace(folder, "/")
                                                                .Replace("\\", "/")
                                                                .Replace("//", "/");
                            if (!src.Equals(newSrc))
                            {
                                Console.WriteLine($"替换 {src} 为 {newSrc}");
                                body = body.Replace(src, newSrc);
                                needUpdate = true;
                            }
                        }
                        else
                            if (!usedImages.Contains(imagePath))
                                usedImages.Add(imagePath); // 记录已使用的图片
                    }
                }
                if (needUpdate)
                {
                    
                    if(debugMode)
                        Console.WriteLine($"==   将写入文件:{file}");
                    else
                    {
                        Console.WriteLine($"==   写入文件: {file}");
                        File.WriteAllText(file, body, Encoding.UTF8);
                    }
                    iUpdate++;
                }
            }
            return iUpdate;
        }
        
        private static int DeleteNoUsedImages(List<ImageInfo> images, List<string> usedImages, bool debugMode, bool deleteToRecycleBin = true)
        {
            int iDelete = 0;
            foreach (var info in images)
            {
                if (info.IsDuplicate ||              // 是重复图片
                    !usedImages.Contains(info.Path)) // 没有被引用
                {
                    if (debugMode)
                        Console.WriteLine($"==   将删除图片：{info.Path}");
                    else
                    {
                        Console.WriteLine($"==   删除图片：{info.Path}");
                        if (OperatingSystem.IsWindows() && deleteToRecycleBin)
                            RecycleBin.DeleteFileOrFolder(info.Path); // Windows 删除到回收站
                        else
                            File.Delete(info.Path);
                    }
                    iDelete++;
                }
            }
            return iDelete;
        }
        private static int ResizeImages(List<ImageInfo> images, bool debugMode, Size maxSize)
        { 
            int iResize = 0;
            // 注意，剔除已删除的文件
            foreach (var info in images.Where(x => !x.IsDuplicate && File.Exists(x.Path)))
            {
                // 仅处理保留的非重复图片，且宽高都大于最大尺寸的
                if (info.Width > maxSize.Width &&
                    info.Height > maxSize.Height)
                {
                    // 按比例缩小图片
                    var tempFile = Path.GetTempFileName();
                    var newSize = ImageResizer.ResizeImage(info.Path, tempFile, maxSize.Width, maxSize.Height);
                    File.Delete(info.Path);
                    File.Move(tempFile, info.Path);
                    Console.WriteLine($"==   缩小尺寸: {info.Path}");
                    Console.WriteLine($"==   {info.Width}x{info.FileSize} => {newSize.Width}x{newSize.Height}");
                    iResize++;
                }
            }
            return iResize;
        }

        private static Size ParseSize(string arg)
        {
            var szParams = arg.ToLower()
                              .Split("x");
            if (szParams.Length == 2)
            {
                if (int.TryParse(szParams[0], out int w) &&
                    int.TryParse(szParams[1], out int h))
                    return new Size(w, h);
                else
                    Console.WriteLine($"尺寸解析出错: {szParams}");
            }
            else
                Console.WriteLine($"无效的缩小尺寸: {szParams}");
            return Size.Empty;
        }

        private static void ShowHelp()
        {
            Console.WriteLine(@"
用法说明:
sscmsTweak.exe [--path=. --debug --resize=1920x1080 --delete]
参数列表:
--path=xxx          指定 wwwroot 目录, 不传参则在当前目录下搜索
--debug             测试模式，不实际修改和删除文件
--resize=1920x1080  指定图片最大尺寸, 默认缩小为 1920x1080, 0x0 表示不改变尺寸
--delete            彻底删除文件, Windows 默认删除到回收站");
            Environment.Exit(0);
        }

        static void Main(string[] args)
        {
            string wwwroot = AppDomain.CurrentDomain.BaseDirectory;
            bool debugMode = false;
            bool deleteToRecycleBin = true;
            Size maxSize = new Size(1920,1080);
            foreach (var arg in args)
            {
                if (arg.StartsWith("--path=", StringComparison.OrdinalIgnoreCase))
                    wwwroot = arg.Substring("--path=".Length);
                else if (arg.Equals("--debug", StringComparison.OrdinalIgnoreCase))
                    debugMode = true;
                else if (arg.Equals("--delete", StringComparison.OrdinalIgnoreCase))
                    deleteToRecycleBin = false;
                else if (arg.StartsWith("--resize=", StringComparison.OrdinalIgnoreCase))
                    maxSize = ParseSize(arg.Substring("--resize=".Length));
                else if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || 
                         arg.Equals("/?", StringComparison.OrdinalIgnoreCase))
                    ShowHelp();
            }
            
            if (!Directory.Exists(wwwroot))
                throw new DirectoryNotFoundException($"目录不存在: {wwwroot}");
            Console.WriteLine($"== 程序版本: {Assembly.GetExecutingAssembly().GetName().Version}");
;            if (debugMode)
                Console.WriteLine("== 测试模式");
            if (maxSize != Size.Empty)
                Console.WriteLine($"== 缩小尺寸: {maxSize.Width}x{maxSize.Height}");
            if (deleteToRecycleBin)
                Console.WriteLine("== 删除方式: 删除到回收站");
            else
                Console.WriteLine("== 删除方式: 直接删除文件");

            Console.WriteLine($"== 处理目录: {wwwroot}");

            // 查找重复的图片，建立重复图片的索引集合，选择第一张作为唯一图片
            var images = ScanDuplicateUploadImages(wwwroot);
            Console.WriteLine($"== 共找到 {images.Count(x => x.IsDuplicate)} 个重复图片文件");
            
            Console.WriteLine($"== 开始替换生成的 html 页面中重复图片的链接");
            int iUpdate = UpdateHtmlPages(wwwroot, images, debugMode, out var usedImages);
            Console.WriteLine($"== 共替换 {iUpdate} 个包含重复图片的文件");

            Console.WriteLine("== 开始删除所有未使用图片(包括重复图片)");
            int iDelete = DeleteNoUsedImages(images, usedImages, debugMode, deleteToRecycleBin);
            Console.WriteLine($"== 共删除 {iDelete} 张未使用的图片");

            if (maxSize != Size.Empty)
            {
                Console.WriteLine($"== 开始调整图片的尺寸");
                int iResize = ResizeImages(images, debugMode, maxSize);
                Console.WriteLine($"== 共调整 {iResize} 张图片的尺寸");
            }
        }
    }

    public record dtoContent(long Id, string ImageUrl, string Body);

    public class ImageInfo
    {
        private string _path = string.Empty;
        /// <summary>
        /// 图片路径
        /// </summary>
        public string Path
        {
            get => _path;
            set
            {
                _path = value;
                _fileName = System.IO.Path.GetFileName(value);
            }
        }
        private string _fileName = string.Empty;
        public string FileName => _fileName; 
        public int FileSize { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }


        /// <summary>
        /// 与之重复的唯一图片路径
        /// </summary>
        private string _duplicateWith = string.Empty;
        public string DuplicateWith 
        { 
            get => _duplicateWith;
            set { 
                _duplicateWith = value;
                _bDuplicate = !string.IsNullOrEmpty(value);
            } 
        }

        private bool _bDuplicate;
        /// <summary>
        /// 是否为重复图片
        /// </summary>
        public bool IsDuplicate => _bDuplicate;
        
        //public bool IsDuplicateWith(ImageInfo other)
        //{
        //    return !Path.Equals(other.Path) && 
        //        FileSize == other.FileSize && 
        //        Width == other.Width && 
        //        Height == other.Height;
        //}
    }
}
