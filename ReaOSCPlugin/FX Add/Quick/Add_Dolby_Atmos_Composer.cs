
namespace Loupedeck.ReaOSCPlugin.FX_Add
{
    using Loupedeck;

    using Loupedeck.ReaOSCPlugin;


    public class Add_Dolby_Atmos_Composer : PluginDynamicCommand
    {
        public Add_Dolby_Atmos_Composer()
            : base(
                displayName: "Add Dolby Atmos Composer",
                description: "插入Dolby Atmos Composer (Fiedler Audio)效果器",
                groupName: "Fiedler Audio FX")
        {
            this.AddParameter("Add Dolby Atmos Composer", "Dolby Atmos Composer添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Fiedler_Audio/Dolby Atmos Composer/", 1);
            PluginLog.Info("已触发Dolby Atmos Composer添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Composer",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Dolby",
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