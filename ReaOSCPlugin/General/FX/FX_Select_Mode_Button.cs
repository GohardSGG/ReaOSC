namespace Loupedeck.ReaOSCPlugin.General.FX
{
    using System;
    using System.Collections.Generic;

    using Loupedeck;

    public static class FX_State_Manager
    {
        public enum FXModeType { FX, Chain }

        private static FXModeType _currentMode = FXModeType.FX;
        public static FXModeType CurrentMode => _currentMode;

        private static Dictionary<(string btnId, FXModeType), bool> _toggleStates
            = new Dictionary<(string, FXModeType), bool>()
        {
            {("Bypass",  FXModeType.FX), false},
            {("Bypass",  FXModeType.Chain), false},
            {("Offline", FXModeType.FX), false},
            {("Offline", FXModeType.Chain), false},
            {("Parallel",FXModeType.FX), false},
            {("Parallel",FXModeType.Chain), false},
            {("Show",    FXModeType.FX), false},
            {("Show",    FXModeType.Chain), false},
        };

        public static event Action FXStateChanged;

        public static void ToggleMode()
        {
            _currentMode = (_currentMode == FXModeType.FX) ? FXModeType.Chain : FXModeType.FX;
            FXStateChanged?.Invoke();
        }

        public static bool GetToggleActive(string btnId)
            => _toggleStates[(btnId, _currentMode)];

        public static void SetToggleActive(string btnId, bool isActive)
        {
            _toggleStates[(btnId, _currentMode)] = isActive;
            FXStateChanged?.Invoke();
        }

        public static string GetToggleOscAddress(string btnId)
        {
            if (_currentMode == FXModeType.FX)
            {
                return btnId switch
                {
                    "Bypass" => "Bypass/FX",
                    "Offline" => "Offline/FX",
                    "Parallel" => "Parallel/FX",
                    "Copy" => "Copy/FX",
                    "Show" => "Show/FX",
                    _ => "FX_Unknown"
                };
            }
            else
            {
                return btnId switch
                {
                    "Bypass" => "Bypass/Chain",
                    "Offline" => "Offline/Chain",
                    "Parallel" => "Parallel/Chain",
                    "Copy" => "Copy/Chain",
                    "Show" => "Show/Chain",
                    _ => "Chain_Unknown"
                };
            }
        }
    }

    public class FX_Select_Mode_Button : PluginDynamicCommand
    {
        public FX_Select_Mode_Button()
            : base("FX/Chain Select", "切换FX Mode ↔ Chain Mode", "FX")
        {
            FX_State_Manager.FXStateChanged += this.ActionImageChanged;
            this.AddParameter("FX&Chain Mode Select", "FX&Chain Mode", "FX");
        }

        protected override void RunCommand(string actionParameter)
        {
            FX_State_Manager.ToggleMode();
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            var isFXMode = FX_State_Manager.CurrentMode == FX_State_Manager.FXModeType.FX;

            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(isFXMode ? BitmapColor.Black : BitmapColor.White);
                bitmap.DrawText(
                    isFXMode ? "FX" : "Chain",
                    fontSize: 23,
                    color: isFXMode ? BitmapColor.White : BitmapColor.Black
                );
                bitmap.DrawText(
    text: "Mode",
    x: 50,
    y: 55,
    width: 14,
    height: 14,
    fontSize: 14,
    color: new BitmapColor(136, 226, 255)
);
                return bitmap.ToImage();
            }
        }
    }
}
