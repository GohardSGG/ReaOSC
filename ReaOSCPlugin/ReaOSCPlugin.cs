namespace Loupedeck.ReaOSCPlugin
{
    using System;
    using System.Text;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using WebSocketSharp;
    using Loupedeck;

    public class ReaOSCPlugin : Plugin
    {
        // === Loupedeck必需配置 ===
        public override bool UsesApplicationApiOnly => true;
        public override bool HasNoApplication => true;

        // === 单例实例 ===
        public static ReaOSCPlugin Instance { get; private set; }

        // === WebSocket配置 ===
        private const string WS_SERVER = "ws://localhost:9122";
        private WebSocket _wsClient;
        private bool _isReconnecting;

        // === 插件初始化 ===
        public ReaOSCPlugin()
        {
            // Initialize the plugin log.
            PluginLog.Init(this.Log);

            // Initialize the plugin resources.
            PluginResources.Init(this.Assembly);
            Instance = this;

            this.InitializeWebSocket();
        }

        /// <summary>
        /// 初始化WebSocket连接并配置事件监听
        /// </summary>
        private void InitializeWebSocket()
        {
            _wsClient = new WebSocket(WS_SERVER);

            // 连接成功事件处理
            _wsClient.OnOpen += (sender, e) =>
                PluginLog.Info("WebSocket连接成功");

            // 错误处理事件
            _wsClient.OnError += (sender, e) =>
                PluginLog.Error($"WebSocket错误: {e.Message}");

            // 连接关闭事件处理
            _wsClient.OnClose += (sender, e) =>
                PluginLog.Warning($"连接已关闭: {e.Reason}");

            try
            {
                _wsClient.Connect(); // 尝试建立连接
                PluginLog.Info("正在连接WebSocket服务器...");
            }
            catch (Exception ex)
            {
                PluginLog.Error($"连接失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 插件加载完成时触发
        /// </summary>
        public override void Load() => PluginLog.Info("插件已加载");

        /// <summary>
        /// 通用OSC消息发送方法
        /// </summary>
        /// <param name="category">消息类别（如FX/General）</param>
        /// <param name="address">目标地址</param>
        /// <param name="value">发送的数值</param>
        public static void SendGeneralMessage(string category, string address, int value)
        {
            // 检查WebSocket连接状态
            if (Instance?._wsClient?.IsAlive != true)
            {
                PluginLog.Error("连接未就绪");
                return;
            }

            var message = $"/{category}/{address}"; // 构建OSC消息路径
            try
            {
                Instance._wsClient.Send(message); // 发送消息
                PluginLog.Verbose($"已发送: {message}");
            }
            catch (Exception ex)
            {
                PluginLog.Error($"发送失败: {ex.Message}");
            }
        }

        // ========== 专用消息发送方法 ==========

        /// <summary>
        /// 发送FX效果器控制消息
        /// </summary>
        /// <param name="address">效果器参数地址</param>
        /// <param name="value">参数值</param>
        public static void SendFXMessage(string address, int value)
            => SendGeneralMessage("FX", address, value);

        /// <summary>
        /// 发送常规控制消息
        /// </summary>
        /// <param name="address">控制地址</param>
        /// <param name="value">控制值</param>
        public static void SendGeneralMessage(string address, int value)
            => SendGeneralMessage("General", address, value);

        /// <summary>
        /// 插件卸载时清理资源
        /// </summary>
        public override void Unload()
        {
            if (_wsClient?.IsAlive == true)
            {
                _wsClient.Close(CloseStatusCode.Normal); // 安全关闭连接
                PluginLog.Info("连接已正常关闭");
            }
            base.Unload();
        }
    }
}

