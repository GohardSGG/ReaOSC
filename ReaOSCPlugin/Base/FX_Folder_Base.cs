// 文件名: Base/FX_Folder_Base.cs
// 描述: 这是为效果器浏览器这类具有复杂过滤和分页逻辑的动态文件夹创建的基类。
// 它封装了品牌/类型过滤、分页、自定义UI绘制等所有通用逻辑。

namespace Loupedeck.ReaOSCPlugin.Base
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public abstract class FX_Folder_Base : PluginDynamicFolder
    {
        // --- Member Variables ---
        private readonly ButtonConfig _entryConfig;
        private string _currentBrandFilter = "Favorite";
        private string _currentEffectTypeFilter = "All";
        private int _currentPage = 0;
        private int _totalPages = 1;
        private readonly List<string> _brandOptions;
        private readonly List<string> _effectTypeOptions = new List<string> { "All", "EQ", "Comp", "Reverb", "Delay", "Saturate" };
        private readonly Dictionary<string, DateTime> _lastPressTimes = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, ButtonConfig> _allFxConfigs = new Dictionary<string, ButtonConfig>();
        private List<string> _currentFxActionNames = new List<string>();
        private List<string> _favoriteFxNames = new List<string>();
        private List<string> _originalBrandOrder = null; // 用于存储从JSON读取的原始品牌顺序

        public FX_Folder_Base()
        {
            var folderClassName = this.GetType().Name;
            var folderBaseName = folderClassName.Replace("_Dynamic", "").Replace("_", " ");

            this._entryConfig = Logic_Manager_Base.Instance.GetConfigByDisplayName("Dynamic", folderBaseName);

            if (this._entryConfig == null)
            {
                PluginLog.Error($"[FX_Folder_Base] Constructor: 未能在 Logic_Manager 中找到文件夹入口 '{folderBaseName}' 的配置项。");
                this.DisplayName = folderBaseName;
                this.GroupName = "Dynamic";
                this._brandOptions = new List<string>(); // 初始化 _brandOptions
                return;
            }

            this.DisplayName = this._entryConfig.DisplayName;
            this.GroupName = this._entryConfig.GroupName ?? "Dynamic";
            this._brandOptions = new List<string>(); // 初始化 _brandOptions

            var fxData = Logic_Manager_Base.Instance.GetFxData(this.DisplayName);
            if (fxData == null)
            {
                PluginLog.Error($"[FX_Folder_Base] Constructor for '{this.DisplayName}' could not retrieve its data from Logic_Manager. The corresponding _List.json file may have failed to load.");
                if (this._favoriteFxNames.Any()) { this._brandOptions.Add("Favorite"); } // 即使没有其他数据，也要处理Favorite
                this._currentBrandFilter = this._brandOptions.FirstOrDefault() ?? (this._favoriteFxNames.Any() ? "Favorite" : "All");
                return;
            }

            this.ParseFxData(fxData); // ParseFxData 会在需要时填充 _originalBrandOrder

            // 构建 _brandOptions 列表
            if (this._favoriteFxNames.Any())
            {
                this._brandOptions.Add("Favorite");
            }

            // 如果配置了 PreserveBrandOrderInJson 并且已记录原始顺序
            if (this._entryConfig.PreserveBrandOrderInJson && this._originalBrandOrder != null && this._originalBrandOrder.Any())
            {
                var validOrderedBrands = this._originalBrandOrder
                    .Where(brandName => brandName != "Favorite" && this._allFxConfigs.Values.Any(c => c.GroupName == brandName));
                this._brandOptions.AddRange(validOrderedBrands);

                // 添加任何在 _originalBrandOrder 中未提及但在 _allFxConfigs 中存在的品牌 (按字母顺序)
                var remainingBrands = this._allFxConfigs.Values
                    .Select(c => c.GroupName)
                    .Distinct()
                    .Where(brandName => brandName != "Favorite" && !this._brandOptions.Contains(brandName))
                    .OrderBy(name => name);
                this._brandOptions.AddRange(remainingBrands);
            }
            else
            {
                // 默认行为：按字母顺序排序
                var brandNames = this._allFxConfigs.Values
                    .Select(c => c.GroupName)
                    .Distinct()
                    .Where(brandName => brandName != "Favorite")
                    .OrderBy(name => name);
                this._brandOptions.AddRange(brandNames);
            }

            // 设置当前品牌过滤器的默认值
            if (!String.IsNullOrEmpty(this._currentBrandFilter) && this._brandOptions.Contains(this._currentBrandFilter))
            {
                // 保留之前的 _currentBrandFilter (如果它仍然有效)
            }
            else if (this._brandOptions.Any())
            {
                this._currentBrandFilter = this._brandOptions.First();
            }
            else if (this._favoriteFxNames.Any()) // 如果列表为空但有收藏
            {
                this._currentBrandFilter = "Favorite";
            }
            else // 如果完全没有品牌或收藏
            {
                this._currentBrandFilter = "All"; // 或其他合适的默认值
            }
            
            this.UpdateFxList();
        }

        private void ParseFxData(JObject root)
        {
            try
            {
                if (root.TryGetValue("Favorite", out JToken favToken) && favToken is JArray favArray)
                {
                    this._favoriteFxNames = favArray.ToObject<List<string>>() ?? new List<string>();
                }

                // 如果配置了 PreserveBrandOrderInJson，则记录原始的顶层键顺序
                if (this._entryConfig != null && this._entryConfig.PreserveBrandOrderInJson)
                {
                    this._originalBrandOrder = new List<string>();
                    foreach (var property in root.Properties())
                    {
                        // 我们只关心那些作为品牌/类别的键
                        // Favorite 单独处理，不加入原始顺序列表，它总是在前面(如果存在)
                        if (property.Name != "Favorite" && (property.Value is JObject || property.Value is JArray))
                        {
                            this._originalBrandOrder.Add(property.Name);
                        }
                    }
                    if (this._originalBrandOrder.Any())
                    {
                       PluginLog.Info($"[FX_Folder_Base] For '{this.DisplayName}', recorded original brand order: {string.Join(", ", this._originalBrandOrder)}");
                    }
                }

                // 从 root 中移除 Favorite，以免被当作普通品牌处理进 _allFxConfigs
                // 这一步必须在记录 _originalBrandOrder 之后，且在 ToObject<Dictionary...>() 之前
                if (root.Property("Favorite") != null)
                {
                    root.Remove("Favorite");
                }
                
                var allData = root.ToObject<Dictionary<string, List<ButtonConfig>>>();
                if (allData != null)
                {
                    foreach (var group in allData)
                    {
                        string brand = group.Key;
                        if (group.Value != null)
                        {
                            foreach (var config in group.Value)
                            {
                                config.GroupName = brand; 
                                string actionParameter = CreateActionParameter(config);
                                this._allFxConfigs[actionParameter] = config;
                            }
                        }
                    }
                }
                PluginLog.Info($"[FX_Folder_Base] Successfully parsed {this._allFxConfigs.Count} FX configs and {this._favoriteFxNames.Count} favorites for folder '{this.DisplayName}'.");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[FX_Folder_Base] Failed to parse FX data for folder '{this.DisplayName}'.");
            }
        }

        private void UpdateFxList()
        {
            IEnumerable<KeyValuePair<string, ButtonConfig>> filteredQuery;

            if (_currentBrandFilter == "Favorite")
            {
                filteredQuery = this._allFxConfigs
                    .Where(kvp => this._favoriteFxNames.Contains(kvp.Value.DisplayName));
            }
            else
            {
                filteredQuery = this._allFxConfigs
                    .Where(kvp => kvp.Value.GroupName == this._currentBrandFilter);
            }
            
            this._currentFxActionNames = filteredQuery
                .Select(kvp => kvp.Key)
                .ToList();
            
            this._totalPages = (int)Math.Ceiling(this._currentFxActionNames.Count / 12.0);
            if (this._totalPages == 0) this._totalPages = 1;
        }

        public override IEnumerable<string> GetButtonPressActionNames()
        {
            this._currentPage = Math.Min(this._currentPage, this._totalPages - 1);
            return this._currentFxActionNames
                .Skip(this._currentPage * 12)
                .Take(12)
                .Select(key => this.CreateCommandName(key));
        }

        public override IEnumerable<string> GetEncoderRotateActionNames() => new[] 
        { 
            "Back", "Brand", "Page", 
            null, "Type", null 
        }.Select(name => this.CreateAdjustmentName(name));
        
        public override IEnumerable<string> GetEncoderPressActionNames() => new string[]
        {
            this.CreateCommandName("Back"),
            null, 
            null, 
            null, 
            null, 
            null
        };

        public override PluginDynamicFolderNavigation GetNavigationArea(DeviceType _) => PluginDynamicFolderNavigation.None;

        public override void ApplyAdjustment(string actionParameter, int ticks)
        {
            var listChanged = false;
            var pageCountChanged = false;

            if (actionParameter == "Brand")
            {
                if (this._brandOptions.Count == 0) return;
                var currentIndex = this._brandOptions.IndexOf(this._currentBrandFilter);
                var newIndex = (currentIndex + ticks + this._brandOptions.Count) % this._brandOptions.Count;
                this._currentBrandFilter = this._brandOptions[newIndex];
                this._currentPage = 0;
                this.UpdateFxList();
                listChanged = true;
                pageCountChanged = true;
            }
            else if (actionParameter == "Type")
            {
                var currentIndex = this._effectTypeOptions.IndexOf(this._currentEffectTypeFilter);
                var newIndex = (currentIndex + ticks + this._effectTypeOptions.Count) % this._effectTypeOptions.Count;
                this._currentEffectTypeFilter = this._effectTypeOptions[newIndex];
                this._currentPage = 0;
                this.UpdateFxList();
                listChanged = true;
                pageCountChanged = true;
            }
            else if (actionParameter == "Page")
            {
                var newPage = this._currentPage + ticks;
                if (newPage >= 0 && newPage < this._totalPages)
                {
                    this._currentPage = newPage;
                    listChanged = true;
                }
            }
            else if (actionParameter == "Back")
            {
                this.Close();
                return;
            }

            if(listChanged)
            {
                this.ButtonActionNamesChanged();
                this.AdjustmentValueChanged(actionParameter);
            }
            if(pageCountChanged)
            {
                this.AdjustmentValueChanged("Page");
            }
        }
        
        public override void RunCommand(string actionParameter)
        {
            if (actionParameter == "Back")
            {
                this.ApplyAdjustment(actionParameter, 0);
                return;
            }

            if (this._allFxConfigs.TryGetValue(actionParameter, out var config))
            {
                string oscAddress = DetermineOscAddress(config);
                ReaOSCPlugin.SendOSCMessage(oscAddress, 1.0f);
                this._lastPressTimes[actionParameter] = DateTime.Now;
                this.ButtonActionNamesChanged();
                Task.Delay(200).ContinueWith(_ => this.ButtonActionNamesChanged());
            }
        }

        public override Boolean ProcessEncoderEvent(String actionParameter, DeviceEncoderEvent encoderEvent)
        {
            if (actionParameter == "Back")
            {
                this.Close();
                return true;
            }
            return false;
        }

        public override Boolean ProcessButtonEvent2(String actionParameter, DeviceButtonEvent2 buttonEvent)
        {
            if (actionParameter == "Back" && buttonEvent.EventType == DeviceButtonEventType.Press)
            {
                this.Close();
                return true;
            }
            return false;
        }

        public override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            if (!this._allFxConfigs.TryGetValue(actionParameter, out var config))
            {
                using (var bitmapBuilder = new BitmapBuilder(imageSize))
                {
                    bitmapBuilder.Clear(BitmapColor.Black);
                    bitmapBuilder.DrawText("?", color: BitmapColor.White);
                    return bitmapBuilder.ToImage();
                }
            }

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                var currentBgColor = this._lastPressTimes.TryGetValue(actionParameter, out var pressTime) && (DateTime.Now - pressTime).TotalMilliseconds < 200
                    ? new BitmapColor(0x50, 0x50, 0x50)
                    : BitmapColor.Black;
                bitmapBuilder.Clear(currentBgColor);

                var titleToDraw = !String.IsNullOrEmpty(config.Title) ? config.Title : config.DisplayName;
                var titleColor = String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor);
                bitmapBuilder.DrawText(text: titleToDraw, fontSize: GetAutomaticButtonTitleFontSize(titleToDraw), color: titleColor);

                if (!String.IsNullOrEmpty(config.Text))
                {
                    var textColor = String.IsNullOrEmpty(config.TextColor) ? BitmapColor.White : HexToBitmapColor(config.TextColor);
                    bitmapBuilder.DrawText(text: config.Text, x: config.TextX ?? 35, y: config.TextY ?? 55, width: config.TextWidth ?? 14, height: config.TextHeight ?? 14, color: textColor, fontSize: config.TextSize ?? 14);
                }
                return bitmapBuilder.ToImage();
            }
        }

        public override BitmapImage GetAdjustmentImage(string actionParameter, PluginImageSize imageSize)
        {
            if (String.IsNullOrEmpty(actionParameter)) return null;

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                bitmapBuilder.Clear(BitmapColor.Black);
                var value = this.GetValueForEncoder(actionParameter);

                if (String.IsNullOrEmpty(value))
                {
                    var titleFontSize = GetAutomaticTitleFontSize(actionParameter);
                    bitmapBuilder.DrawText(actionParameter, color: BitmapColor.White, fontSize: titleFontSize);
                }
                else
                {
                    var titleFontSize = GetAutomaticTitleFontSize(actionParameter);
                    bitmapBuilder.DrawText(actionParameter, 0, 5, bitmapBuilder.Width, 30, BitmapColor.White, titleFontSize);
                    var valueFontSize = GetAutomaticTitleFontSize(value);
                    bitmapBuilder.DrawText(value, 0, 35, bitmapBuilder.Width, bitmapBuilder.Height - 40, BitmapColor.White, valueFontSize);
                }
                return bitmapBuilder.ToImage();
            }
        }

        public override string GetAdjustmentDisplayName(string actionParameter, PluginImageSize imageSize) => null;
        public override string GetAdjustmentValue(string actionParameter) => null;
        
        private string GetValueForEncoder(string actionParameter)
        {
            switch(actionParameter)
            {
                case "Brand": return this._currentBrandFilter;
                case "Type": return this._currentEffectTypeFilter;
                case "Page": return $"{this._currentPage + 1} / {this._totalPages}";
                default: return null;
            }
        }

        private string CreateActionParameter(ButtonConfig config) => $"/{config.GroupName}/{config.DisplayName}".Replace(" ", "_").Replace("//", "/");
        
        private string DetermineOscAddress(ButtonConfig config)
        {
            var vendorPart = SanitizeForOsc(config.GroupName);
            var effectPart = config.DisplayName.StartsWith("Add ") 
                ? SanitizeForOsc(config.DisplayName.Substring(4)) 
                : SanitizeForOsc(config.DisplayName);
            return $"/FX/Add/{vendorPart}/{effectPart}";
        }

        private string SanitizeForOsc(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            string sanitized = Regex.Replace(input, @"[^a-zA-Z0-9]+", "_");
            return sanitized.Trim('_');
        }

        private static int GetAutomaticButtonTitleFontSize(String title) { if (String.IsNullOrEmpty(title)) return 23; var len = title.Length; return len switch { 1 => 38, 2 => 33, 3 => 31, 4 => 26, 5 => 23, 6 => 22, 7 => 20, 8 => 18, _ => 16 }; }
        private int GetAutomaticTitleFontSize(String title) { if (String.IsNullOrEmpty(title)) return 23; var totalLengthWithSpaces = title.Length; int effectiveLength; if (totalLengthWithSpaces <= 8) { effectiveLength = totalLengthWithSpaces; } else { var words = title.Split(' '); effectiveLength = words.Length > 0 ? words.Max(word => word.Length) : 0; if (effectiveLength == 0 && totalLengthWithSpaces > 0) { effectiveLength = totalLengthWithSpaces; } } return effectiveLength switch { 1 => 16, 2 => 16, 3 => 16, 4 => 16, 5 => 15, 6 => 12, 7 => 11, 8 => 12, 9 => 11, 10 => 8, 11 => 7, _ => 6 }; }
        private static BitmapColor HexToBitmapColor(string hex) { if (String.IsNullOrEmpty(hex)) return BitmapColor.White; try { hex = hex.TrimStart('#'); var r = (byte)Int32.Parse(hex.Substring(0, 2), NumberStyles.HexNumber); var g = (byte)Int32.Parse(hex.Substring(2, 2), NumberStyles.HexNumber); var b = (byte)Int32.Parse(hex.Substring(4, 2), NumberStyles.HexNumber); return new BitmapColor(r, g, b); } catch { return BitmapColor.Red; } }

        public override BitmapImage GetButtonImage(PluginImageSize imageSize)
        {
            if (this._entryConfig == null)
            {
                using (var bb = new BitmapBuilder(imageSize))
                {
                    bb.Clear(BitmapColor.Black);
                    bb.DrawText(this.DisplayName, BitmapColor.White, GetAutomaticButtonTitleFontSize(this.DisplayName));
                    return bb.ToImage();
                }
            }
            
            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                BitmapColor bgColor = !String.IsNullOrEmpty(this._entryConfig.BackgroundColor) ? HexToBitmapColor(this._entryConfig.BackgroundColor) : BitmapColor.Black;
                bitmapBuilder.Clear(bgColor);

                BitmapColor titleColor = !String.IsNullOrEmpty(this._entryConfig.TitleColor) ? HexToBitmapColor(this._entryConfig.TitleColor) : BitmapColor.White;
                string mainTitle = this._entryConfig.Title ?? this._entryConfig.DisplayName;

                if (!String.IsNullOrEmpty(mainTitle))
                {
                    bitmapBuilder.DrawText(mainTitle, titleColor, GetAutomaticButtonTitleFontSize(mainTitle));
                }
                if (!String.IsNullOrEmpty(this._entryConfig.Text))
                {
                    var textColor = String.IsNullOrEmpty(this._entryConfig.TextColor) ? HexToBitmapColor(this._entryConfig.TextColor) : BitmapColor.White;
                    bitmapBuilder.DrawText(this._entryConfig.Text, this._entryConfig.TextX ?? 50, this._entryConfig.TextY ?? 55, this._entryConfig.TextWidth ?? 14, this._entryConfig.TextHeight ?? 14, textColor, this._entryConfig.TextSize ?? 14);
                }
                return bitmapBuilder.ToImage();
            }
        }
    }
} 