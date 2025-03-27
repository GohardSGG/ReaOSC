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
            Instance = this;
            PluginLog.Init(this.Log);
            PluginResources.Init(this.Assembly);
            this.InitializeWebSocket();
        }

        // === WebSocket连接管理 ===
        private void InitializeWebSocket()
        {
            this._wsClient = new WebSocket(WS_SERVER);
            this._wsClient.OnMessage += this.OnWebSocketMessage;
            this.Connect();
        }

        private void OnWebSocketMessage(object sender, MessageEventArgs e)
        {
            // 收到服务端推送的 OSC 二进制消息
            if (e.IsBinary)
            {
                var (address, value) = this.ParseOSCMessage(e.RawData);
                if (!string.IsNullOrEmpty(address))
                {
                    // 更新缓存并通知所有订阅者
                    OSCStateManager.Instance.UpdateState(address, value);
                }
            }
        }

        private void Connect()
        {
            if (this._wsClient?.IsAlive == true)
            {
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    this._wsClient?.Connect();
                    //PluginLog.Info("WebSocket连接成功");
                }
                catch (Exception ex)
                {
                    //PluginLog.Error($"连接失败: {ex.Message}");
                    this.ScheduleReconnect();
                }
            });
        }

        private void ScheduleReconnect()
        {
            if (this._isReconnecting)
            {
                return;
            }
            this._isReconnecting = true;

            Task.Run(async () =>
            {
                await Task.Delay(5000);
                //PluginLog.Info("尝试重新连接...");
                this._isReconnecting = false;
                this.InitializeWebSocket();
            });
        }

        // === OSC二进制消息解析 ===
        private (string address, float value) ParseOSCMessage(byte[] data)
        {
            try
            {
                int index = 0;

                // 解析地址部分（以null结尾的字符串）
                int addrEnd = Array.IndexOf(data, (byte)0, index);
                if (addrEnd < 0)
                    return (null, 0f);

                string address = Encoding.ASCII.GetString(data, 0, addrEnd);

                // 地址填充对齐到4字节
                index = (addrEnd + 4) & ~3;
                if (index + 4 > data.Length)
                    return (null, 0f); // 至少需要 ",f" + float

                // 检查类型标签是否为 ",f"
                string typeTag = Encoding.ASCII.GetString(data, index, 2);
                if (typeTag != ",f")
                    return (null, 0f);

                // 浮点数值偏移（类型标签后的填充）
                index += 4; // ",f" + 2字节填充
                if (index + 4 > data.Length)
                    return (null, 0f);

                // 读取大端序float
                byte[] floatBytes = new byte[4];
                Buffer.BlockCopy(data, index, floatBytes, 0, 4);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(floatBytes);
                }

                float value = BitConverter.ToSingle(floatBytes, 0);
                return (address, value);
            }
            catch (Exception ex)
            {
                //PluginLog.Error($"解析异常：{ex.Message}");
                return (null, 0f);
            }
        }

        // === 对外发送OSC消息（静态方法）===
        public static void SendOSCMessage(string address, float value)
        {
            if (Instance?._wsClient?.IsAlive != true)
            {
                //PluginLog.Error("WebSocket连接未就绪，发送失败");
                return;
            }

            try
            {
                var oscData = CreateOSCMessage(address, value);
                Instance._wsClient.Send(oscData);

                //PluginLog.Info($"发送OSC消息成功: {address} -> {value}");
            }
            catch (Exception ex)
            {
                //PluginLog.Error($"发送消息失败: {ex.Message}");
                Instance.ScheduleReconnect();
            }
        }

        // 将 (地址 + float数值) 封装成简单的OSC二进制格式
        private static byte[] CreateOSCMessage(string address, float value)
        {
            var addressBytes = Encoding.ASCII.GetBytes(address);
            int addressPad = (4 - (addressBytes.Length % 4)) % 4;
            var addressBuf = new byte[addressBytes.Length + addressPad];
            Buffer.BlockCopy(addressBytes, 0, addressBuf, 0, addressBytes.Length);

            // ,f
            var typeTagBytes = new byte[] { 0x2C, 0x66, 0x00, 0x00 };

            // float值要大端序
            var valueBytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(valueBytes);
            }

            var oscMessage = new byte[addressBuf.Length + typeTagBytes.Length + valueBytes.Length];
            Buffer.BlockCopy(addressBuf, 0, oscMessage, 0, addressBuf.Length);
            Buffer.BlockCopy(typeTagBytes, 0, oscMessage, addressBuf.Length, typeTagBytes.Length);
            Buffer.BlockCopy(valueBytes, 0, oscMessage, addressBuf.Length + typeTagBytes.Length, valueBytes.Length);

            // 这里没有打包成 bundle，直接发单条消息。依赖你的 OSC 服务器是否能接受单条消息。
            return oscMessage;
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

    public sealed class OSCStateManager
    {
        private static readonly Lazy<OSCStateManager> _instance =
            new Lazy<OSCStateManager>(() => new OSCStateManager());
        public static OSCStateManager Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, float> _stateCache =
            new ConcurrentDictionary<string, float>();

        public class StateChangedEventArgs : EventArgs
        {
            public string Address { get; set; }
            public float Value { get; set; }
        }

        // 当任意地址的状态更新时，会触发此事件
        public event EventHandler<StateChangedEventArgs> StateChanged;

        // 更新某地址的float数值，同时触发事件
        public void UpdateState(string address, float value)
        {
            this._stateCache.AddOrUpdate(address, value, (k, v) => value);

            PluginLog.Info($"[OSCStateManager] Update: {address} = {value}");
            this.StateChanged?.Invoke(this, new StateChangedEventArgs
            {
                Address = address,
                Value = value
            });
        }

        // 获取某地址当前值，若不存在则返回0
        public float GetState(string address) =>
            this._stateCache.TryGetValue(address, out var value) ? value : 0f;

        // 新增：获取所有状态的快照，用于遍历
        public IDictionary<string, float> GetAllStates()
        {
            // 返回一个拷贝，以免外部直接改动内部字典
            return new Dictionary<string, float>(this._stateCache);
        }
    }

}

