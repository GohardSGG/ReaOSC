// �����������桿
// ����һ������ȫ������׳����Դ���������ࡣ
// ���ĸĶ����������Ҳ����ļ�ʱ���׳��쳣�ķ��������ڶ��ᰲȫ�ط��� null���Է�ֹ������������
// �汾�������޸��� ReadImage ������ BitmapImage.FromResource �Ĳ���˳�����
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
        /// ʼԴڲ캯еһΡ
        /// </summary>
        public static void Init(Assembly assembly)
        {
            if (_assembly != null)
                return; // ��ֹ�ظ���ʼ��

            _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            _allResourceNames = _assembly.GetManifestResourceNames();
            PluginLog.Info($"[PluginResources] ��ʼ����ɡ�ɨ�赽 {_allResourceNames.Length} ��Ƕ����Դ��");
        }

        /// <summary>
        /// ����������ȫ��������Ƕ����Դ�в��Ҳ����ص�һ��ƥ���ļ�����������Դ·����
        /// </summary>
        /// <param name="fileName">Ҫ���ҵ��ļ��������� "Play.svg"�������ִ�Сд��</param>
        /// <returns>����ҵ���������Դ���������ƣ����򷵻� null��</returns>
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
        /// ָ "ļ" ǶԴļб
        /// </summary>
        /// <param name="folderName">Դ "ռ·" `Loupedeck.ReaOSCPlugin.Assets`</param>
        /// <returns>ƥԴƵַ飻ҲΪ顣</returns>
        public static String[] GetFilesInFolder(String folderName)
        {
            if (String.IsNullOrEmpty(folderName) || _allResourceNames == null)
            {
                return new String[0];
            }
            return _allResourceNames.Where(r => r.StartsWith(folderName + ".", StringComparison.InvariantCultureIgnoreCase)).ToArray();
        }

        /// <summary>
        /// ʹʽƥԴļ
        /// </summary>
        /// <param name="regexPattern">ʽַ</param>
        /// <returns>ƥԴƵַ飻ҲΪ顣</returns>
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
        /// ȫػȡָǶԴԭʼ
        /// </summary>
        /// <param name="fileName">ҪҵԴļ</param>
        /// <returns>һ Stream Ҳļ򷵻 null</returns>
        public static Stream GetStream(String fileName)
        {
            var resourcePath = FindFile(fileName);
            return String.IsNullOrEmpty(resourcePath) ? null : _assembly.GetManifestResourceStream(resourcePath);
        }

        /// <summary>
        /// ȫضȡָǶʽıļ
        /// </summary>
        /// <param name="fileName">Ҫȡıļ</param>
        /// <returns>ļıݵַҲļ򷵻 null</returns>
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
        /// ����������ȫ�ض�ȡָ����Ƕ��ʽ�������ļ���
        /// </summary>
        /// <param name="fileName">Ҫ��ȡ�Ķ������ļ�����</param>
        /// <returns>�����ļ����ж��������ݵ��ֽ����飻����Ҳ����ļ��򷵻� null��</returns>
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
        /// ����������ȫ�ض�ȡͼ���ļ������Զ����� SVG �� PNG���˰汾���޸�����˳�����⡣
        /// </summary>
        /// <param name="fileName">Ҫ��ȡ��ͼ���ļ�����</param>
        /// <returns>һ�� BitmapImage ��������Ҳ��������ʧ�ܣ��򷵻� null��</returns>
        public static BitmapImage ReadImage(String fileName)
        {
            try
            {
                var resourcePath = FindFile(fileName);
                if (String.IsNullOrEmpty(resourcePath))
                {
                    return null; // Ҳļȫ null
                }

                if (Path.GetExtension(resourcePath).ToLowerInvariant() == ".svg")
                {
                    // ���������ѽ�����˳�����Ϊ (assembly, resourcePath)
                    return BitmapImage.FromResource(_assembly, resourcePath);
                }
                else
                {
                    // ����PNG��λͼ��Loupedeck SDK �� ReadImage ��չ������ֱ��
                    return EmbeddedResources.ReadImage(resourcePath);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[PluginResources] ��Ƕ����Դ����ͼ�� '{fileName}' ʱ��������");
                return null;
            }
        }

        /// <summary>
        /// ����������ȫ�ؽ�һ��Ƕ��ʽ��Դ��ȡ���û���Ӳ���ϡ�
        /// </summary>
        /// <param name="fileName">Ҫ��ȡ��Ƕ����Դ���ļ�����</param>
        /// <param name="filePathName">Ҫ������Ӳ���ϵ�����·�����ļ�����</param>
        /// <returns>�����ȡ�ɹ����� true�����Դ�ļ��Ҳ�����д��ʧ���򷵻� false��</returns>
        public static Boolean ExtractFile(String fileName, String filePathName)
        {
            try
            {
                using (var stream = GetStream(fileName))
                {
                    if (stream == null)
                    {
                        PluginLog.Warning($"[PluginResources] �޷���ȡ�ļ�����ΪԴ��Դ '{fileName}' δ�ҵ���");
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
                PluginLog.Error(ex, $"[PluginResources] ��ȡ��Դ '{fileName}' �� '{filePathName}' ʱʧ�ܡ�");
                return false;
            }
        }
    }
}