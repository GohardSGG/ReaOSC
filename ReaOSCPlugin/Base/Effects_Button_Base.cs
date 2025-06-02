// 文件名: Base/Effects_Button_Base.cs
namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.Globalization; // 用于十六进制颜色解析中的 NumberStyles
    using System.Linq;          // 用于 Linq 方法，如 Max()
    using System.Threading.Tasks; // 用于 Task.Delay
    using System.IO;            // 用于文件和路径操作，例如 Path 和 File，以及 StreamReader
    using System.Reflection;    // 用于 Assembly.GetExecutingAssembly()

    using Newtonsoft.Json;    // 用于JSON的序列化和反序列化
    using Loupedeck.ReaOSCPlugin; // 确保可以访问主插件类 (如果需要静态成员，但当前版本不直接依赖)
    using Loupedeck;            // Loupedeck SDK 核心命名空间

    /// <summary>
    /// 效果器按钮的“工厂”基类。
    /// 此类在插件启动时，会读取嵌入的 Effects_List.json 文件，
    /// 并根据其中的配置动态生成所有“添加效果器”的按钮。
    /// </summary>
    public class Effects_Button_Base : PluginDynamicCommand
    {
        // 字典，用于存储所有从JSON加载的效果器按钮的配置
        // Key是唯一的OSC路径 (actionParameter)，Value是完整的ButtonConfig对象
        private readonly Dictionary<string, ButtonConfig> _buttonConfigs = new Dictionary<string, ButtonConfig>();

        // 字典，用于跟踪每个按钮的瞬时高亮状态
        // Key是唯一的OSC路径 (actionParameter)，Value表示是否高亮
        private readonly Dictionary<string, bool> _temporaryActiveStates = new Dictionary<string, bool>();

        /// <summary>
        /// 构造函数在Loupedeck实例化此动作类时执行。
        /// 主要负责读取嵌入的JSON配置，并根据配置创建Loupedeck动作参数。
        /// </summary>
        public Effects_Button_Base() : base()
        {
            PluginLog.Info("Effects_Button_Base 构造函数开始执行 (嵌入资源方案)。");
            string jsonContent = "";
            try
            {
                // 1. 获取当前执行此代码的程序集 (即插件的DLL)
                var assembly = Assembly.GetExecutingAssembly();

                // 2. 构建嵌入资源的完整逻辑名称
                //    通常格式是: <默认项目命名空间>.<文件夹路径点分隔>.<文件名>
                //    例如，如果您的项目默认命名空间是 "Loupedeck.ReaOSCPlugin"，
                //    并且 Effects_List.json 在项目的 "Effects" 文件夹下，
                //    那么资源名很可能是 "Loupedeck.ReaOSCPlugin.Effects.Effects_List.json"。
                //    【重要】您需要根据您的实际项目结构和默认命名空间来确认和调整这个名称。
                string resourceName = "Loupedeck.ReaOSCPlugin.Effects.Effects_List.json";
                // 您可以通过在主插件的 Load() 方法中打印 this.Assembly.GetManifestResourceNames() 来查看所有可用的嵌入资源名称，以获取准确的名称。

                PluginLog.Info($"尝试读取嵌入资源: {resourceName}");

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        PluginLog.Error($"无法找到嵌入资源: '{resourceName}'。请检查：1. Effects_List.json 是否在项目指定的路径中。2. 其“生成操作”是否为“嵌入的资源”。3. 上述 resourceName 是否与程序集内的实际资源名称完全匹配（包括大小写和命名空间前缀）。");
                        // 打印所有可用的嵌入资源名称以帮助调试
                        PluginLog.Info("项目中可用的嵌入资源名称列表:");
                        var availableResources = assembly.GetManifestResourceNames();
                        if (availableResources.Length == 0)
                        {
                            PluginLog.Warning("未找到任何嵌入资源！请检查文件属性。");
                        }
                        foreach (var name in availableResources)
                        {
                            PluginLog.Info($"- {name}");
                        }
                        return; // 无法找到资源，则不继续
                    }
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        jsonContent = reader.ReadToEnd();
                    }
                }

                if (String.IsNullOrEmpty(jsonContent))
                {
                    PluginLog.Error($"嵌入资源 {resourceName} 内容为空。");
                    return;
                }
                PluginLog.Info($"成功从嵌入资源 {resourceName} 读取内容。");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "读取嵌入的 Effects_List.json 文件时发生严重错误。");
                return;
            }

            var groupedConfigs = JsonConvert.DeserializeObject<Dictionary<string, List<ButtonConfig>>>(jsonContent);
            if (groupedConfigs == null)
            {
                PluginLog.Error("反序列化 Effects_List.json 失败。");
                return;
            }

            foreach (var group in groupedConfigs)
            {
                var groupName = group.Key;
                foreach (var config in group.Value)
                {
                    var description = $"插入 {config.DisplayName} 效果器";
                    var oscPath = $"Add/{groupName.Replace(" FX", "")}/{config.DisplayName}";
                    var actionParameter = oscPath;
                    this._buttonConfigs[actionParameter] = config;
                    this._temporaryActiveStates[actionParameter] = false;
                    this.AddParameter(actionParameter, config.DisplayName, groupName, description);
                    PluginLog.Verbose($"Effects_Button_Base: 已添加参数: {actionParameter} (DisplayName: {config.DisplayName})");
                }
            }
            PluginLog.Info("Effects_Button_Base 构造函数成功执行完毕，已从嵌入的JSON加载按钮。");
        }

        /// <summary>
        /// 将十六进制颜色字符串（如 "#RRGGBB"）转换为Loupedeck的BitmapColor对象。
        /// </summary>
        /// <param name="hexColor">十六进制颜色字符串。</param>
        /// <returns>Loupedeck的BitmapColor对象。如果格式不正确，默认为白色。</returns>
        private static BitmapColor HexToBitmapColor(string hexColor)
        {
            if (String.IsNullOrEmpty(hexColor) || !hexColor.StartsWith("#"))
            {
                return BitmapColor.White; // 默认为白色
            }
            var hex = hexColor.Substring(1); // 去掉 '#'
            if (hex.Length != 6) // 检查长度是否为6 (RRGGBB)
            {
                return BitmapColor.White; // 格式错误时默认为白色
            }
            try
            {
                var r = (byte)Int32.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                var g = (byte)Int32.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                var b = (byte)Int32.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                return new BitmapColor(r, g, b);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"无法解析十六进制颜色: {hexColor}");
                return BitmapColor.Red; // 解析错误时返回品红色，便于调试
            }
        }

        /// <summary>
        /// 当用户点击任何一个由本工厂创建的按钮时，此方法会被调用。
        /// </summary>
        /// <param name="actionParameter">被点击按钮的唯一ID（即OSC路径）。</param>
        protected override void RunCommand(string actionParameter)
        {
            if (!this._buttonConfigs.ContainsKey(actionParameter))
                return;
            ReaOSCPlugin.SendFXMessage(actionParameter, 1); // 假设 ReaOSCPlugin.SendFXMessage 是您主插件类中的静态方法
            PluginLog.Info($"已触发效果器添加请求: {actionParameter}");

            // 实现瞬时高亮逻辑
            this._temporaryActiveStates[actionParameter] = true;
            this.ActionImageChanged(actionParameter); // 通知Loupedeck服务，按钮图像已更改，需要重绘

            Task.Delay(200).ContinueWith(_ =>
            {
                // 在回调中再次检查键是否存在，因为按钮可能在延迟期间被移除或插件卸载
                if (this._temporaryActiveStates.ContainsKey(actionParameter))
                {
                    this._temporaryActiveStates[actionParameter] = false;
                    this.ActionImageChanged(actionParameter); // 通知Loupedeck服务，按钮图像已更改，恢复正常
                }
            });
        }

        /// <summary>
        /// 根据标题的长度和结构（是否包含空格），自动计算并返回合适的字体大小。
        /// </summary>
        /// <param name="title">按钮的主标题。</param>
        /// <returns>计算出的字体大小。</returns>
        private int GetAutomaticTitleFontSize(String title)
        {
            if (String.IsNullOrEmpty(title))
            {
                return 23; // 如果标题为空，返回一个默认的大字号
            }

            var totalLengthWithSpaces = title.Length; // 获取标题总长度（包含空格）
            int effectiveLength; // 用于最终决定字号的“有效长度”

            if (totalLengthWithSpaces <= 10)
            {
                // 如果总长度（含空格）不超过10个字符，则认为它可以单行显示，
                // 直接使用总长度（含空格）作为判断字号的依据。
                effectiveLength = totalLengthWithSpaces;
            }
            else
            {
                // 如果总长度超过10个字符，则认为它可能需要换行（或已经很长），
                // 此时按空格分割，取最长的那个单词的长度作为判断字号的依据。
                var words = title.Split(' ');
                effectiveLength = words.Length > 0 ? words.Max(word => word.Length) : 0;

                // 处理特殊情况：如果标题全是空格但长度大于0 (例如 "   ")
                // 上面的 Max(word => word.Length) 会返回0。
                // 这种情况下，我们也按总长度来处理，或者给一个较小的默认字号。
                if (effectiveLength == 0 && totalLengthWithSpaces > 0)
                {
                    // 如果全是空格，effectiveLength 会是0，这里我们让它取一个值以匹配switch的最后一个case
                    // 或者可以根据totalLengthWithSpaces再细化
                    effectiveLength = totalLengthWithSpaces; // 或者直接设为很大的数，让它落入switch的最后一个case
                }
            }

            // 根据计算出的“有效长度”应用您的字号规则
            return effectiveLength switch
            {
                1 => 38,
                2 => 33,
                3 => 31,
                4 => 26,
                5 => 26,
                >= 6 and <= 7 => 20,
                8 => 18,
                9 => 17,
                10 => 16,
                11 => 13,
                _ => 18 // 默认值，处理超过11个字符或上面未覆盖的edge case
            };
        }

        /// <summary>
        /// 当Loupedeck需要绘制按钮时，此方法会被调用。
        /// </summary>
        /// <param name="actionParameter">需要绘制的按钮的唯一ID（即OSC路径）。</param>
        /// <param name="imageSize">Loupedeck服务期望的图像尺寸。</param>
        /// <returns>生成的按钮图像。</returns>
        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            // 尝试从配置字典中获取按钮配置
            if (!this._buttonConfigs.TryGetValue(actionParameter, out var config))
            {
                // 如果找不到配置，返回默认的基类图像（或一个错误提示图像）
                return base.GetCommandImage(actionParameter, imageSize);
            }

            // 获取按钮当前的瞬时高亮状态
            var isHighlighted = this._temporaryActiveStates.TryGetValue(actionParameter, out var active) && active;
            // 定义背景色和硬编码的高亮颜色
            var backgroundColor = BitmapColor.Black;
            var highlightColor = new BitmapColor(0x50, 0x50, 0x50);

            // 使用BitmapBuilder创建按钮图像
            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                // 根据是否高亮设置背景
                bitmapBuilder.Clear(isHighlighted ? highlightColor : backgroundColor);

                // 绘制主标题
                if (!String.IsNullOrEmpty(config.Title))
                {
                    var autoTitleSize = this.GetAutomaticTitleFontSize(config.Title); // 调用自动计算字号函数
                    var titleColor = String.IsNullOrEmpty(config.TitleColor)
                                     ? BitmapColor.White // 如果JSON中未定义TitleColor，默认为白色
                                     : HexToBitmapColor(config.TitleColor); // 否则使用JSON中定义的颜色

                    // 使用命名参数绘制主标题，让其居中并自动换行
                    bitmapBuilder.DrawText(
                        text: config.Title,
                        fontSize: autoTitleSize, // 使用自动计算出的字号
                        color: titleColor
                    );
                }

                // 绘制次要文本
                if (!String.IsNullOrEmpty(config.Text))
                {
                    // 使用 ?? 运算符为可选参数提供默认值
                    var textSize = config.TextSize ?? 14;
                    var textX = config.TextX ?? 35;
                    var textY = config.TextY ?? 55;
                    var textWidth = config.TextWidth ?? 14;
                    var textHeight = config.TextHeight ?? 14;
                    var textColor = String.IsNullOrEmpty(config.TextColor)
                                    ? BitmapColor.White // 次要文本颜色也默认为白色，如果未在JSON中定义
                                    : HexToBitmapColor(config.TextColor);

                    // 使用命名参数绘制次要文本
                    bitmapBuilder.DrawText(
                        text: config.Text,
                        x: textX,
                        y: textY,
                        width: textWidth,
                        height: textHeight,
                        color: textColor,
                        fontSize: textSize
                    );
                }
                return bitmapBuilder.ToImage(); // 返回最终生成的图像
            }
        }
    }
}