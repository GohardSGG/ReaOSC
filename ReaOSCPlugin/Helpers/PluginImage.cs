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
                    int iconHeight = 46; 
                    int iconWidth = (customIcon.Height > 0) ? (customIcon.Width * iconHeight / customIcon.Height) : customIcon.Width;
                    iconWidth = Math.Max(1, iconWidth);
                    int iconX = (bitmapBuilder.Width - iconWidth) / 2;
                    int iconY = 8; 
                    bitmapBuilder.DrawImage(customIcon, iconX, iconY, iconWidth, iconHeight);
                    
                    // 【恢复】图标下方绘制主标题 (较小字号)
                    if (!String.IsNullOrEmpty(titleToDraw))
                    {
                        var titleFontSizeForIcon = 12; // 或者根据imageSize动态调整，但通常图标下文字较小
                        var textYUnderIcon = bitmapBuilder.Height - titleFontSizeForIcon - 10; // 尝试放在底部，需要足够空间
                         if (iconY + iconHeight < textYUnderIcon && textYUnderIcon > iconY + iconHeight - 5) // 确保文字在图标下方且有一定绘制空间
                         {
                            // 使用带有x,y,width,height的DrawText，以便控制绘制区域
                            bitmapBuilder.DrawText(text: titleToDraw, 
                                               x: 0, 
                                               y: textYUnderIcon, 
                                               width: bitmapBuilder.Width, 
                                               height: titleFontSizeForIcon + 5, // 给字体留一些垂直空间
                                               fontSize: titleFontSizeForIcon, 
                                               color: currentTitleColor);
                            PluginLog.Verbose($"[PluginImage] Drew title '{titleToDraw}' under icon.");
                         }
                         else
                         {
                            PluginLog.Verbose($"[PluginImage] Not enough space to draw title '{titleToDraw}' under icon, or title is empty.");
                         }
                    }
                    iconDrawn = true;
                }

                PluginLog.Verbose($"[PluginImage] After icon check: iconDrawn = {iconDrawn}, titleToDraw = '{titleToDraw}' (before main text draw logic for config: {config?.DisplayName})");

                if (!iconDrawn) 
                {
                    bool shouldShowConfigTitle = !(config?.ShowTitle?.Equals("No", StringComparison.OrdinalIgnoreCase) == true);

                    if (!String.IsNullOrEmpty(valueText)) 
                    {
                        var paramValueFontSize = GetParameterValueFontSize(valueText);
                        if (shouldShowConfigTitle && !String.IsNullOrEmpty(titleToDraw)) 
                        {
                            var labelFontSize = GetDialFontSize(titleToDraw); 
                            PluginLog.Verbose($"[PluginImage] NoIcon_DrawDial (Mode1): Title='{titleToDraw}' (Size:{labelFontSize}), Value='{valueText}' (Size:{paramValueFontSize}). ConfigShowTitle={shouldShowConfigTitle}");
                            
                            bitmapBuilder.DrawText(titleToDraw, 
                                               x: 0, y: 5, 
                                               width: bitmapBuilder.Width, height: bitmapBuilder.Height / 2 - 7, 
                                               color: currentTitleColor, 
                                               fontSize: labelFontSize 
                                               );
                            bitmapBuilder.DrawText(valueText, 
                                               x: 0, y: bitmapBuilder.Height / 2 - 2, 
                                               width: bitmapBuilder.Width, height: bitmapBuilder.Height / 2 - 3, 
                                               color: currentTitleColor, 
                                               fontSize: paramValueFontSize
                                               );
                        }
                        else 
                        {
                            PluginLog.Verbose($"[PluginImage] NoIcon_DrawDial (Mode2): ValueOnly='{valueText}' (Size:{paramValueFontSize}). ConfigShowTitle={shouldShowConfigTitle}");
                            bitmapBuilder.DrawText(valueText, currentTitleColor, paramValueFontSize);
                        }
                    }
                    else if (!String.IsNullOrEmpty(titleToDraw)) 
                    {
                        int titleFontSize;
                        if (config.ActionType != null && (config.ActionType.Contains("Dial"))) 
                        {
                            titleFontSize = GetDialFontSize(titleToDraw);
                        }
                        else 
                        {
                            titleFontSize = GetButtonFontSize(titleToDraw);
                        }
                        PluginLog.Verbose($"[PluginImage] NoIcon_DrawButton/PlaceholderTitle: Title='{titleToDraw}' (Size:{titleFontSize}). ActionType={config.ActionType}");
                        bitmapBuilder.DrawText(titleToDraw, currentTitleColor, titleFontSize);
                    }
                }

                // 5. 绘制辅助文本 (config.Text)
                // 这部分逻辑保持不变，它使用config中定义的TextX, TextY等绝对定位或默认定位
                string textToRenderForAux = actualAuxText ?? config.Text;
                if (!String.IsNullOrEmpty(textToRenderForAux))
                {
                    var textX = config.TextX ?? 50;
                    var textY = config.TextY ?? 55;
                    var textWidth = config.TextWidth ?? 14;
                    var textHeight = config.TextHeight ?? 14;
                    var textSize = config.TextSize ?? 14; // 默认辅助文本大小
                    var auxTextColor = HexToBitmapColor(config.TextColor, BitmapColor.White);

                    // 确保绘制在边界内
                    textX = Math.Max(0, Math.Min(textX, bitmapBuilder.Width - textWidth));
                    textY = Math.Max(0, Math.Min(textY, bitmapBuilder.Height - textHeight));
                    // 确保宽度和高度至少为1，防止DrawText错误
                    textWidth = Math.Max(1, Math.Min(textWidth, bitmapBuilder.Width > textX ? bitmapBuilder.Width - textX : 1));
                    textHeight = Math.Max(1, Math.Min(textHeight, bitmapBuilder.Height > textY ? bitmapBuilder.Height - textY: 1));

                    if (textWidth > 0 && textHeight > 0) 
                    {
                        bitmapBuilder.DrawText(text: textToRenderForAux, 
                                            x: textX, y: textY, 
                                            width: textWidth, height: textHeight, 
                                            color: auxTextColor, fontSize: textSize);
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

        // 【新增】获取参数值的字体大小
        private static int GetParameterValueFontSize(string parameterValue)
        {
            if (String.IsNullOrEmpty(parameterValue)) return 10; // 默认一个较小值以防空字符串
            var len = parameterValue.Length;
            return len switch
            {
                1 => 16,
                2 => 16,
                3 => 16,
                4 => 16,
                5 => 15,
                6 => 12,
                7 => 11,
                8 => 12, // 注意：按用户给定的规则，长度8比7和9大
                9 => 11,
                10 => 8,
                11 => 7,
                _ => 6
            };
        }
    }
} 