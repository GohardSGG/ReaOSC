
namespace Loupedeck.ReaOSCPlugin.Effects
{
    using Loupedeck;

using Loupedeck.ReaOSCPlugin;

    public class Add_Kontakt_7 : PluginDynamicCommand
    {
        public Add_Kontakt_7()
            : base(
                displayName: "Add Kontakt 7",
                description: "插入Kontakt 7 (Native Instruments) (64 out)效果器",
                groupName: "Native Instruments FX")
        {
            this.AddParameter("Add Kontakt 7", "Kontakt 7添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Native Instruments/Kontakt 7", 1);
            PluginLog.Info("已触发Kontakt 7添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Kontakt",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "7",
                    x:50,
                    y:55,
                    width:14,
                    height:14,
                    fontSize: 14,
                    color: new BitmapColor(255, 226, 0)
                );
                return bitmap.ToImage();
            }
        }
    }
}