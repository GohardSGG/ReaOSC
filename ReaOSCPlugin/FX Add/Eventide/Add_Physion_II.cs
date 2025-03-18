namespace Loupedeck.ReaOSCPlugin.FX_Add


{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Add_Physion_II : PluginDynamicCommand
    {
        public Add_Physion_II()
            : base(
                displayName: "Physion II",
                description: "插入Eventide Physion II效果器",
                groupName: "Eventide FX")
        {
            this.AddParameter("Add Physion II", "Physion II添加按钮", "FX Add");
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendFXMessage("Add/Eventide/Physion II", 1);
            PluginLog.Info("已触发Physion II添加请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Physion",
                    fontSize: 23,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "II",
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
