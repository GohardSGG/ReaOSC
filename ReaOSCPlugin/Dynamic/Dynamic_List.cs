// 文件名: Dynamic/Dynamic_List.cs
// 描述: 此文件包含所有简单的、由JSON配置驱动的动态文件夹类。
// 它们的存在是为了被Loupedeck SDK的反射机制发现并注册。
// 这种方式可以减少项目中的空文件数量，使结构更整洁。
// 【修改】所有类现在继承自新的 General_Folder_Base

namespace Loupedeck.ReaOSCPlugin.Dynamic
{
    using Loupedeck.ReaOSCPlugin.Base; // 确保引用包含 General_Folder_Base

    /// <summary>
    /// "添加轨道" 动态文件夹。
    /// 所有功能都由 General_Folder_Base 基类实现。
    /// 这个类只需要存在并继承基类即可。
    /// </summary>
    public class Add_Track_Dynamic : General_Folder_Base // 修改基类
    {
        // 无需任何代码
    }

    /// <summary>
    /// "Render" 动态文件夹。
    /// 所有按钮和旋钮的定义在 Render_List.json 中。
    /// 实际功能由 General_Folder_Base 和 Logic_Manager_Base 实现。
    /// </summary>
    public class Render_Dynamic : General_Folder_Base // 修改基类
    {
        // 构造函数会自动调用基类的构造函数。
        // 基类构造函数会根据类名 "Render_Dynamic" -> "Render"
        // 去 Dynamic_List.json 中查找名为 "Render" 的入口配置，
        // 并加载 "Render_List.json" 来填充文件夹内容。
        public Render_Dynamic() : base() // 调用基类构造函数
        {
            // 通常无需在此添加额外代码，除非有 Render 文件夹特有的初始化逻辑。
        }
    }

    /// <summary>
    /// "Effect" 动态文件夹。
    /// </summary>
    public class Effect_Dynamic : General_Folder_Base // 修改基类
    {
        // 所有逻辑均由 General_Folder_Base 处理。
        // 此类的存在是为了被Loupedeck SDK的反射机制发现。
        // 基类会根据此类名 "Effect_Dynamic" -> "Effect"
        // 来加载对应的 "Effect_List.json" 配置文件。
    }

    /// <summary>
    /// "Instrument" 动态文件夹。
    /// </summary>
    public class Instrument_Dynamic : General_Folder_Base // 修改基类
    {
        // 所有逻辑均由 General_Folder_Base 处理。
        // 此类的存在是为了被Loupedeck SDK的反射机制发现。
        // 基类会根据此类名 "Instrument_Dynamic" -> "Instrument"
        // 来加载对应的 "Instrument_List.json" 配置文件。
    }

    /// <summary>
    /// "Track Name" 动态文件夹。
    /// </summary>
    public class Track_Name_Dynamic : General_Folder_Base // 修改基类
    {
        // 所有逻辑均由 General_Folder_Base 处理。
        // 此类的存在是为了被Loupedeck SDK的反射机制发现。
        // 基类会根据此类名 "Track_Name_Dynamic" -> "Track_Name"
        // 来加载对应的 "Track_Name_List.json" 配置文件。
    }

    /// <summary>
    /// "Track Routing" 动态文件夹。
    /// </summary>
    public class Track_Routing_Dynamic : General_Folder_Base // 修改基类
    {
        // 所有逻辑均由 General_Folder_Base 处理。
        // 基类会根据此类名 "Track_Routing_Dynamic" -> "Track Routing"
        // 来加载对应的 "Track_Routing_List.json" 配置文件。
        public Track_Routing_Dynamic() : base() { } // 调用基类构造函数
    }

    /// <summary>
    /// "Input Output" 动态文件夹。
    /// </summary>
    public class Track_Dynamic : General_Folder_Base // 修改基类
    {
        // 所有逻辑均由 General_Folder_Base 处理。
        // 基类会根据此类名 "Input_Output_Dynamic" -> "Input Output"
        // 来加载对应的 "Input_Output_List.json" 配置文件。
        public Track_Dynamic() : base() { } // 调用基类构造函数
    }
} 