
namespace Loupedeck.ReaOSCPlugin.FX_Add
{
    using Loupedeck;

using Loupedeck.ReaOSCPlugin;

    public class Add_ReaSurroundPan : PluginDynamicCommand
    {
        public Add_ReaSurroundPan()
            : base(
                displayName: "Add ReaSurroundPan",
                description: "插入ReaSurroundPan (Cockos)效果器",
                groupName: "Cockos FX")
        {
            this.AddParameter("Add ReaSurroundPan", "ReaSurroundPan添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Cockos/ReaSurroundPan", 1);
            PluginLog.Info("已触发ReaSurroundPan添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "SurroundPan",
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