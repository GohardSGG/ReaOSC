// Envelope_Toggle_Width_And_Pitch.cs
namespace Loupedeck.ReaOSCPlugin.General.Automation
{
    using Loupedeck;

    public class Envelope_Toggle_Width_And_Pitch : PluginDynamicCommand
    {
        private const string TrackName = "Width";
        private const string TakeName = "Pitch";
        private const string TypeName = "Envelope";

        public Envelope_Toggle_Width_And_Pitch()
            : base(displayName: "Width/Pitch Toggle",
                  description: "宽度/音高包络切换",
                  groupName: TypeName)
        {
            Envelope_Select_Mode.ModeChanged += (s, e) => ActionImageChanged();
            this.AddParameter("wp_toggle", "Width/Pitch Toggle", TypeName);
        }

        protected override void RunCommand(string actionParameter)
        {
            var modePrefix = Envelope_Select_Mode.GetModePrefix();
            var paramName = modePrefix == "Track" ? "Width" : "Pitch";
            var path = $"Envelope/{modePrefix}/Toggle/{paramName}";
            ReaOSCPlugin.SendGeneralMessage(path, 1);
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);

                var BaseName = Envelope_Select_Mode.GetModePrefix() == "Track" ?
                    TrackName :
                    TakeName;

                bitmap.DrawText(
                    text: BaseName,
                    fontSize: 23,
                    color: BitmapColor.White
                );

                // 第二行文字
                bitmap.DrawText(
                    x: 50,
                    y: 55,
                    width: 14,
                    height: 14,
                    text: Envelope_Select_Mode.GetModePrefix(),
                    fontSize: 14,
                    color: new BitmapColor(136, 226, 255)
                );

                return bitmap.ToImage();
            }
        }
    }
}