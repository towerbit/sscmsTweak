using System.Runtime.InteropServices;

namespace System.IO;

/// <summary>
/// 回收站操作帮助类
/// </summary>
internal static class RecycleBin
{
    #region Windows API 定义

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string pTo;
        public ushort fFlags;
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation([In] ref SHFILEOPSTRUCT lpFileOp);

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOERRORUI = 0x0400;

    #endregion

    /// <summary>
    /// 使用 Windows API 将文件删除到回收站
    /// </summary>
    /// <param name="path">文件路径</param>
    /// <param name="showUI">是否显示用户界面</param>
    /// <returns>操作是否成功</returns>
    internal static bool DeleteFileOrFolder(string path, bool showUI = false)
    {
        try
        {
            // 检查文件是否存在
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                throw new FileNotFoundException($"文件或目录不存在: {path}");
            }

            ushort flags = FOF_ALLOWUNDO;
            if (!showUI)
                flags |= (ushort)(FOF_SILENT | FOF_NOCONFIRMATION | FOF_NOERRORUI);
            
            var shf = new SHFILEOPSTRUCT
            {
                hwnd = IntPtr.Zero,
                wFunc = FO_DELETE,
                pFrom = path + '\0' + '\0', // 必须以双 null 结尾
                pTo = null,
                fFlags = flags,
                fAnyOperationsAborted = false,
                hNameMappings = IntPtr.Zero,
                lpszProgressTitle = null
            };

            int result = SHFileOperation(ref shf);
            return result == 0 && !shf.fAnyOperationsAborted;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"删除到回收站时发生错误: {ex.Message}");
            return false;
        }
    }
}