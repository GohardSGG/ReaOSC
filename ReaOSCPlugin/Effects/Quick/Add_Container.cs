
namespace Loupedeck.ReaOSCPlugin.Effects
{

using Loupedeck;
using Loupedeck.ReaOSCPlugin;

    public class Add_Container : PluginDynamicCommand
    {
        public Add_Container()
            : base(
                displayName: "Add Container",
                description: "插入Container效果器",
                groupName: "Cockos FX")
        {
            this.AddParameter("Add Container", "Container添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Cockos/Container", 1);
            PluginLog.Info("已触发Container添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Container",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "",
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