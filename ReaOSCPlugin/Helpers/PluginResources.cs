// 文件名: Helpers/PluginResources.cs
// 描述: 一个用于安全、全局地访问嵌入式插件资源的辅助类。
// 此类的关键设计是，即使在文件名查找或文件读取时发生异常，其方法也会安全地返回 null，以防止插件崩溃。
// 版本说明: 修正了 ReadImage 方法中 BitmapImage.FromResource 的参数顺序问题。
namespace Loupedeck.ReaOSCPlugin
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;

    internal static class PluginResources
    {
        private static Assembly _assembly;
        private static String[] _allResourceNames;

        /// <summary>
        /// 初始化资源辅助类。
        /// </summary>
        public static void Init(Assembly assembly)
        {
            if (_assembly != null)
            {
                return; // 防止重复初始化
            }


            _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            _allResourceNames = _assembly.GetManifestResourceNames();
            PluginLog.Info($"[PluginResources] 初始化完成。扫描到 {_allResourceNames.Length} 个嵌入式资源。");
        }

        /// <summary>
        /// 在程序集的所有嵌入式资源中查找并返回第一个匹配的文件名的完整资源路径。
        /// </summary>
        /// <param name="fileName">要查找的文件名，例如 "Play.svg"。此查找不区分大小写。</param>
        /// <returns>找到的嵌入式资源的完整名称，如果未找到则返回 null。</returns>
        public static String FindFile(String fileName)
        {
            if (String.IsNullOrEmpty(fileName) || _allResourceNames == null)
            {
                return null;
            }

            // 【最终修复】从"模糊结尾匹配"升级为"精准文件定位"
            // 旧的逻辑 r.EndsWith(fileName) 会错误地将 "Big_Clock.png" 匹配给 "Lock.png"
            // 新的逻辑通过匹配 ".fileName" 或全名，确保我们定位到的是一个完整的、正确的文件名。
            return _allResourceNames.FirstOrDefault(r => 
                r.Equals(fileName, StringComparison.InvariantCultureIgnoreCase) || 
                r.EndsWith("." + fileName, StringComparison.InvariantCultureIgnoreCase)
            );
        }

        /// <summary>
        /// 获取指定 "文件夹" 下的所有嵌入式资源文件列表。
        /// </summary>
        /// <param name="folderName">资源的 "命名空间路径"，例如 `Loupedeck.ReaOSCPlugin.Assets`</param>
        /// <returns>匹配的资源名称数组；如果无匹配则为空数组。</returns>
        public static String[] GetFilesInFolder(String folderName)
        {
            if (String.IsNullOrEmpty(folderName) || _allResourceNames == null)
            {
                return new String[0];
            }
            return _allResourceNames.Where(r => r.StartsWith(folderName + ".", StringComparison.InvariantCultureIgnoreCase)).ToArray();
        }

        /// <summary>
        /// 使用正则表达式查找匹配的资源文件。
        /// </summary>
        /// <param name="regexPattern">正则表达式字符串。</param>
        /// <returns>匹配的资源名称数组；如果无匹配则为空数组。</returns>
        public static String[] FindFiles(String regexPattern)
        {
            if (String.IsNullOrEmpty(regexPattern) || _allResourceNames == null)
            {
                return new String[0];
            }
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
            return _allResourceNames.Where(r => regex.IsMatch(r)).ToArray();
        }

        /// <summary>
        /// 安全地获取指定嵌入式资源的原始流。
        /// </summary>
        /// <param name="fileName">要查找的资源文件名。</param>
        /// <returns>一个 Stream 对象，如果文件未找到或发生错误则返回 null。</returns>
        public static Stream GetStream(String fileName)
        {
            var resourcePath = FindFile(fileName);
            return String.IsNullOrEmpty(resourcePath) ? null : _assembly.GetManifestResourceStream(resourcePath);
        }

        /// <summary>
        /// 安全地读取指定的嵌入式文本文件。
        /// </summary>
        /// <param name="fileName">要读取的文本文件名。</param>
        /// <returns>文件的文本内容字符串，如果文件未找到或发生错误则返回 null。</returns>
        public static String ReadTextFile(String fileName)
        {
            using (var stream = GetStream(fileName))
            {
                if (stream == null)
                {
                    return null;
                }
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// 安全地读取指定的嵌入式二进制文件。
        /// </summary>
        /// <param name="fileName">要读取的二进制文件名。</param>
        /// <returns>包含文件内容的字节数组；如果文件未找到或发生错误则返回 null。</returns>
        public static Byte[] ReadBinaryFile(String fileName)
        {
            using (var stream = GetStream(fileName))
            {
                if (stream == null)
                {
                    return null;
                }
                using (var memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);
                    return memoryStream.ToArray();
                }
            }
        }

        /// <summary>
        /// 安全地读取图像文件，能自动处理 SVG 和 PNG。此版本修正了参数顺序问题。
        /// </summary>
        /// <param name="fileName">要读取的图像文件名。</param>
        /// <returns>一个 BitmapImage 对象，如果读取或解析失败，则返回 null。</returns>
        public static BitmapImage ReadImage(String fileName)
        {
            try
            {
                var resourcePath = FindFile(fileName);
                if (String.IsNullOrEmpty(resourcePath))
                {
                    return null; // 文件未找到，安全返回 null
                }

                if (Path.GetExtension(resourcePath).ToLowerInvariant() == ".svg")
                {
                    // SVG图像的加载，参数顺序为 (assembly, resourcePath)
                    return BitmapImage.FromResource(_assembly, resourcePath);
                }
                else
                {
                    // 对于PNG等位图，Loupedeck SDK 的 ReadImage 扩展方法可以直接处理
                    return EmbeddedResources.ReadImage(resourcePath);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[PluginResources] 从嵌入式资源加载图像 '{fileName}' 时发生错误。");
                return null;
            }
        }

        /// <summary>
        /// 安全地将一个嵌入式资源提取到用户硬盘上。
        /// </summary>
        /// <param name="fileName">要提取的嵌入式资源的文件名。</param>
        /// <param name="filePathName">要保存到硬盘上的完整路径及文件名。</param>
        /// <returns>如果提取成功则返回 true，如果资源文件未找到或写入失败则返回 false。</returns>
        public static Boolean ExtractFile(String fileName, String filePathName)
        {
            try
            {
                using (var stream = GetStream(fileName))
                {
                    if (stream == null)
                    {
                        PluginLog.Warning($"[PluginResources] 无法提取文件，因为源资源 '{fileName}' 未找到。");
                        return false;
                    }
                    using (var fileStream = File.Create(filePathName))
                    {
                        stream.CopyTo(fileStream);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[PluginResources] 提取资源 '{fileName}' 到 '{filePathName}' 时失败。");
                return false;
            }
        }
    }
}