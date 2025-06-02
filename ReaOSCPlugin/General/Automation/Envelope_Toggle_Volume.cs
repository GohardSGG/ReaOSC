// Envelope_Toggle_Volume.cs
namespace Loupedeck.ReaOSCPlugin.General.Automation
{
    using Loupedeck;

    public class Envelope_Toggle_Volume : PluginDynamicCommand
    {
        private const string BaseName = "Volume";
        private const string TypeName = "Envelope";

        public Envelope_Toggle_Volume()
            : base(displayName: $"{BaseName} Toggle",
                  description: "音量包络切换",
                  groupName: TypeName)
        {
            Envelope_Select_Mode.ModeChanged += (s, e) => ActionImageChanged();
            this.AddParameter("vol_toggle", $"{BaseName} Toggle", TypeName);
        }

        protected override void RunCommand(string actionParameter)
        {
            var modePrefix = Envelope_Select_Mode.GetModePrefix();
            var path = $"Envelope/{modePrefix}/Toggle/Volume";
            ReaOSCPlugin.SendGeneralMessage(path, 1);
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);

                // 第一行文字
                bitmap.DrawText(
                    text: BaseName,
                    fontSize: 22,
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