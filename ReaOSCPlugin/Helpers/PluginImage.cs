// 文件名: Helpers/PluginImage.cs
// 描述: 统一的UI元素绘制类

namespace Loupedeck.ReaOSCPlugin.Helpers
{
    using System;
    using System.Globalization; // For HexToBitmapColor
    using System.Linq; // For GetAutomatic...FontSize, if needed for word splitting

    // 确保 Loupedeck 命名空间被正确引用，以便 BitmapImageHorizontalAlignment 等枚举能被找到
    using Loupedeck; 

    using Loupedeck.ReaOSCPlugin.Base; // For ButtonConfig

    public static class PluginImage
    {
        /// <summary>
        /// 主要的通用绘制方法，用于绘制按钮和旋钮等UI元素。
        /// </summary>
        /// <param name="imageSize">控件尺寸。</param>
        /// <param name="config">控件的完整配置 (来自JSON)。</param>
        /// <param name="mainTitleOverride">可选：如果需要覆盖config中的Title/DisplayName。</param>
        /// <param name="valueText">可选：用于显示参数值或次要状态文本。</param>
        /// <param name="isActive">控件的激活状态 (如ToggleButton的ON, CombineButton按下的高亮)。</param>
        /// <param name="currentMode">控件的模式状态 (如2ModeTickDial的模式0或1)。</param>
        /// <param name="customIcon">可选：外部加载的图标。</param>
        /// <param name="forceTextOnly">可选: 强制只绘制文字，即使有图标配置。</param>
        /// <param name="actualAuxText">可选：用于替换config.Text的实际绘制文本。</param>
        /// <returns>绘制好的 BitmapImage。</returns>
        public static BitmapImage DrawElement(
            PluginImageSize imageSize,
            ButtonConfig config,
            string mainTitleOverride = null,
            string valueText = null, // 在Dynamic_Folder_Base的旋钮绘制中有这个概念
            bool isActive = false,
            int currentMode = 0, // 0 通常是默认模式
            BitmapImage customIcon = null,
            bool forceTextOnly = false,
            string actualAuxText = null)
        {
            if (config == null)
            {
                // 如果配置为空，返回一个空白图像或错误提示
                using (var bb = new BitmapBuilder(imageSize))
                {
                    bb.Clear(BitmapColor.Black);
                    bb.DrawText("Cfg?", BitmapColor.Red, GetButtonFontSize("Cfg?")); // 使用按钮字体大小
                    return bb.ToImage();
                }
            }

            // 在这里初始化 valueFontSize 以确保其作用域覆盖后续使用
            var valueFontSize = 0; 

            if (!String.IsNullOrEmpty(valueText))
            {
                valueFontSize = GetDialFontSize(valueText) - 2;
                if (valueFontSize < 8) valueFontSize = 8;
            }

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                // 1. 确定背景色
                BitmapColor currentBgColor = HexToBitmapColor(config.BackgroundColor, BitmapColor.Black); // 默认黑色背景
                
                // 根据ActionType和状态调整颜色
                if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial")
                {
                    currentBgColor = isActive
                        ? HexToBitmapColor(config.ActiveColor, HexToBitmapColor(config.BackgroundColor, BitmapColor.Black)) // 激活时用ActiveColor，否则用BGColor
                        : HexToBitmapColor(config.DeactiveColor, HexToBitmapColor(config.BackgroundColor, BitmapColor.Black)); // 非激活时用DeactiveColor，否则用BGColor
                }
                else if (config.ActionType == "TriggerButton" || config.ActionType == "CombineButton") // 假设isActive代表瞬时高亮
                {
                    if (isActive) // 瞬时高亮
                    {
                        currentBgColor = HexToBitmapColor(config.ActiveColor, new BitmapColor(0x50, 0x50, 0x50)); // 默认高亮为灰色
                    }
                }
                else if (config.ActionType == "2ModeTickDial")
                {
                    currentBgColor = currentMode == 1
                        ? HexToBitmapColor(config.BackgroundColor_Mode2, currentBgColor)
                        : HexToBitmapColor(config.BackgroundColor, currentBgColor);
                }
                // SelectModeButton 的背景色可能也需要根据模式来
                else if (config.ActionType == "SelectModeButton")
                {
                     // General_Button_Base: isModeConsideredActive (通常非第一个模式) 使用 ActiveColor
                     // isActive 在这里可以代表 "模式被选中且不是默认初始模式"
                    currentBgColor = isActive && !String.IsNullOrEmpty(config.ActiveColor) ? HexToBitmapColor(config.ActiveColor) : HexToBitmapColor(config.BackgroundColor, BitmapColor.Black);
                }

                bitmapBuilder.Clear(currentBgColor);

                // 2. 确定主标题和标题颜色
                string titleToDraw = !String.IsNullOrEmpty(mainTitleOverride) ? mainTitleOverride : (config.Title ?? config.DisplayName);
                BitmapColor currentTitleColor = HexToBitmapColor(config.TitleColor, BitmapColor.White); // 默认白色标题
                
                if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial")
                {
                    currentTitleColor = isActive
                        ? HexToBitmapColor(config.ActiveTextColor, BitmapColor.White)
                        : HexToBitmapColor(config.DeactiveTextColor, BitmapColor.White);
                }
                else if (config.ActionType == "2ModeTickDial")
                {
                    titleToDraw = currentMode == 1 ? (config.Title_Mode2 ?? titleToDraw) : titleToDraw;
                    currentTitleColor = currentMode == 1
                        ? HexToBitmapColor(config.TitleColor_Mode2, BitmapColor.White)
                        : HexToBitmapColor(config.TitleColor, BitmapColor.White);
                }
                else if (config.ActionType == "SelectModeButton")
                {
                    // General_Button_Base: 激活时用TitleColor，不激活用DeactiveTextColor
                    // titleToDraw 已经是 mainTitleOverride (通常是当前模式名)
                    currentTitleColor = isActive && !String.IsNullOrEmpty(config.TitleColor)
                            ? HexToBitmapColor(config.TitleColor, BitmapColor.White)
                            : HexToBitmapColor(config.DeactiveTextColor, BitmapColor.White);
                }

                // 3. 绘制图标或主文本
                bool iconDrawn = false;
                if (customIcon != null && !forceTextOnly)
                {
                    // 图标绘制逻辑 (参考 General_Button_Base)
                    int iconHeight = 46; // 默认值
                    int iconWidth = (customIcon.Height > 0) ? (customIcon.Width * iconHeight / customIcon.Height) : customIcon.Width;
                    iconWidth = Math.Max(1, iconWidth);
                     // 如果宽度被调整了，高度也应该按比例调整，或者使用固定高度，这里用固定高度示例
                    // iconHeight = (customIcon.Width > 0 && iconWidth == customIcon.Width) ? customIcon.Height : iconHeight;

                    int iconX = (bitmapBuilder.Width - iconWidth) / 2;
                    int iconY = 8; // 图标稍靠上
                    bitmapBuilder.DrawImage(customIcon, iconX, iconY, iconWidth, iconHeight);
                    
                    // 图标下方绘制主标题 (较小字号)
                    if (!String.IsNullOrEmpty(titleToDraw))
                    {
                        // 强制使用小号字体配合图标
                        var titleFontSizeForIcon = 12; // 或者根据imageSize动态调整
                        var textYUnderIcon = bitmapBuilder.Height - titleFontSizeForIcon - 10; // 底部留白
                         if (iconY + iconHeight < textYUnderIcon) // 确保文字在图标下方
                         {
                            bitmapBuilder.DrawText(text: titleToDraw, x: 0, y: textYUnderIcon, width: bitmapBuilder.Width, height: titleFontSizeForIcon + 2, fontSize: titleFontSizeForIcon, color: currentTitleColor);
                         }
                         else // 如果空间不足，尝试在图标旁边或不画
                         {
                             // Fallback: 简单绘制在中央 (可能会覆盖图标)
                             // bitmapBuilder.DrawText(text: titleToDraw, fontSize: GetButtonFontSize(titleToDraw) / 2, color: currentTitleColor);
                         }
                    }
                    iconDrawn = true;
                }
                
                if (!iconDrawn) // 如果没有图标，或者强制只用文本
                {
                    if (!String.IsNullOrEmpty(titleToDraw))
                    {
                        int titleFontSize;
                        if (config.ActionType != null && (config.ActionType.Contains("Dial") || config.ActionType.Contains("Parameter"))) // ParameterDial, ParameterButton
                        {
                            titleFontSize = GetDialFontSize(titleToDraw);
                        }
                        else
                        {
                            titleFontSize = GetButtonFontSize(titleToDraw);
                        }
                        bitmapBuilder.DrawText(titleToDraw, currentTitleColor, titleFontSize);
                    }
                }

                // 4. 绘制 valueText (如果提供，且没有图标或有特定布局)
                // 这个通常用于旋钮显示当前值，或按钮显示次要信息
                if (!String.IsNullOrEmpty(valueText))
                {
                    // 决定 valueText 的位置和大小
                    // 如果有主标题且没有图标，valueText 可以在主标题下方
                    // 如果有图标，valueText 的位置需要更小心处理
                    // 简单示例：绘制在底部
                    var valueTextColor = currentTitleColor; // 通常与主标题同色或稍暗
                    
                    if (!iconDrawn && !String.IsNullOrEmpty(titleToDraw)) // 在主标题下方
                    {
                         bitmapBuilder.DrawText(valueText, 0, bitmapBuilder.Height - valueFontSize - 10, bitmapBuilder.Width, valueFontSize +2 ,valueTextColor, valueFontSize);
                    }
                    else if (iconDrawn)
                    {
                        // 如果有图标，并且主标题已在图标下方，valueText可能没地方放了，或需要更复杂的布局
                    }
                    else // 没有主标题，也没有图标，valueText 作为主要显示
                    {
                        bitmapBuilder.DrawText(valueText, valueTextColor, GetDialFontSize(valueText));
                    }
                }

                // 5. 绘制辅助文本
                string textToRenderForAux = actualAuxText ?? config.Text; // 优先使用 actualAuxText

                if (!String.IsNullOrEmpty(textToRenderForAux))
                {
                    // Restore original simple defaults for config.Text if not specified in config
                    var textX = config.TextX ?? 50;
                    var textY = config.TextY ?? 55;
                    var textWidth = config.TextWidth ?? 14;
                    var textHeight = config.TextHeight ?? 14;
                    var textSize = config.TextSize ?? 14;
                    // 辅助文本颜色优先使用 config.TextColor，如果为空，默认为白色
                    var textColor = HexToBitmapColor(config.TextColor, BitmapColor.White);

                    // Simple boundary checks to prevent drawing outside the bitmap, if necessary
                    textX = Math.Max(0, Math.Min(textX, bitmapBuilder.Width - textWidth));
                    textY = Math.Max(0, Math.Min(textY, bitmapBuilder.Height - textHeight));
                    textWidth = Math.Max(1, Math.Min(textWidth, bitmapBuilder.Width - textX));
                    textHeight = Math.Max(1, Math.Min(textHeight, bitmapBuilder.Height - textY));

                    if (textWidth > 0 && textHeight > 0) 
                    {
                        bitmapBuilder.DrawText(text: textToRenderForAux, // 使用最终确定的文本 
                                            x: textX, y: textY, 
                                            width: textWidth, height: textHeight, 
                                            color: textColor, fontSize: textSize);
                    }
                }

                return bitmapBuilder.ToImage();
            }
        }
        
        // --- 辅助函数 ---

        /// <summary>
        /// 将十六进制颜色字符串转换为 BitmapColor。
        /// </summary>
        private static BitmapColor HexToBitmapColor(string hexColor, BitmapColor? defaultColor = null)
        {
            if (String.IsNullOrEmpty(hexColor))
                return defaultColor ?? BitmapColor.Black; // Default for background or if not specified

            if (!hexColor.StartsWith("#"))
                 return defaultColor ?? (defaultColor.HasValue ? defaultColor.Value : BitmapColor.Red); // Invalid format, return default or Red

            var hex = hexColor.Substring(1);
            if (hex.Length != 6 && hex.Length != 8)
                return defaultColor ?? BitmapColor.Red; // 错误颜色

            try
            {
                var r = (byte)Int32.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                var g = (byte)Int32.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                var b = (byte)Int32.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                if (hex.Length == 8)
                {
                    var a = (byte)Int32.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
                    return new BitmapColor(r, g, b, a);
                }
                return new BitmapColor(r, g, b);
            }
            catch
            {
                return defaultColor ?? BitmapColor.Red; // 解析错误颜色
            }
        }

        /// <summary>
        /// 根据标题长度自动获取按钮的字体大小 (源自 General_Button_Base)。
        /// </summary>
        private static int GetButtonFontSize(String title)
        {
            if (String.IsNullOrEmpty(title)) return 23;
            var totalLengthWithSpaces = title.Length;
            int effectiveLength;
            if (totalLengthWithSpaces <= 8)
            {
                effectiveLength = totalLengthWithSpaces;
            }
            else
            {
                var words = title.Split(' ');
                effectiveLength = words.Length > 0 ? words.Max(word => word.Length) : 0;
                if (effectiveLength == 0 && totalLengthWithSpaces > 0)
                {
                    effectiveLength = totalLengthWithSpaces; // Fallback if no words or only spaces
                }
            }
            return effectiveLength switch
            {
                1 => 38,
                2 => 33,
                3 => 31,
                4 => 26,
                5 => 23,
                6 => 22,
                7 => 20,
                8 => 18,
                9 => 17, // Added from observation of pattern
                10 => 16, // Added
                11 => 13, // Added
                _ => 12  // Default for very long strings, General_Button_Base had 18 as catch-all. Adjusted to be smaller for very long titles.
            };
        }

        /// <summary>
        /// 根据标题长度自动获取旋钮的字体大小 (源自 General_Dial_Base)。
        /// </summary>
        private static int GetDialFontSize(String title)
        {
            if (String.IsNullOrEmpty(title)) return 16;
            var totalLengthWithSpaces = title.Length;
            int effectiveLength;
            if (totalLengthWithSpaces <= 10) // General_Dial_Base logic
            {
                effectiveLength = totalLengthWithSpaces;
            }
            else
            {
                var words = title.Split(' ');
                effectiveLength = words.Length > 0 ? words.Max(word => word.Length) : 0;
                if (effectiveLength == 0 && totalLengthWithSpaces > 0)
                {
                    effectiveLength = totalLengthWithSpaces;
                }
            }
            return effectiveLength switch
            {
                1 => 26,
                2 => 23,
                3 => 21,
                4 => 19,
                5 => 17,
                >= 6 and <= 7 => 15, // Matches General_Dial_Base
                8 => 13,
                9 => 12,
                10 => 11,
                11 => 10, // Added from observation
                _ => 9 // Matches General_Dial_Base
            };
        }
    }
} 