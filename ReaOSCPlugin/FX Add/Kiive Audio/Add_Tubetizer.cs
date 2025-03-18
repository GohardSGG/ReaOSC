namespace Loupedeck.ReaOSCPlugin.FX_Add

{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Tubetizer : PluginDynamicCommand
    {
        public Add_Tubetizer()
            : base(
                displayName: "Tubetizer",
                description: "插入Kiive Audio Tubetizer效果器",
                groupName: "Kiive Audio FX")
        {
            this.AddParameter("Add Tubetizer", "Tubetizer添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Kiive Audio/Tubetizer", 1);
            PluginLog.Info("已触发Tubetizer添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Tubetizer",
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
