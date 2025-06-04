// 文件名: DynamicFolders/Render_Dynamic.cs
namespace Loupedeck.ReaOSCPlugin.DynamicFolders
{
    using Loupedeck.ReaOSCPlugin.Base; // 引用我们创建的基类

    /// <summary>
    /// "Render" 动态文件夹。
    /// 所有按钮和旋钮的定义在 Render_List.json 中。
    /// 实际功能由 Dynamic_Folder_Base 和 Logic_Manager_Base 实现。
    /// </summary>
    public class Render_Dynamic : Dynamic_Folder_Base
    {
        // 构造函数会自动调用基类的构造函数。
        // 基类构造函数会根据类名 "Render_Dynamic" -> "Render"
        // 去 Dynamic_List.json 中查找名为 "Render" 的入口配置，
        // 并加载 "Render_List.json" 来填充文件夹内容。
        public Render_Dynamic() : base()
        {
            // 通常无需在此添加额外代码，除非有 Render 文件夹特有的初始化逻辑。
        }
    }
}