using Loupedeck;
using Loupedeck.ReaOSCPlugin.Render;

namespace Loupedeck.ReaOSCPlugin.Render
{
    public class Master_Button : PluginDynamicCommand
    {
        public Master_Button()
            : base(displayName: "Master Button", description: "Set Source=Master", groupName: "Render")
        {
            // 监听状态改变以刷新显示
            RenderStateManager.StateChanged += () => this.ActionImageChanged();
            // 注册参数
            this.AddParameter("Master Button", "Master", "Render");
        }

        protected override void RunCommand(string actionParameter)
        {
            // 按下 => Source=Master
            RenderStateManager.SetSource(RenderStateManager.SourceType.Master);
        }

        protected override BitmapImage GetCommandImage(string actionParameter, PluginImageSize imageSize)
        {
            var isActive = (RenderStateManager.Source == RenderStateManager.SourceType.Master);

            using (var bitmap = new BitmapBuilder(imageSize))
            {
                if (isActive)
                {
                    bitmap.Clear(BitmapColor.White);
                    bitmap.DrawText("Master", fontSize: 20, color: BitmapColor.Black);
                }
                else
                {
                    bitmap.Clear(BitmapColor.Black);
                    bitmap.DrawText("Master", fontSize: 20, color: BitmapColor.White);
                }
                return bitmap.ToImage();
            }
        }
    }
}
