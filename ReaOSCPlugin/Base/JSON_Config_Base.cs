namespace Loupedeck.ReaOSCPlugin.Base
{
    public class ButtonConfig
    {
        public string DisplayName { get; set; }

        public string Title { get; set; }
        public string TitleColor { get; set; } // 可选，默认为白色

        // --- 次要文本 (所有属性均为可选) ---
        public string Text { get; set; }
        public string TextColor { get; set; }
        public int? TextSize { get; set; }
        public int? TextX { get; set; }
        public int? TextY { get; set; }
        public int? TextWidth { get; set; }
        public int? TextHeight { get; set; }
    }
}