namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Immersive_Wrapper : PluginDynamicCommand
    {
        public Add_Immersive_Wrapper()
            : base(
                displayName: "Add Immersive Wrapper",
                description: "插入Waves Add Immersive Wrapper效果器",
                groupName: "Waves FX")
        {
                this.AddParameter("Add Immersive Wrapper", "Add Immersive Wrapper添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Waves/Immersive Wrapper", 1);
            PluginLog.Info("已触发Add Immersive Wrapper添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Immersive Wrapper",
                    fontSize: 16,
                    color: BitmapColor.White
                );

                return bitmap.ToImage();
            }
        }
    }
}
