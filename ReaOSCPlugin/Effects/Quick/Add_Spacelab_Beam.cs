
namespace Loupedeck.ReaOSCPlugin.Effects
{
    using Loupedeck;

using Loupedeck.ReaOSCPlugin;

    public class Add_Spacelab_Beam : PluginDynamicCommand
    {
        public Add_Spacelab_Beam()
            : base(
                displayName: "Add Spacelab Beam",
                description: "插入Spacelab Beam (Fiedler Audio)效果器",
                groupName: "Fiedler Audio FX")
        {
            this.AddParameter("Add Spacelab Beam", "Spacelab Beam添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Fiedler Audio/Spacelab Beam", 1);
            PluginLog.Info("已触发Spacelab Beam添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Spacelab",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Beam",
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