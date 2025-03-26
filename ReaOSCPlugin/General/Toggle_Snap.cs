namespace Loupedeck.ReaOSCPlugin.General
{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin;

    public class Toggle_Snap : PluginDynamicAdjustment
    {
        public static string fullName = "Toggle Snap";
        public static string chineseName = "切换吸附";
        public static string typeName = "General";
        public Toggle_Snap()
            : base(
                displayName: fullName,
                description: chineseName,
                groupName: typeName,
                hasReset:false)
        {
            this.AddParameter(fullName, chineseName + "按钮", typeName);
        }

        // 处理旋钮旋转
        protected override void ApplyAdjustment(string actionParameter, int ticks)
        {


            if (ticks > 0)
            {
                ReaOSCPlugin.SendGeneralMessage("Toggle/Snap", 1);
            }
            else if (ticks < 0)
            {
                ReaOSCPlugin.SendGeneralMessage("Toggle/Snap", 0);
            }

            this.AdjustmentValueChanged(actionParameter);
        }
        protected override void RunCommand(string actionParameter)
        {
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Snap",
                    fontSize: 19,
                    color: BitmapColor.White
                );
                bitmap.DrawText(
                    text: "",
                    x:50,
                    y:55,
                    width:14,
                    height:14,
                    fontSize: 14,
                    color: new BitmapColor(136, 226, 255)
                );
                return bitmap.ToImage();
            }
        }
    }
}
