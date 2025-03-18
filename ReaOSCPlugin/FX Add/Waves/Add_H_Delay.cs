namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_H_Delay : PluginDynamicCommand
    {
        public Add_H_Delay()
            : base(
                displayName: "Add H-Delay",
                description: "插入Waves Add H-Delay效果器",
                groupName: "Waves FX")
        {
                this.AddParameter("Add H-Delay", "Add H-Delay添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Waves/H-Delay", 1);
            PluginLog.Info("已触发Add H-Delay添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "H-Delay",
                    fontSize: 21,
                    color: BitmapColor.White
                );

                return bitmap.ToImage();
            }
        }
    }
}
