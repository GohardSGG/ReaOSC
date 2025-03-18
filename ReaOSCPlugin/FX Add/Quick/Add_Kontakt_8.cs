
namespace Loupedeck.ReaOSCPlugin.FX_Add
{
    using Loupedeck;

using Loupedeck.ReaOSCPlugin;

    public class Add_Kontakt_8 : PluginDynamicCommand
    {
        public Add_Kontakt_8()
            : base(
                displayName: "Add Kontakt 8",
                description: "插入Kontakt 8 (Native Instruments) (64 out)效果器",
                groupName: "Native Instruments FX")
        {
            this.AddParameter("Add Kontakt 8", "Kontakt 8添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Native Instruments/Kontakt 8", 1);
            PluginLog.Info("已触发Kontakt 8添加请求");
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
                    text: "8",
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