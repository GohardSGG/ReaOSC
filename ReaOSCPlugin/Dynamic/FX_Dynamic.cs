// 文件名: Dynamic/FX_Dynamic.cs
namespace Loupedeck.ReaOSCPlugin.Dynamic
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Loupedeck.ReaOSCPlugin.Base;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class FX_Dynamic : PluginDynamicFolder
    {
        // --- Member Variables ---
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

        public FX_Dynamic()
        {
            this.DisplayName = "FX Browser";
            this.GroupName = "Dynamic";

            this.LoadFxConfigs();

            // Brand options no longer include "All". "Favorite" is added first if it exists.
            this._brandOptions = new List<string>();
            if (this._favoriteFxNames.Any())
            {
                this._brandOptions.Add("Favorite");
            }

            var brandNames = this._allFxConfigs.Values.Select(c => c.GroupName).Distinct().OrderBy(name => name);
            this._brandOptions.AddRange(brandNames);

            // Default to the first available option (either "Favorite" or the first brand).
            this._currentBrandFilter = this._brandOptions.FirstOrDefault();

            this.UpdateFxList(); // Initial population of the action list
        }

        private void LoadFxConfigs()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Loupedeck.ReaOSCPlugin.Dynamic.FX_List.json";

            try
            {
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                using (var reader = new StreamReader(stream))
                {
                    string jsonContent = reader.ReadToEnd();

                    // 先解析为 JObject，避免一次性强制转换导致的类型冲突
                    JObject root = JObject.Parse(jsonContent);

                    // --- 1. 处理收藏列表 "Favorite" ---
                    if (root.TryGetValue("Favorite", out JToken favToken) && favToken is JArray favArray)
                    {
                        this._favoriteFxNames = favArray.ToObject<List<string>>();
                        root.Remove("Favorite"); // 删除，防止后续转换冲突
                    }

                    // --- 2. 其余厂商品牌转换为 ButtonConfig 集合 ---
                    var allData = root.ToObject<Dictionary<string, List<ButtonConfig>>>();

                    foreach (var group in allData)
                    {
                        string brand = group.Key;
                        foreach (var config in group.Value)
                        {
                            config.GroupName = brand; // 补充品牌名，后续过滤使用
                            string actionParameter = CreateActionParameter(config);
                            this._allFxConfigs[actionParameter] = config;
                        }
                    }
                }

                PluginLog.Info($"[FX_Dynamic] 成功加载并解析了 {this._allFxConfigs.Count} 个效果器配置和 {this._favoriteFxNames.Count} 个收藏。");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[FX_Dynamic] 加载或解析 '{resourceName}' 失败。");
            }
        }

        // This method now recalculates the filtered list of RAW action parameters and page counts.
        private void UpdateFxList()
        {
            IEnumerable<KeyValuePair<string, ButtonConfig>> filteredQuery;

            if (_currentBrandFilter == "Favorite")
            {
                // Favorite filtering. Match against DisplayName, not the full action parameter key.
                filteredQuery = this._allFxConfigs
                    .Where(kvp => this._favoriteFxNames.Contains(kvp.Value.DisplayName));
            }
            else
            {
                // Standard brand filtering.
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
            // CreateCommandName is now called here, at the last moment, which is safe.
            return this._currentFxActionNames
                .Skip(this._currentPage * 12)
                .Take(12)
                .Select(key => this.CreateCommandName(key));
        }

        // We define the encoder layout here. "Back" is now in the first slot.
        public override IEnumerable<string> GetEncoderRotateActionNames() => new[] 
        { 
            "Back", "Brand", "Page", 
            null, "Type", null 
        }.Select(name => this.CreateAdjustmentName(name));
        
        // The press action for "Back" is also moved to the first slot.
        public override IEnumerable<string> GetEncoderPressActionNames() => new string[]
        {
            this.CreateCommandName("Back"),
            null, 
            null, 
            null, 
            null, 
            null
        };

        // This is the modern, non-obsolete way to disable system-default navigation controls.
        public override PluginDynamicFolderNavigation GetNavigationArea(DeviceType _) => PluginDynamicFolderNavigation.None;

        public override void ApplyAdjustment(string actionParameter, int ticks)
        {
            var listChanged = false;
            var pageCountChanged = false;

            if (actionParameter == "Brand")
            {
                if (this._brandOptions.Count == 0)
                {
                    return;
                }
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
                this.AdjustmentValueChanged("Page"); // Explicitly notify that the page display needs a refresh.
            }
        }
        
        // When the "Back" encoder is pressed, this is called.
        // We "trick" the system by treating the press as a "zero-tick" rotation,
        // routing it to the single reliable entry point: ApplyAdjustment.
        public override void RunCommand(string actionParameter)
        {
            if (actionParameter == "Back")
            {
                this.ApplyAdjustment(actionParameter, 0);
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

        // --- LOW-LEVEL EVENT HANDLERS FOR MAXIMUM RELIABILITY ---

        // This handler will now manage the rotation of the "Back" encoder.
        public override Boolean ProcessEncoderEvent(String actionParameter, DeviceEncoderEvent encoderEvent)
        {
            if (actionParameter == "Back")
            {
                this.Close();
                return true;
            }
            return false;
        }

        // This handler continues to manage the press of the "Back" encoder.
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
            // Note: The system will provide a default image for NavigateUpActionName.
            // We only need to handle our custom effect buttons.
            if (!this._allFxConfigs.TryGetValue(actionParameter, out var config)) return null;

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                var currentBgColor = this._lastPressTimes.TryGetValue(actionParameter, out var pressTime) && (DateTime.Now - pressTime).TotalMilliseconds < 200
                    ? new BitmapColor(0x50, 0x50, 0x50)
                    : BitmapColor.Black;
                bitmapBuilder.Clear(currentBgColor);

                // --- 核心修复：如果 Title 为空，则使用 DisplayName 作为备用 ---
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

        // This override now ensures all encoder titles AND values have auto-sizing fonts.
        public override BitmapImage GetAdjustmentImage(string actionParameter, PluginImageSize imageSize)
        {
            if (String.IsNullOrEmpty(actionParameter))
            {
                return null;
            }

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                bitmapBuilder.Clear(BitmapColor.Black);

                // We get the value from our private helper to draw it ourselves.
                var value = this.GetValueForEncoder(actionParameter);

                // Case 1: No value to display (e.g., "Back"). Draw the title centered.
                if (String.IsNullOrEmpty(value))
                {
                    var titleFontSize = GetAutomaticTitleFontSize(actionParameter);
                    bitmapBuilder.DrawText(actionParameter, color: BitmapColor.White, fontSize: titleFontSize);
                }
                // Case 2: There is a value. Draw title on top, value on bottom.
                else
                {
                    // Draw title (top) - auto-sized
                    var titleFontSize = GetAutomaticTitleFontSize(actionParameter);
                    bitmapBuilder.DrawText(actionParameter, 0, 5, bitmapBuilder.Width, 30, BitmapColor.White, titleFontSize);

                    // Draw value (bottom) - auto-sized
                    var valueFontSize = GetAutomaticTitleFontSize(value);
                    bitmapBuilder.DrawText(value, 0, 35, bitmapBuilder.Width, bitmapBuilder.Height - 40, BitmapColor.White, valueFontSize);
                }
                
                return bitmapBuilder.ToImage();
            }
        }

        // We return null here to prevent the service from drawing its own default title.
        public override string GetAdjustmentDisplayName(string actionParameter, PluginImageSize imageSize) => null;

        // We return null here to prevent the service from drawing its own default value, avoiding the "double text" issue.
        public override string GetAdjustmentValue(string actionParameter) => null;
        
        // This is a private helper to get the data for our custom drawing logic.
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
            // 净化厂商名: "FabFilter FX" -> "FabFilter_FX"
            var vendorPart = SanitizeForOsc(config.GroupName);

            // 提取并净化效果器名: "Add Pro-C 2" -> "Pro_C_2"
            var effectPart = config.DisplayName.StartsWith("Add ") 
                ? SanitizeForOsc(config.DisplayName.Substring(4)) 
                : SanitizeForOsc(config.DisplayName);

            return $"/FX/Add/{vendorPart}/{effectPart}";
        }

        // 一个更强大的净化方法，将所有非字母数字替换为下划线
        private string SanitizeForOsc(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            // 将任何非字母、非数字的字符序列替换为单个下划线
            string sanitized = Regex.Replace(input, @"[^a-zA-Z0-9]+", "_");
            return sanitized.Trim('_');
        }

        private static int GetAutomaticButtonTitleFontSize(String title) { if (String.IsNullOrEmpty(title)) return 23; var len = title.Length; return len switch { 1 => 38, 2 => 33, 3 => 31, 4 => 26, 5 => 23, 6 => 22, 7 => 20, 8 => 18, _ => 16 }; }
        private int GetAutomaticTitleFontSize(String title) { if (String.IsNullOrEmpty(title)) return 23; var totalLengthWithSpaces = title.Length; int effectiveLength; if (totalLengthWithSpaces <= 8) { effectiveLength = totalLengthWithSpaces; } else { var words = title.Split(' '); effectiveLength = words.Length > 0 ? words.Max(word => word.Length) : 0; if (effectiveLength == 0 && totalLengthWithSpaces > 0) { effectiveLength = totalLengthWithSpaces; } } return effectiveLength switch { 1 => 16, 2 => 16, 3 => 16, 4 => 16, 5 => 15, 6 => 12, 7 => 11, 8 => 12, 9 => 11, 10 => 8, 11 => 7, _ => 6 }; }
        private static BitmapColor HexToBitmapColor(string hex) { if (String.IsNullOrEmpty(hex)) return BitmapColor.White; try { hex = hex.TrimStart('#'); var r = (byte)Int32.Parse(hex.Substring(0, 2), NumberStyles.HexNumber); var g = (byte)Int32.Parse(hex.Substring(2, 2), NumberStyles.HexNumber); var b = (byte)Int32.Parse(hex.Substring(4, 2), NumberStyles.HexNumber); return new BitmapColor(r, g, b); } catch { return BitmapColor.Red; } }
    }
} 