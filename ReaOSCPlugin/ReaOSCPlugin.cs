namespace Loupedeck.ReaOSCPlugin
{
    using System;
    using System.Text;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using WebSocketSharp;
    using Loupedeck;
    using Rug.Osc;
    using System.IO;
    using System.Net;

    public class ReaOSCPlugin : Plugin
    {
        // === Loupedeck插件接口实现 ===
        public override Boolean UsesApplicationApiOnly => true;
        public override Boolean HasNoApplication => true;

        // === 插件实例 ===
        public static ReaOSCPlugin Instance { get; private set; }

        // === WebSocket 连接相关 ===
        private const String WS_SERVER = "ws://localhost:9122";
        private WebSocket _wsClient;
        private Boolean _isReconnecting;

        // === 初始化 ===
        public ReaOSCPlugin()
        {
            Instance = this;
            PluginLog.Init(this.Log);
            PluginResources.Init(this.Assembly);
            this.InitializeWebSocket();
        }

        // === WebSocket连接与管理 ===
        private void InitializeWebSocket()
        {
            this._wsClient = new WebSocket(WS_SERVER);
            this._wsClient.OnMessage += this.OnWebSocketMessage;
            this.Connect();
        }

        private void OnWebSocketMessage(Object sender, MessageEventArgs e)
        {
            // 收到的可能是文本或OSC二进制消息
            if (e.IsBinary)
            {
                var (address, parsedValue, isValid) = this.ParseOSCMessage(e.RawData);
                if (isValid && !String.IsNullOrEmpty(address))
                {
                    // 状态变化通知相关
                    OSCStateManager.Instance.UpdateState(address, parsedValue);
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
                catch (Exception)
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
        private (String address, Object value, Boolean isValid) ParseOSCMessage(Byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                PluginLog.Warning("[ParseOSCMessage] Received empty or null data array.");
                return (null, null, false);
            }

            try
            {
                OscPacket packet = OscPacket.Read(data, 0, data.Length);

                if (packet is OscMessage message)
                {
                    String address = message.Address;
                    Object parsedArg = null;
                    Boolean argParsedSuccessfully = false;

                    if (message.Count > 0)
                    {
                        Object arg = message[0];
                        if (arg is String strVal)
                        {
                            parsedArg = strVal;
                            argParsedSuccessfully = true;
                            PluginLog.Verbose($"[ParseOSCMessage] Address '{address}': Received String arg '{strVal}'.");
                        }
                        else if (arg is Single singleVal)
                        {
                            parsedArg = singleVal;
                            argParsedSuccessfully = true;
                        }
                        else if (arg is Int32 intVal)
                        {
                            parsedArg = (Single)intVal;
                            argParsedSuccessfully = true;
                            PluginLog.Verbose($"[ParseOSCMessage] Address '{address}': Received Int32 arg {intVal}, converted to Single {parsedArg}.");
                        }
                        else if (arg is Double doubleVal)
                        {
                            parsedArg = (Single)doubleVal;
                            argParsedSuccessfully = true;
                            PluginLog.Verbose($"[ParseOSCMessage] Address '{address}': Received Double arg {doubleVal}, converted to Single {parsedArg}.");
                        }
                        else if (arg is Boolean boolVal)
                        {
                            parsedArg = boolVal ? 1.0f : 0.0f;
                            argParsedSuccessfully = true;
                            PluginLog.Verbose($"[ParseOSCMessage] Address '{address}': Received Boolean arg {boolVal}, converted to Single {parsedArg}.");
                        }
                        else
                        {
                            PluginLog.Warning($"[ParseOSCMessage] Address '{address}': First argument is not a supported type. Type: {arg?.GetType().Name}, Value: {arg?.ToString()}");
                        }
                    }
                    else
                    {
                        PluginLog.Warning($"[ParseOSCMessage] Address '{address}': Message has no arguments.");
                    }
                    
                    if(argParsedSuccessfully) {
                        return (address, parsedArg, true);
                    } else {
                        return (address, null, false);
                    }
                }
                else if (packet is OscBundle bundle)
                {
                    PluginLog.Info("[ParseOSCMessage] Received an OSC Bundle, attempting to process first message if available.");
                    if (bundle.Count > 0 && bundle[0] is OscMessage firstMessageInBundle)
                    {
                         if (firstMessageInBundle.Count > 0)
                         {
                             Object firstArgInBundle = firstMessageInBundle[0];
                             if (firstArgInBundle is String strValBundle)
                             {
                                 PluginLog.Info($"[ParseOSCMessage] Using first message from bundle: {firstMessageInBundle.Address} -> (String) '{strValBundle}'");
                                 return (firstMessageInBundle.Address, strValBundle, true);
                             }
                             else if (firstArgInBundle is Single floatValBundle)
                             {
                                 PluginLog.Info($"[ParseOSCMessage] Using first message from bundle: {firstMessageInBundle.Address} -> (Single) {floatValBundle}");
                                 return (firstMessageInBundle.Address, floatValBundle, true);
                             }
                             else if (firstArgInBundle is Int32 intValBundle)
                             {
                                 PluginLog.Info($"[ParseOSCMessage] Using first message from bundle: {firstMessageInBundle.Address} -> (Int32 to Single) {(Single)intValBundle}");
                                 return (firstMessageInBundle.Address, (Single)intValBundle, true);
                             }
                             // Add other type checks (Double, Boolean) for bundle message if needed
                         }
                    }
                    return (null, null, false);
                }
                else
                {
                    PluginLog.Warning($"[ParseOSCMessage] Received data that is not an OscMessage or OscBundle. Packet Type: {packet?.GetType().Name}");
                    return (null, null, false);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "[ParseOSCMessage] Exception while parsing OSC message with Rug.Osc.");
                return (null, null, false);
            }
        }

        // === 主动发送OSC消息 ===
        public static void SendOSCMessage(String address, Single value)
        {
            if (Instance?._wsClient?.IsAlive != true)
            {
                //PluginLog.Error("WebSocket连接未建立，发送OSC消息失败");
                return;
            }

            try
            {
                var oscData = CreateOSCMessage(address, value);
                Instance._wsClient.Send(oscData);

                PluginLog.Info($"发送OSC消息成功: {address} -> {value}");
            }
            catch (Exception ex)
            {
                PluginLog.Error($"发送OSC消息失败: {ex.Message}");
                Instance.ScheduleReconnect();
            }
        }

        public static void SendOSCMessage(String address, String value)
        {
            if (Instance?._wsClient?.IsAlive != true)
            {
                //PluginLog.Error("WebSocket连接未建立，发送OSC消息失败");
                return;
            }
            try
            {
                var oscData = CreateOSCMessage(address, value);
                Instance._wsClient.Send(oscData);
                // Corrected: Logging string value with proper escaping for quotes inside the interpolated string.
                PluginLog.Info($"发送OSC消息成功: {address} -> (String) \"{value?.Replace("\"", "\\\"")}\"");
            }
            catch (Exception ex)
            {
                PluginLog.Error($"发送OSC消息失败: {ex.Message}");
                Instance.ScheduleReconnect();
            }
        }

        private static void DebugStringChars(String prefix, String s)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"{prefix} => s.Length={s.Length}, chars:");
            for (Int32 i = 0; i < s.Length; i++)
            {
                sb.Append($" [i={i}]='\\u{(Int32)s[i]:X4}'");
            }
            PluginLog.Info(sb.ToString());
        }

        //  (地址 + float值) 封装成简单的OSC包格式
        private static Byte[] CreateOSCMessage(String address, Single value)
        {
            if (String.IsNullOrEmpty(address))
            {
                address = "/EmptyAddress"; 
            }

            try
            {
                OscMessage message = new OscMessage(address, value);
                
                // 直接调用 OscMessage 实例的 ToByteArray() 方法
                // 这个方法由 Rug.Osc 库提供，用于将消息正确序列化为符合OSC规范的字节数组
                return message.ToByteArray(); 
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[CreateOSCMessage] Error creating or serializing OSC message with Rug.Osc for address '{address}' and value {value}.");
                return new Byte[0]; 
            }
        }

        // 新增：创建包含字符串参数的OSC消息
        private static Byte[] CreateOSCMessage(String address, String value)
        {
            if (String.IsNullOrEmpty(address))
            {
                address = "/EmptyAddress";
            }
            if (value == null)
            {
                value = String.Empty;
            }

            try
            {
                OscMessage message = new OscMessage(address, value);
                return message.ToByteArray();
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[CreateOSCMessage] Error creating or serializing OSC message with Rug.Osc for address '{address}' and string value \"{value?.Replace("\"", "\\\"")}\".");
                return new Byte[0];
            }
        }

        private static void DebugLogHex(Byte[] data, String title)
        {
            var hex = BitConverter.ToString(data).Replace("-", " ");
            PluginLog.Info($"{title} ={data.Length}\n{hex}\n");
        }

        /// <summary>
        /// 插件加载时调用
        /// </summary>
        public override void Load() => PluginLog.Info("插件已加载");

        /// <summary>
        /// 发送一个 float 值通过OSC消息 (通用类别)
        /// </summary>
        public static void SendGeneralMessage(String category, String address, Int32 value)
        {
            if (Instance?._wsClient?.IsAlive != true)
            {
                PluginLog.Error("WebSocket未连接，无法发送通用消息");
                return;
            }

            // 拼接地址: /category/address
            var fullAddress = $"/{category}/{address}".Replace("//", "/");
            Single floatValue = value; // int隐式转float

            // OSC 封装
            var oscData = CreateOSCMessage(fullAddress, floatValue);

            // 通过WebSocket发送
            Instance._wsClient.Send(oscData);
            PluginLog.Verbose($"[SendGeneralMessage] 已发送: {fullAddress} -> {floatValue}");
        }

        // ========== 特定消息发送封装 ==========

        /// <summary>
        /// 发送FX效果消息 (原int value转为OSC float)
        /// </summary>
        public static void SendFXMessage(String address, Int32 value)
        {
            // 拼接地址: /FX/xxx
            var fullAddress = $"/FX/{address}".Replace("//", "/");
            // 将 int 转为 float, OSC标准需要 float
            Single floatValue = value;

            // 直接调用 SendOSCMessage 进行封装和发送
            SendOSCMessage(fullAddress, floatValue);
        }

        /// <summary>
        /// 发送通用消息 (默认分类 "General")
        /// </summary>
        /// <param name="address">OSC地址</param>
        /// <param name="value">整数值</param>
        public static void SendGeneralMessage(String address, Int32 value)
            => SendGeneralMessage("General", address, value);

        /// <summary>
        /// 卸载插件时释放资源
        /// </summary>
        public override void Unload()
        {
            if (this._wsClient?.IsAlive == true)
            {
                this._wsClient.Close(CloseStatusCode.Normal); // 安全关闭连接
                PluginLog.Info("WebSocket连接已关闭");
            }
            base.Unload();
        }

   
    }

    public sealed class OSCStateManager
    {
        private static readonly Lazy<OSCStateManager> _instance =
            new Lazy<OSCStateManager>(() => new OSCStateManager());
        public static OSCStateManager Instance => _instance.Value;

        // Corrected: _stateCache stores Object
        private readonly ConcurrentDictionary<String, Object> _stateCache =
            new ConcurrentDictionary<String, Object>();

        // Corrected: StateChangedEventArgs includes StringValue and IsString
        public class StateChangedEventArgs : EventArgs
        {
            public String Address { get; set; } 
            public Single Value { get; set; }    // Numeric OSC value (Single.NaN if string message)
            public String StringValue { get; set; } // String OSC value (null if numeric message)
            public Boolean IsString { get; set; }     // True if the primary value is a string
        }

        public event EventHandler<StateChangedEventArgs> StateChanged;

        // Corrected: UpdateState accepts Object and populates StateChangedEventArgs accordingly
        public void UpdateState(String address, Object value)
        {
            this._stateCache.AddOrUpdate(address, value, (k, oldValue) => value);

            String valueType = value?.GetType().Name ?? "null";
            String valueStringRepresentation = value?.ToString() ?? "null";
            if (value is String s) 
            {
                 valueStringRepresentation = $"\"{s.Replace("\"", "\\\"")}\""; // Escape quotes in string for log
            }

            PluginLog.Info($"[OSCStateManager] 状态更新: Address='{address}', Type='{valueType}', Value={valueStringRepresentation}");

            var eventArgs = new StateChangedEventArgs
            {
                Address = address
            };

            if (value is String strVal)
            {
                eventArgs.StringValue = strVal;
                eventArgs.Value = Single.NaN; 
                eventArgs.IsString = true;
            }
            else if (value is Single floatVal)
            {
                eventArgs.StringValue = null;
                eventArgs.Value = floatVal;
                eventArgs.IsString = false;
            }
            // Assuming other numeric types are converted to Single in ParseOSCMessage
            else 
            {
                eventArgs.StringValue = value?.ToString(); 
                eventArgs.Value = Single.NaN;
                eventArgs.IsString = (value is String); // Check again, though should be covered
            }
            this.StateChanged?.Invoke(this, eventArgs);
        }

        // Corrected: GetState returns Single, handles Object internally
        public Single GetState(String address)
        {
            if (this._stateCache.TryGetValue(address, out var storedValue))
            {
                if (storedValue is Single floatVal)
                {
                    return floatVal;
                }
                // If stored value is not a Single (e.g., it's a String), return a default float.
                // 0f is a common default, or Single.NaN if a more explicit "not-a-number" is needed.
                return 0f; 
            }
            return 0f; // Address not found
        }

        // Added: GetRawState
        public Object GetRawState(String address)
        {
            return this._stateCache.TryGetValue(address, out var value) ? value : null;
        }

        // Added: GetStringState
        public String GetStringState(String address)
        {
            if (this._stateCache.TryGetValue(address, out var storedValue) && storedValue is String strVal)
            {
                return strVal;
            }
            return null;
        }

        // Corrected: GetAllStates returns IDictionary<String, Object>
        public IDictionary<String, Object> GetAllStates()
        {
            return new Dictionary<String, Object>(this._stateCache);
        }
    }

}

