namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_ADT : PluginDynamicCommand
    {
        public Add_ADT()
            : base(
                displayName: "Add ADT",
                description: "插入Waves Add ADT效果器",
                groupName: "Waves FX")
        {
                this.AddParameter("Add ADT", "Add ADT添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Waves/ADT", 1);
            PluginLog.Info("已触发Add ADT添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "ADT",
                    fontSize: 33,
                    color: BitmapColor.White
                );
                return bitmap.ToImage();
            }
        }
    }
}
