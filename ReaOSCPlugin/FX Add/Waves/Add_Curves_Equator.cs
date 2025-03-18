namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Curves_Equator : PluginDynamicCommand
    {
        public Add_Curves_Equator()
            : base(
                displayName: "Add Curves Equator",
                description: "插入Waves Add Curves Equator效果器",
                groupName: "Waves FX")
        {
                this.AddParameter("Add Curves Equator", "Add Curves Equator添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Waves/Curves Equator", 1);
            PluginLog.Info("已触发Add Curves Equator添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Curves Equator",
                    fontSize: 20,
                    color: BitmapColor.White
                );

                return bitmap.ToImage();
            }
        }
    }
}
