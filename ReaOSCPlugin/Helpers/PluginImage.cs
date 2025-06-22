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
        /// 尝试加载与控件配置关联的图标。
        /// 会先检查 ButtonImage 属性，然后尝试根据 DisplayName 推断 "DisplayName.png"。
        /// </summary>
        /// <param name="config">控件的配置。</param>
        /// <param name="contextDisplayName">可选，用于日志记录的上下文名称。</param>
        /// <returns>加载的 BitmapImage，如果失败则返回 null。</returns>
        public static BitmapImage TryLoadIcon(ButtonConfig config, string contextDisplayName = null)
        {
            if (config == null)
            {
                PluginLog.Verbose($"[{contextDisplayName ?? "PluginImage"}|TryLoadIcon] 传入的 ButtonConfig 为空。");
                return null;
            }
            string imagePathToLoad = null;
            if (!String.IsNullOrEmpty(config.ButtonImage))
            {
                imagePathToLoad = config.ButtonImage;
                PluginLog.Verbose($"[{contextDisplayName ?? "PluginImage"}|TryLoadIcon] 尝试从 ButtonImage 字段 ('{config.ButtonImage}') 加载图标 for control '{config.DisplayName}'.");
            }
            else if (!string.IsNullOrEmpty(config.DisplayName))
            {
                imagePathToLoad = $"{config.DisplayName.Replace(" ", "_")}.png";
                PluginLog.Verbose($"[{contextDisplayName ?? "PluginImage"}|TryLoadIcon] ButtonImage为空，尝试根据 DisplayName ('{config.DisplayName}') 推断图标: '{imagePathToLoad}'.");
            }
            if (string.IsNullOrEmpty(imagePathToLoad))
            {
                PluginLog.Verbose($"[{contextDisplayName ?? "PluginImage"}|TryLoadIcon] 未能为控件 '{config.DisplayName}' 确定有效的图标路径。");
                return null;
            }
            try
            {
                BitmapImage icon = PluginResources.ReadImage(imagePathToLoad);
                if (icon == null)
                {
                    PluginLog.Verbose($"[{contextDisplayName ?? "PluginImage"}|TryLoadIcon] PluginResources.ReadImage 为路径 '{imagePathToLoad}' 返回了null (图标文件不存在或格式不支持)。");
                }
                else
                {
                    PluginLog.Info($"[{contextDisplayName ?? "PluginImage"}|TryLoadIcon] 成功加载图标 '{imagePathToLoad}' for control '{config.DisplayName}'.");
                }
                return icon;
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, $"[{contextDisplayName ?? "PluginImage"}|TryLoadIcon] 加载图标 '{imagePathToLoad}' 时发生异常 for control '{config.DisplayName}'.");
                return null;
            }
        }

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
        /// <param name="preferIconOnlyForDial">【新增】用于旋钮的纯图标模式</param>
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
            string actualAuxText = null,
            bool preferIconOnlyForDial = false)
        {
            if (config == null && customIcon == null) // 如果连配置和图标都没有，才绘制错误
            {
                using (var bb = new BitmapBuilder(imageSize))
                {
                    bb.Clear(BitmapColor.Black);
                    bb.DrawText("Cfg?", BitmapColor.Red, GetButtonFontSize("Cfg?"));
                    return bb.ToImage();
                }
            }
            
            // 情况：只有图标，没有配置 (例如插件直接提供一个纯图标按钮，config可省略)
            if (config == null && customIcon != null) 
            {
                using (var bb = new BitmapBuilder(imageSize))
                {
                    bb.Clear(BitmapColor.Black); // 或者一个可配置的默认背景
                    DrawScaledIcon(bb, customIcon, 0, 0, bb.Width, bb.Height, true);
                    return bb.ToImage();
                }
            }

            // 至此，config 必然不为 null
            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                // 1. 确定背景色
                BitmapColor currentBgColor = HexToBitmapColor(config.BackgroundColor, BitmapColor.Black);
                if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial")
                { currentBgColor = isActive ? HexToBitmapColor(config.ActiveColor, HexToBitmapColor(config.BackgroundColor, BitmapColor.Black)) : HexToBitmapColor(config.DeactiveColor, HexToBitmapColor(config.BackgroundColor, BitmapColor.Black)); }
                else if (config.ActionType == "TriggerButton" || config.ActionType == "CombineButton") 
                { if (isActive) { currentBgColor = HexToBitmapColor(config.ActiveColor, new BitmapColor(0x50, 0x50, 0x50)); } }
                else if (config.ActionType == "2ModeTickDial")
                { currentBgColor = currentMode == 1 ? HexToBitmapColor(config.BackgroundColor_Mode2, currentBgColor) : HexToBitmapColor(config.BackgroundColor, currentBgColor); }
                else if (config.ActionType == "SelectModeButton")
                { currentBgColor = isActive && !String.IsNullOrEmpty(config.ActiveColor) ? HexToBitmapColor(config.ActiveColor) : HexToBitmapColor(config.BackgroundColor, BitmapColor.Black); }
                bitmapBuilder.Clear(currentBgColor);

                // --- 图标优先绘制逻辑 ---
                if (customIcon != null && !forceTextOnly)
                {
                    bool isDial = config.ActionType?.Contains("Dial") ?? false;

                    if (isDial && preferIconOnlyForDial) // 【旋钮：纯图标模式】
                    {
                        DrawScaledIcon(bitmapBuilder, customIcon, 0, 0, bitmapBuilder.Width, bitmapBuilder.Height, true);
                    }
                    else // 【按钮：图标 + 下方文字模式】
                    {
                        int titleFontSizeForIcon = imageSize == PluginImageSize.Width90 ? 10 : 12;
                        int titleAreaHeight = titleFontSizeForIcon + (imageSize == PluginImageSize.Width90 ? 4 : 6); 
                        int topMargin = imageSize == PluginImageSize.Width90 ? 2 : 3;
                        int iconAreaHeight = bitmapBuilder.Height - titleAreaHeight - topMargin;
                        iconAreaHeight = Math.Max(10, iconAreaHeight); 
                        int iconAreaWidth = bitmapBuilder.Width - (imageSize == PluginImageSize.Width90 ? 4 : 6); 
                        
                        var (drawnIconWidth, drawnIconHeight) = DrawScaledIcon(bitmapBuilder, customIcon, (bitmapBuilder.Width - iconAreaWidth)/2, topMargin, iconAreaWidth, iconAreaHeight, true); // 确保DrawScaledIcon内部能正确居中于给定的areaX, areaY, areaWidth, areaHeight

                        string titleForIconMode = mainTitleOverride ?? config.Title ?? config.DisplayName;
                        if (!string.IsNullOrEmpty(titleForIconMode))
                        {
                            var textYUnderIcon = topMargin + drawnIconHeight + (imageSize == PluginImageSize.Width90 ? 1 : 2); 
                            var remainingHeightForTitle = bitmapBuilder.Height - textYUnderIcon - (imageSize == PluginImageSize.Width90 ? 1 : 2);
                            if (remainingHeightForTitle >= titleFontSizeForIcon) 
                            {
                                BitmapColor titleColorUnderIcon = GetTitleColor(config, isActive, currentMode);
                                // 【修正】为确保文字居中，我们需要让DrawText在整个宽度内绘制，并依赖其内部对齐
                                // 或者手动计算X。Loupedeck的DrawText在给定宽度时，默认行为是关键。
                                // 原始代码 x:0, width: bitmapBuilder.Width 是左对齐。
                                // 我们需要模拟 (bitmapBuilder.Width - text_actual_width) / 2
                                // 但 BitmapBuilder 没有直接的 MeasureText。所以我们使用整个宽度，并期望Loupedeck的绘制器能居中。
                                // 如果不行，则需要更复杂的文本宽度估算或API。
                                // 简单的居中尝试：将x设为0，width设为总宽度，并使用 Loupedeck.TextAlignment.Center (如果 BitmapBuilder 支持)
                                // 鉴于之前对齐枚举失败，我们将依赖 width 和 x 坐标。
                                // Loupedeck SDK通常在DrawText指定width时会内部居中。我们将x设为0，width设为总宽度。
                                bitmapBuilder.DrawText(text: titleForIconMode, 
                                                     x: 0, // 从最左边开始
                                                     y: textYUnderIcon, 
                                                     width: bitmapBuilder.Width, // 使用整个宽度
                                                     height: remainingHeightForTitle, 
                                                     fontSize: titleFontSizeForIcon, 
                                                     color: titleColorUnderIcon);
                            }
                        }
                    }
                }
                else // 【文本回退模式：恢复原始的文本绘制逻辑】
                {
                    // 此部分严格参考用户提供的原始 PluginImage.cs 的文本绘制逻辑
                    // (假设在2024-07-15 10:19:47.561 UTC消息附近获取到的版本)
                    BitmapColor currentTitleColor = GetTitleColor(config, isActive, currentMode);
                    string titleToDraw = mainTitleOverride ?? config.Title ?? config.DisplayName;
                    // 原始逻辑中，如果customIcon存在且!forceTextOnly，会先尝试绘制图标和其下小字标题
                    // 但此分支的条件是 customIcon == null || forceTextOnly == true，所以那部分不执行
                    // 直接进入原始的 (!iconDrawn) 分支逻辑:
                    
                    bool shouldShowConfigTitle = !(config.ShowTitle?.Equals("No", StringComparison.OrdinalIgnoreCase) == true);
                    if (!String.IsNullOrEmpty(valueText)) 
                    { 
                        var paramValueFontSize = GetParameterValueFontSize(valueText);
                        if (shouldShowConfigTitle && !String.IsNullOrEmpty(titleToDraw)) 
                        { 
                            var labelFontSize = GetDialFontSize(titleToDraw);
                            // 原始位置: y:5, height: bitmapBuilder.Height / 2 - 7 (标题)
                            //            y: bitmapBuilder.Height / 2 - 2, height: bitmapBuilder.Height / 2 - 3 (值)
                            bitmapBuilder.DrawText(titleToDraw, x: 0, y: 5, width: bitmapBuilder.Width, height: (bitmapBuilder.Height / 2) - 7, color: currentTitleColor, fontSize: labelFontSize );
                            bitmapBuilder.DrawText(valueText, x: 0, y: (bitmapBuilder.Height / 2) - 2, width: bitmapBuilder.Width, height: (bitmapBuilder.Height / 2) - 3, color: currentTitleColor, fontSize: paramValueFontSize);
                        }
                        else { bitmapBuilder.DrawText(valueText, currentTitleColor, paramValueFontSize); }
                    }
                    else if (!String.IsNullOrEmpty(titleToDraw)) 
                    { 
                        int titleFontSize = (config.ActionType != null && (config.ActionType.Contains("Dial"))) ? GetDialFontSize(titleToDraw) : GetButtonFontSize(titleToDraw);
                        bitmapBuilder.DrawText(titleToDraw, currentTitleColor, titleFontSize);
                    }

                    // 绘制辅助文本 (config.Text) - 原始逻辑的第5步
                    string textToRenderForAux = actualAuxText ?? config.Text;
                    if (!String.IsNullOrEmpty(textToRenderForAux))
                    {
                        var textX = config.TextX ?? 50;
                        var textY = config.TextY ?? 55;
                        var textWidth = config.TextWidth ?? 14;
                        var textHeight = config.TextHeight ?? 14;
                        var textSize = config.TextSize ?? 14; 
                        var auxTextColor = HexToBitmapColor(config.TextColor, BitmapColor.White);
                        textX = Math.Max(0, Math.Min(textX, bitmapBuilder.Width - textWidth));
                        textY = Math.Max(0, Math.Min(textY, bitmapBuilder.Height - textHeight));
                        textWidth = Math.Max(1, Math.Min(textWidth, bitmapBuilder.Width > textX ? bitmapBuilder.Width - textX : 1));
                        textHeight = Math.Max(1, Math.Min(textHeight, bitmapBuilder.Height > textY ? bitmapBuilder.Height - textY: 1));
                        if (textWidth > 0 && textHeight > 0) 
                        { bitmapBuilder.DrawText(text: textToRenderForAux, x: textX, y: textY, width: textWidth, height: textHeight, color: auxTextColor, fontSize: textSize); }
                    }
                }
                return bitmapBuilder.ToImage();
            }
        }
        
        /// <summary>
        /// 辅助方法：在指定区域内绘制图标，保持宽高比并缩放（如果需要）。
        /// </summary>
        /// <returns>返回实际绘制的图标尺寸 (width, height)。</returns>
        private static (int drawnWidth, int drawnHeight) DrawScaledIcon(BitmapBuilder bb, BitmapImage icon, int areaX, int areaY, int areaWidth, int areaHeight, bool centerInArea)
        {
            if (icon == null || areaWidth <= 0 || areaHeight <= 0) return (0,0);

            int originalIconWidth = icon.Width;
            int originalIconHeight = icon.Height;
            int drawnWidth = originalIconWidth;
            int drawnHeight = originalIconHeight;
            double scale = 1.0;

            // 计算缩放比例以适应区域，同时保持宽高比
            if (originalIconHeight > areaHeight) { scale = (double)areaHeight / originalIconHeight; }
            if (originalIconWidth * scale > areaWidth) { scale = Math.Min(scale, (double)areaWidth / originalIconWidth); } 
            else if (originalIconWidth > areaWidth && Math.Abs(scale - 1.0) < 0.001) { scale = (double)areaWidth / originalIconWidth; } // 如果未因高度缩放，但宽度超出

            drawnWidth = (int)(originalIconWidth * scale);
            drawnHeight = (int)(originalIconHeight * scale);
            drawnWidth = Math.Max(1, drawnWidth);
            drawnHeight = Math.Max(1, drawnHeight);

            int finalX = areaX;
            int finalY = areaY;
            if (centerInArea)
            {
                finalX = areaX + (areaWidth - drawnWidth) / 2;
                finalY = areaY + (areaHeight - drawnHeight) / 2;
            }
            
            bb.DrawImage(icon, finalX, finalY, drawnWidth, drawnHeight);
            return (drawnWidth, drawnHeight);
        }

        private static BitmapColor GetTitleColor(ButtonConfig config, bool isActive, int currentMode)
        {
            if (config == null) return BitmapColor.White;
            BitmapColor titleColor = HexToBitmapColor(config.TitleColor, BitmapColor.White);
            if (config.ActionType == "ToggleButton" || config.ActionType == "ToggleDial")
            { titleColor = isActive ? HexToBitmapColor(config.ActiveTextColor, BitmapColor.White) : HexToBitmapColor(config.DeactiveTextColor, BitmapColor.White); }
            else if (config.ActionType == "2ModeTickDial")
            { titleColor = currentMode == 1 ? HexToBitmapColor(config.TitleColor_Mode2, BitmapColor.White) : HexToBitmapColor(config.TitleColor, BitmapColor.White); }
            else if (config.ActionType == "SelectModeButton")
            { titleColor = isActive && !String.IsNullOrEmpty(config.TitleColor) ? HexToBitmapColor(config.TitleColor, BitmapColor.White) : HexToBitmapColor(config.DeactiveTextColor, BitmapColor.White); }
            return titleColor;
        }

        /// <summary>
        /// 将十六进制颜色字符串转换为 BitmapColor。
        /// </summary>
        private static BitmapColor HexToBitmapColor(string hexColor, BitmapColor? defaultColor = null)
        {
            if (String.IsNullOrEmpty(hexColor)) return defaultColor ?? BitmapColor.Black; 
            if (!hexColor.StartsWith("#")) return defaultColor ?? (defaultColor.HasValue ? defaultColor.Value : BitmapColor.Red); 
            var hex = hexColor.Substring(1);
            if (hex.Length != 6 && hex.Length != 8) return defaultColor ?? BitmapColor.Red; 
            try
            {
                var r = (byte)Int32.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
                var g = (byte)Int32.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                var b = (byte)Int32.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                if (hex.Length == 8) { var a = (byte)Int32.Parse(hex.Substring(6, 2), NumberStyles.HexNumber); return new BitmapColor(r, g, b, a); }
                return new BitmapColor(r, g, b);
            }
            catch { return defaultColor ?? BitmapColor.Red; }
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