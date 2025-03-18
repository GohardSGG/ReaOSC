
namespace Loupedeck.ReaOSCPlugin.FX_Add
{
    using Loupedeck;

using Loupedeck.ReaOSCPlugin;

    public class Add_ReaTune : PluginDynamicCommand
    {
        public Add_ReaTune()
            : base(
                displayName: "Add ReaTune",
                description: "插入ReaTune效果器",
                groupName: "Cockos FX")
        {
            this.AddParameter("Add ReaTune", "ReaTune添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Cockos/ReaTune", 1);
            PluginLog.Info("已触发ReaTune添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Tune",
                    fontSize: 26,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Rea",
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