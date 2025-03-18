namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Vocal_Rider : PluginDynamicCommand
    {
        public Add_Vocal_Rider()
            : base(
                displayName: "Add Vocal Rider",
                description: "插入Waves Add Vocal Rider效果器",
                groupName: "Waves FX")
        {
                this.AddParameter("Add Vocal Rider", "Add Vocal Rider添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Waves/Vocal Rider", 1);
            PluginLog.Info("已触发Add Vocal Rider添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Vocal",
                    fontSize: 29,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "Rider",
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
