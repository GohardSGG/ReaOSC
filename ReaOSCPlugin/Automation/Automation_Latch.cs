namespace Loupedeck.ReaOSCPlugin.Automation
{

    using Loupedeck;
    using Loupedeck.ReaOSCPlugin.Automation;

    public class Automation_Latch : PluginDynamicCommand
    {
        public static string fullName = "Automation Latch";
        public static string chineseName = "自动化模式为Latch";
        public static string typeName = "Automation";
        public Automation_Latch()
            : base(
                displayName: fullName,
                description: chineseName,
                groupName: typeName)
        {
            this.AddParameter(fullName, chineseName + "按钮", typeName);
        }

        protected override void RunCommand(string actionParameter)
        {
            ReaOSCPlugin.SendGeneralMessage("Automation/Latch", 1);
            PluginLog.Info("已触发" + chineseName + "请求");
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            using (var bitmap = new BitmapBuilder(imageSize))
            {
                bitmap.Clear(BitmapColor.Black);
                bitmap.DrawText(
                    text: "Latch",
                    fontSize: 26,
                    color: new BitmapColor(236, 170, 122)
                );
                bitmap.DrawText(
                    text: "",
                    x:50,
                    y:55,
                    width:14,
                    height:14,
                    fontSize: 14,
                    color: new BitmapColor(140, 61, 3)
                );
                return bitmap.ToImage();
            }
        }
    }
}
