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
    using System.Timers;

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
        private Boolean _isReconnecting = false;
        private System.Timers.Timer _reconnectTimer;
        private Boolean _isManuallyClosed = false;
        private const Int32 RECONNECT_DELAY_MS = 5000;

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
            PluginLog.Info("WebSocket: Attempting to initialize and connect...");
            this._isManuallyClosed = false;

            if (this._wsClient != null)
            {
                PluginLog.Info("WebSocket: Cleaning up existing WebSocket client before reinitialization.");
                this._wsClient.OnOpen -= this.OnWebSocketOpen;
                this._wsClient.OnMessage -= this.OnWebSocketMessage;
                this._wsClient.OnClose -= this.OnWebSocketClose;
                this._wsClient.OnError -= this.OnWebSocketError;

                if (this._wsClient.IsAlive)
                {
                    try
                    {
                        this._wsClient.Close(CloseStatusCode.Normal, "Reinitializing WebSocket");
                        PluginLog.Info("WebSocket: Old client closed during reinitialization.");
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error(ex, "WebSocket: Exception during old client Close() on reinitialization.");
                    }
                }
                this._wsClient = null;
                PluginLog.Info("WebSocket: Old client instance nullified.");
            }

            try
            {
                this._wsClient = new WebSocket(WS_SERVER);
                PluginLog.Info($"WebSocket: New client created for {WS_SERVER}.");

                this._wsClient.OnOpen += this.OnWebSocketOpen;
                this._wsClient.OnMessage += this.OnWebSocketMessage;
                this._wsClient.OnClose += this.OnWebSocketClose;
                this._wsClient.OnError += this.OnWebSocketError;
                PluginLog.Info("WebSocket: Event handlers subscribed.");

                this._wsClient.Connect();
                PluginLog.Info("WebSocket: Connection attempt initiated (Connect() called).");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "WebSocket: Exception during new WebSocket client creation or Connect() call in InitializeWebSocket.");
                this.ScheduleDelayedReconnect();
            }
        }

        private void OnWebSocketOpen(Object sender, EventArgs e)
        {
            PluginLog.Info("WebSocket: Connection opened successfully.");
            this._isReconnecting = false;
            if (this._reconnectTimer != null)
            {
                this._reconnectTimer.Stop();
                PluginLog.Info("WebSocket: Reconnect timer stopped due to successful connection.");
            }
        }

        private void OnWebSocketMessage(Object sender, MessageEventArgs e)
        {
            // 收到的可能是文本或OSC二进制消息
            if (e.IsBinary)
            {
                var (address, parsedValue, isValid) = this.ParseOSCMessage(e.RawData);
                if (isValid && !String.IsNullOrEmpty(address))
                {
                    if (Loupedeck.ReaOSCPlugin.Base.Logic_Manager_Base.Instance.IsAddressRelevant(address))
                    {
                        OSCStateManager.Instance.UpdateState(address, parsedValue);
                    }
                    else
                    {
                        PluginLog.Verbose($"[ReaOSCPlugin|OnWebSocketMessage] Discarding irrelevant OSC message for address: {address}");
                    }
                }
            }
        }

        private void OnWebSocketClose(Object sender, CloseEventArgs e)
        {
            PluginLog.Warning($"WebSocket: Connection closed. WasClean: {e.WasClean}, Code: {e.Code} ({((CloseStatusCode)e.Code).ToString()}), Reason: '{e.Reason}'");
            if (!this._isManuallyClosed)
            {
                PluginLog.Info("WebSocket: Connection closed unexpectedly. Scheduling reconnect...");
                this.ScheduleDelayedReconnect();
            }
            else
            {
                PluginLog.Info("WebSocket: Connection closed manually (or during reinitialization). No reconnect scheduled by this event.");
            }
        }

        private void OnWebSocketError(Object sender, WebSocketSharp.ErrorEventArgs e)
        {
            PluginLog.Error(e.Exception, $"WebSocket: Error occurred: {e.Message}");
            if (!this._isManuallyClosed)
            {
                PluginLog.Info("WebSocket: Error indicates potential connection issue. Scheduling reconnect.");
                this.ScheduleDelayedReconnect();
            }
            else
            {
                PluginLog.Info("WebSocket: Error occurred but manual close is flagged. No reconnect scheduled by this event.");
            }
        }

        private void ScheduleDelayedReconnect()
        {
            if (this._isManuallyClosed)
            {
                PluginLog.Info("WebSocket: Plugin is shutting down or connection was manually closed, skipping reconnect schedule.");
                return;
            }

            if (this._isReconnecting)
            {
                PluginLog.Info("WebSocket: Reconnect already in progress or scheduled by another event. Skipping duplicate schedule.");
                return;
            }

            this._isReconnecting = true;
            PluginLog.Info($"WebSocket: Scheduling reconnect attempt in {RECONNECT_DELAY_MS / 1000} seconds...");

            if (this._reconnectTimer == null)
            {
                this._reconnectTimer = new System.Timers.Timer(RECONNECT_DELAY_MS);
                this._reconnectTimer.Elapsed += this.OnReconnectTimerElapsed;
                this._reconnectTimer.AutoReset = false;
            }
            else
            {
                this._reconnectTimer.Interval = RECONNECT_DELAY_MS;
                this._reconnectTimer.Stop();
            }
            this._reconnectTimer.Start();
        }

        private void OnReconnectTimerElapsed(Object sender, System.Timers.ElapsedEventArgs e)
        {
            PluginLog.Info("WebSocket: Reconnect timer elapsed. Attempting to re-initialize WebSocket.");
            this._isReconnecting = false;
            this.InitializeWebSocket();
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
                PluginLog.Warning($"WebSocket: Connection not alive. Cannot send OSC: {address} -> {value}");
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
                PluginLog.Error(ex, $"发送OSC消息失败: {address} -> {value}");
                Instance.ScheduleDelayedReconnect();
            }
        }

        public static void SendOSCMessage(String address, String value)
        {
            String valueForLog = value != null ? value.Replace("\"", "\\\"") : "null"; // 为日志预处理，将引号转义为\"
            if (Instance?._wsClient?.IsAlive != true)
            {
                PluginLog.Warning($"WebSocket: Connection not alive. Cannot send OSC: {address} -> (String) \"{valueForLog}\"");
                return;
            }
            try
            {
                var oscData = CreateOSCMessage(address, value);
                Instance._wsClient.Send(oscData);
                PluginLog.Info($"发送OSC消息成功: {address} -> (String) \"{valueForLog}\"");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"发送OSC消息失败: {address} -> (String) \"{valueForLog}\"");
                Instance.ScheduleDelayedReconnect();
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
                String valueForLogEx = value != null ? value.Replace("\"", "\\\"") : "null";
                PluginLog.Error(ex, $"[CreateOSCMessage] Error creating or serializing OSC message with Rug.Osc for address '{address}' and string value \"{valueForLogEx}\".");
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
                PluginLog.Warning("WebSocket未连接，无法发送通用消息");
                return;
            }

            var fullAddress = $"/{category}/{address}".Replace("//", "/");
            Single floatValue = value;
            var oscData = CreateOSCMessage(fullAddress, floatValue);
            try
            {
                Instance._wsClient.Send(oscData);
                PluginLog.Verbose($"[SendGeneralMessage] 已发送: {fullAddress} -> {floatValue}");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"[SendGeneralMessage] 发送失败: {fullAddress} -> {floatValue}");
                Instance.ScheduleDelayedReconnect();
            }
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
            PluginLog.Info("WebSocket: Plugin Unload called. Cleaning up WebSocket resources.");
            this._isManuallyClosed = true;

            if (this._reconnectTimer != null)
            {
                this._reconnectTimer.Stop();
                this._reconnectTimer.Elapsed -= this.OnReconnectTimerElapsed;
                this._reconnectTimer.Dispose();
                this._reconnectTimer = null;
                PluginLog.Info("WebSocket: Reconnect timer stopped and disposed.");
            }

            if (this._wsClient != null)
            {
                this._wsClient.OnOpen -= this.OnWebSocketOpen;
                this._wsClient.OnMessage -= this.OnWebSocketMessage;
                this._wsClient.OnClose -= this.OnWebSocketClose;
                this._wsClient.OnError -= this.OnWebSocketError;
                PluginLog.Info("WebSocket: Event handlers unsubscribed for Unload.");

                if (this._wsClient.IsAlive)
                {
                    try
                    {
                        this._wsClient.Close(CloseStatusCode.Normal, "Plugin unloading");
                        PluginLog.Info("WebSocket: Connection closed on Unload.");
                    }
                    catch (Exception ex)
                    {
                        PluginLog.Error(ex, "WebSocket: Exception during Close() on Unload.");
                    }
                }
                else
                {
                    PluginLog.Info("WebSocket: Connection was not alive during Unload.");
                }
                this._wsClient = null;
                PluginLog.Info("WebSocket: Client instance nullified.");
            }
            else
            {
                PluginLog.Info("WebSocket: Client instance was already null on Unload.");
            }
            
            this._isReconnecting = false;

            base.Unload();
            PluginLog.Info("插件已成功卸载。");
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
                eventArgs.IsString = value is String; // Check again, though should be covered
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
        public Object GetRawState(String address) => 
            this._stateCache.TryGetValue(address, out var value) ? value : null;

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
        public IDictionary<String, Object> GetAllStates() => 
            new Dictionary<String, Object>(this._stateCache);
    }

}

