namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_DeReverb_Pro : PluginDynamicCommand
    {
        public Add_DeReverb_Pro()
            : base(
                displayName: "Add DeReverb Pro",
                description: "插入Waves Add DeReverb Pro效果器",
                groupName: "Waves FX")
        {
                this.AddParameter("Add DeReverb Pro", "Add DeReverb Pro添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Waves/DeReverb Pro", 1);
            PluginLog.Info("已触发Add DeReverb Pro添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "DeReverb",
                    fontSize: 18,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Pro",
                    x:55,
                    y:55,
                    width:14,
                    height:14,
                    fontSize: 14,
                    color: new BitmapColor(51, 112, 255)
                );
                return bitmap.ToImage();
            }
        }
    }
}
