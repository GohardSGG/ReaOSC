
namespace Loupedeck.ReaOSCPlugin.FX_Add 
{

using Loupedeck;

using Loupedeck.ReaOSCPlugin;

    public class Add_Dolby_Atmos_Beam : PluginDynamicCommand
    {
        public Add_Dolby_Atmos_Beam()
            : base(
                displayName: "Add Dolby Atmos Beam",
                description: "插入Dolby Atmos Beam (Fiedler Audio)效果器",
                groupName: "Fiedler Audio FX")
        {
            this.AddParameter("Add Dolby Atmos Beam", "Dolby Atmos Beam添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Fiedler_Audio/Dolby Atmos Beam", 1);
            PluginLog.Info("已触发Dolby Atmos Beam添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Beam",
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