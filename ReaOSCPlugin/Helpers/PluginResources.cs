// 【最终修正版】
// 这是一个更安全、更健壮的资源管理帮助类。
// 核心改动：所有在找不到文件时会抛出异常的方法，现在都会安全地返回 null，以防止插件意外崩溃。
// 版本修正：修复了 ReadImage 方法中 BitmapImage.FromResource 的参数顺序错误。
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
        private static string[] _allResourceNames;

        /// <summary>
        /// 【保留】初始化资源管理器。必须在插件构造函数中调用一次。
        /// </summary>
        public static void Init(Assembly assembly)
        {
            if (_assembly != null)
                return; // 防止重复初始化

            _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            _allResourceNames = _assembly.GetManifestResourceNames();
            PluginLog.Info($"[PluginResources] 初始化完成。扫描到 {_allResourceNames.Length} 个嵌入资源。");
        }

        /// <summary>
        /// 【修正】安全地在所有嵌入资源中查找并返回第一个匹配文件名的完整资源路径。
        /// </summary>
        /// <param name="fileName">要查找的文件名，例如 "Play.svg"。不区分大小写。</param>
        /// <returns>如果找到，返回资源的完整名称；否则返回 null。</returns>
        public static String FindFile(String fileName)
        {
            if (string.IsNullOrEmpty(fileName) || _allResourceNames == null)
            {
                return null;
            }
            // 使用 ToLowerInvariant() 进行不区分区域性的、不区分大小写的稳定比较
            var lowerFileName = fileName.ToLowerInvariant();
            return _allResourceNames.FirstOrDefault(r => r.ToLowerInvariant().EndsWith(lowerFileName));
        }

        /// <summary>
        /// 【保留】检索指定 "虚拟文件夹" 内所有嵌入资源文件的完整名称列表。
        /// </summary>
        /// <param name="folderName">资源的 "命名空间路径"，例如 `Loupedeck.ReaOSCPlugin.Assets`</param>
        /// <returns>包含所有匹配资源完整名称的字符串数组；如果找不到则为空数组。</returns>
        public static String[] GetFilesInFolder(String folderName)
        {
            if (string.IsNullOrEmpty(folderName) || _allResourceNames == null)
            {
                return new String[0];
            }
            return _allResourceNames.Where(r => r.StartsWith(folderName + ".", StringComparison.InvariantCultureIgnoreCase)).ToArray();
        }

        /// <summary>
        /// 【保留】使用正则表达式查找所有匹配的资源文件。
        /// </summary>
        /// <param name="regexPattern">正则表达式字符串。</param>
        /// <returns>包含所有匹配资源完整名称的字符串数组；如果找不到则为空数组。</returns>
        public static String[] FindFiles(String regexPattern)
        {
            if (string.IsNullOrEmpty(regexPattern) || _allResourceNames == null)
            {
                return new String[0];
            }
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
            return _allResourceNames.Where(r => regex.IsMatch(r)).ToArray();
        }

        /// <summary>
        /// 【修正】安全地获取指定嵌入资源的原始数据流。
        /// </summary>
        /// <param name="fileName">要查找的资源文件名。</param>
        /// <returns>一个 Stream 对象；如果找不到文件则返回 null。</returns>
        public static Stream GetStream(String fileName)
        {
            var resourcePath = FindFile(fileName);
            return string.IsNullOrEmpty(resourcePath) ? null : _assembly.GetManifestResourceStream(resourcePath);
        }

        /// <summary>
        /// 【修正】安全地读取指定的嵌入式文本文件。
        /// </summary>
        /// <param name="fileName">要读取的文本文件名。</param>
        /// <returns>包含文件所有文本内容的字符串；如果找不到文件则返回 null。</returns>
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
        /// 【修正】安全地读取指定的嵌入式二进制文件。
        /// </summary>
        /// <param name="fileName">要读取的二进制文件名。</param>
        /// <returns>包含文件所有二进制数据的字节数组；如果找不到文件则返回 null。</returns>
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
        /// 【修正】安全地读取图像文件，并自动处理 SVG 和 PNG。此版本已修复参数顺序问题。
        /// </summary>
        /// <param name="fileName">要读取的图像文件名。</param>
        /// <returns>一个 BitmapImage 对象；如果找不到或加载失败，则返回 null。</returns>
        public static BitmapImage ReadImage(string fileName)
        {
            try
            {
                var resourcePath = FindFile(fileName);
                if (string.IsNullOrEmpty(resourcePath))
                {
                    return null; // 找不到文件，安全返回 null
                }

                if (Path.GetExtension(resourcePath).ToLowerInvariant() == ".svg")
                {
                    // 【修正】已将参数顺序更正为 (assembly, resourcePath)
                    return BitmapImage.FromResource(_assembly, resourcePath);
                }
                else
                {
                    // 对于PNG等位图，Loupedeck SDK 的 ReadImage 扩展方法更直接
                    return EmbeddedResources.ReadImage(resourcePath);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[PluginResources] 从嵌入资源加载图像 '{fileName}' 时发生错误。");
                return null;
            }
        }

        /// <summary>
        /// 【修正】安全地将一个嵌入式资源提取到用户的硬盘上。
        /// </summary>
        /// <param name="fileName">要提取的嵌入资源的文件名。</param>
        /// <param name="filePathName">要保存在硬盘上的完整路径和文件名。</param>
        /// <returns>如果提取成功返回 true；如果源文件找不到或写入失败则返回 false。</returns>
        public static bool ExtractFile(String fileName, String filePathName)
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