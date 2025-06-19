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

    public class FX_Dynamic : PluginDynamicFolder
    {
        // --- Member Variables ---
        private string _currentVendorFilter = "All";
        private string _currentEffectTypeFilter = "All";
        private readonly List<string> _vendorOptions = new List<string> { "All" };
        private readonly List<string> _effectTypeOptions = new List<string> { "All", "EQ", "Comp", "Reverb", "Delay", "Saturate" };
        private readonly Dictionary<string, DateTime> _lastPressTimes = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, ButtonConfig> _allFxConfigs = new Dictionary<string, ButtonConfig>();

        public FX_Dynamic()
        {
            this.DisplayName = "FX Browser";
            this.GroupName = "Dynamic";
            this.Navigation = PluginDynamicFolderNavigation.ButtonArea;
            this.LoadFxConfigs();

            var vendorNames = this._allFxConfigs.Values
                .Select(c => c.GroupName)
                .Distinct()
                .OrderBy(name => name);
            this._vendorOptions.AddRange(vendorNames);
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
                    var jsonContent = reader.ReadToEnd();
                    var groupedConfigs = JsonConvert.DeserializeObject<Dictionary<string, List<ButtonConfig>>>(jsonContent);

                    foreach (var group in groupedConfigs)
                    {
                        foreach (var config in group.Value)
                        {
                            config.GroupName = group.Key;
                            var actionParameter = CreateActionParameter(config);
                            this._allFxConfigs[actionParameter] = config;
                        }
                    }
                }
                 PluginLog.Info($"[FX_Dynamic] 成功加载并解析了 {this._allFxConfigs.Count} 个效果器配置。");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[FX_Dynamic] 加载或解析 '{resourceName}' 失败。");
            }
        }

        public override IEnumerable<string> GetButtonPressActionNames()
        {
            return this._allFxConfigs
                .Where(kvp => 
                    (_currentVendorFilter == "All" || kvp.Value.GroupName == _currentVendorFilter) &&
                    (_currentEffectTypeFilter == "All" /* TODO: Add EffectType filtering here */)
                )
                .Select(kvp => this.CreateCommandName(kvp.Key))
                .Take(12);
        }

        public override IEnumerable<string> GetEncoderRotateActionNames() => new[] { this.CreateAdjustmentName("Vendor"), this.CreateAdjustmentName("Type") };

        public override void ApplyAdjustment(string actionParameter, int ticks)
        {
            var listChanged = false;
            if (actionParameter == "Vendor")
            {
                var currentIndex = this._vendorOptions.IndexOf(this._currentVendorFilter);
                var newIndex = (currentIndex + ticks + this._vendorOptions.Count) % this._vendorOptions.Count;
                this._currentVendorFilter = this._vendorOptions[newIndex];
                listChanged = true;
            }
            else if (actionParameter == "Type")
            {
                var currentIndex = this._effectTypeOptions.IndexOf(this._currentEffectTypeFilter);
                var newIndex = (currentIndex + ticks + this._effectTypeOptions.Count) % this._effectTypeOptions.Count;
                this._currentEffectTypeFilter = this._effectTypeOptions[newIndex];
                listChanged = true;
            }

            if(listChanged)
            {
                this.ButtonActionNamesChanged();
                this.AdjustmentValueChanged(actionParameter);
            }
        }

        public override void RunCommand(string actionParameter)
        {
            if (this._allFxConfigs.TryGetValue(actionParameter, out var config))
            {
                string oscAddress = DetermineOscAddress(config);
                ReaOSCPlugin.SendOSCMessage(oscAddress, 1.0f);

                this._lastPressTimes[actionParameter] = DateTime.Now;
                this.ButtonActionNamesChanged();
                Task.Delay(200).ContinueWith(_ => this.ButtonActionNamesChanged());
            }
        }

        public override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            PluginLog.Info($"[FX_Dynamic] GetCommandImage called for: '{actionParameter}'");
            if (!this._allFxConfigs.TryGetValue(actionParameter, out var config)) return null;

            using (var bitmapBuilder = new BitmapBuilder(imageSize))
            {
                var currentBgColor = this._lastPressTimes.TryGetValue(actionParameter, out var pressTime) && (DateTime.Now - pressTime).TotalMilliseconds < 200
                    ? new BitmapColor(0x50, 0x50, 0x50)
                    : BitmapColor.Black;
                bitmapBuilder.Clear(currentBgColor);

                var titleColor = String.IsNullOrEmpty(config.TitleColor) ? BitmapColor.White : HexToBitmapColor(config.TitleColor);
                bitmapBuilder.DrawText(text: config.Title, fontSize: GetAutomaticButtonTitleFontSize(config.Title), color: titleColor);

                if (!String.IsNullOrEmpty(config.Text))
                {
                    var textColor = String.IsNullOrEmpty(config.TextColor) ? BitmapColor.White : HexToBitmapColor(config.TextColor);
                    bitmapBuilder.DrawText(text: config.Text, x: config.TextX ?? 35, y: config.TextY ?? 55, width: config.TextWidth ?? 14, height: config.TextHeight ?? 14, color: textColor, fontSize: config.TextSize ?? 14);
                }
                return bitmapBuilder.ToImage();
            }
        }

        public override string GetAdjustmentValue(string actionParameter)
        {
            if (actionParameter == "Vendor") return this._currentVendorFilter;
            if (actionParameter == "Type") return this._currentEffectTypeFilter;
            return null;
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
        private static BitmapColor HexToBitmapColor(string hex) { if (String.IsNullOrEmpty(hex)) return BitmapColor.White; try { hex = hex.TrimStart('#'); var r = (byte)Int32.Parse(hex.Substring(0, 2), NumberStyles.HexNumber); var g = (byte)Int32.Parse(hex.Substring(2, 2), NumberStyles.HexNumber); var b = (byte)Int32.Parse(hex.Substring(4, 2), NumberStyles.HexNumber); return new BitmapColor(r, g, b); } catch { return BitmapColor.Red; } }
    }
} 