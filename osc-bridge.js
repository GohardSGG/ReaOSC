// [file name]: osc-bridge.js

// 在代码开头添加
const EventEmitter = require('events');
EventEmitter.defaultMaxListeners = 30; // 调整为适当值

const WebSocket = require('ws');
const express = require('express');
const dgram = require('dgram');
const fs = require('fs/promises');
const path = require('path');
const OSC = require('osc-js'); // 引入 osc-js

// ===================== 配置 =====================
const HTTP_PORT = 9122;               // WebSocket服务端口
const UDP_TARGET_PORT = 7878;         // 目标设备OSC端口 (发送给 REAPER 的主要控制端口)
const UDP_TARGET_PORT_EXTRA = 9223;   // 新增的额外转发UDP端口 (REAPER 监听此端口接收控制)
const UDP_LISTEN_PORT = 7879;         // 本地监听端口 (监听来自 REAPER 的主要反馈端口)
const UDP_REAPER_FEEDBACK_LISTEN_PORT = 9222; // 新增: REAPER脚本发送反馈的OSC监听端口
const PRESETS_DIR = 'C:/Web/Vue/Reaper Web/presets';
const TARGET_IP = '127.0.0.1';    // 目标设备IP

// ===================== 初始化 =====================
const app = express();
const udpClient = dgram.createSocket('udp4');  // 发送客户端 (用于将WS消息转发到REAPER)
const udpServer = dgram.createSocket('udp4');  // 接收服务端 (监听来自REAPER主要反馈端口7879)
const udpReaperFeedbackServer = dgram.createSocket('udp4'); // 新增: 监听来自REAPER脚本反馈端口9222
const osc = new OSC(); // 创建osc实例用于解析消息
let wsClientId = 0; // WebSocket客户端ID计数器

// 添加 CORS 支持
// 替换原有简单CORS配置
app.use((req, res, next) => {
  res.header('Access-Control-Allow-Origin', '*');
  res.header('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS');
  res.header('Access-Control-Allow-Headers', 'Origin, X-Requested-With, Content-Type, Accept');
  
  // 处理预检请求
  if (req.method === 'OPTIONS') {
    res.sendStatus(200);
  } else {
    next();
  }
});

//app.get('*', (req, res) => {
//  console.log('收到未处理请求:', req.originalUrl);
//  res.status(404).send('Not Found');
//});

app.use(express.json());
// ===================== UDP 服务 =====================
// 启动UDP监听 (来自 REAPER 主要反馈端口 7879)
udpServer.bind(UDP_LISTEN_PORT, () => {
  console.log(`🎧 UDP 监听已启动 (端口 ${UDP_LISTEN_PORT}) - 用于 REAPER 主要反馈`);
});

// 处理来自UDP_LISTEN_PORT (7879) 的OSC消息
udpServer.on('message', (msg, rinfo) => {
  try {
    const dataView = new DataView(msg.buffer, msg.byteOffset, msg.byteLength);
    const message = new OSC.Message();
    message.unpack(dataView);
    if (message.address) { 
      const argsString = message.args.map(arg => typeof arg === 'object' ? JSON.stringify(arg) : arg).join(', ');
      console.log(`← [UDP:${UDP_LISTEN_PORT}] 收到 OSC [${rinfo.address}:${rinfo.port}] | 地址: ${message.address} | 值: ${argsString}`);
    } else { 
      console.log(`← [UDP:${UDP_LISTEN_PORT}] 收到 OSC Bundle [${rinfo.address}:${rinfo.port}]`);
      message.packets.forEach((packet, i) => {
        const argsString = packet.args.map(arg => typeof arg === 'object' ? JSON.stringify(arg) : arg).join(', ');
        console.log(`  - 包 #${i + 1}: 地址: ${packet.address} | 值: ${argsString}`);
      });
    }
  } catch (error) {
    console.error(`← [UDP:${UDP_LISTEN_PORT}] [!] 收到无法解析的OSC消息 [${rinfo.address}:${rinfo.port}] (${msg.length}字节)`);
    console.error(`  - 错误详情: ${error.message}`);
  }

  let forwardedCount = 0;
  wss.clients.forEach(client => {
    if (client.readyState === WebSocket.OPEN) {
      client.send(msg);
      forwardedCount++;
    }
  });

  if (forwardedCount > 0) {
    console.log(`✅ 已将来自 [UDP:${UDP_LISTEN_PORT}] 的消息转发给 ${forwardedCount} 个WebSocket客户端`);
  }
});

// 新增: 启动UDP监听 (来自 REAPER 脚本反馈端口 9222)
udpReaperFeedbackServer.bind(UDP_REAPER_FEEDBACK_LISTEN_PORT, () => {
  console.log(`🎧 REAPER脚本反馈OSC监听已启动 (端口 ${UDP_REAPER_FEEDBACK_LISTEN_PORT})`);
});

// 新增: 处理来自UDP_REAPER_FEEDBACK_LISTEN_PORT (9222) 的OSC消息
udpReaperFeedbackServer.on('message', (msg, rinfo) => {
  try {
    const dataView = new DataView(msg.buffer, msg.byteOffset, msg.byteLength);
    const message = new OSC.Message(); 
    message.unpack(dataView);
    if (message.address) { 
      const argsString = message.args.map(arg => typeof arg === 'object' ? JSON.stringify(arg) : arg).join(', ');
      console.log(`← [REAPER脚本 UDP:${UDP_REAPER_FEEDBACK_LISTEN_PORT}] 收到 OSC [${rinfo.address}:${rinfo.port}] | 地址: ${message.address} | 值: ${argsString}`);
    } else { 
      console.log(`← [REAPER脚本 UDP:${UDP_REAPER_FEEDBACK_LISTEN_PORT}] 收到 OSC Bundle [${rinfo.address}:${rinfo.port}]`);
      message.packets.forEach((packet, i) => {
        const argsString = packet.args.map(arg => typeof arg === 'object' ? JSON.stringify(arg) : arg).join(', ');
        console.log(`  - 包 #${i + 1}: 地址: ${packet.address} | 值: ${argsString}`);
      });
    }
  } catch (error) {
    console.error(`← [REAPER脚本 UDP:${UDP_REAPER_FEEDBACK_LISTEN_PORT}] [!] 收到无法解析的OSC消息 [${rinfo.address}:${rinfo.port}] (${msg.length}字节)`);
    console.error(`  - 错误详情: ${error.message}`);
  }

  let forwardedCount = 0;
  wss.clients.forEach(client => {
    if (client.readyState === WebSocket.OPEN) {
      client.send(msg); 
      forwardedCount++;
    }
  });

  if (forwardedCount > 0) {
    console.log(`✅ 已将来自 [REAPER脚本 UDP:${UDP_REAPER_FEEDBACK_LISTEN_PORT}] 的消息转发给 ${forwardedCount} 个WebSocket客户端`);
  }
});

// ===================== WebSocket 服务 =====================
const server = app.listen(HTTP_PORT, '0.0.0.0', () => {
  console.log(`🌐 HTTP服务已启动，端口：${HTTP_PORT}`);
  console.log(`🎯 OSC 转发目标 (控制REAPER) 1：udp://${TARGET_IP}:${UDP_TARGET_PORT}`);
  console.log(`🎯 OSC 转发目标 (控制REAPER) 2：udp://${TARGET_IP}:${UDP_TARGET_PORT_EXTRA}`);
  console.log(`📁 预设存储路径：${PRESETS_DIR}`);
});

// WebSocket服务
const wss = new WebSocket.Server({ server });
wss.on('connection', (ws) => {
  ws.id = ++wsClientId;
  console.log(`📡 客户端 #${ws.id} 已连接`);

  ws.on('message', (msg) => {
    // 转发到目标UDP端口 (REAPER主要控制端口 7878)
    udpClient.send(msg, UDP_TARGET_PORT, TARGET_IP, (err) => {
      if (err) {
        console.error(`❌ [WS #${ws.id}] 转发到 ${UDP_TARGET_PORT} 失败:`, err);
        return;
      }
      try {
        const dataView = new DataView(msg.buffer, msg.byteOffset, msg.byteLength);
        const message = new OSC.Message();
        message.unpack(dataView);
        if (message.address) { 
          const argsString = message.args.map(arg => typeof arg === 'object' ? JSON.stringify(arg) : arg).join(', ');
          console.log(`→ [WS #${ws.id} -> UDP:${UDP_TARGET_PORT}] OSC | 地址: ${message.address} | 值: ${argsString}`);
        } else { 
          console.log(`→ [WS #${ws.id} -> UDP:${UDP_TARGET_PORT}] OSC Bundle`);
          message.packets.forEach((packet, i) => {
            const argsString = packet.args.map(arg => typeof arg === 'object' ? JSON.stringify(arg) : arg).join(', ');
            console.log(`  - 包 #${i + 1}: 地址: ${packet.address} | 值: ${argsString}`);
          });
        }
      } catch (error) {
        console.warn(`→ [WS #${ws.id} -> UDP:${UDP_TARGET_PORT}] 转发了无法解析的WS消息 (${msg.length}字节)`);
        console.warn(`  - 错误详情: ${error.message}`);
      }
    });

    // 额外转发到目标UDP端口 (REAPER脚本监听端口 9223)
    udpClient.send(msg, UDP_TARGET_PORT_EXTRA, TARGET_IP, (err) => {
      if (err) {
        console.error(`❌ [WS #${ws.id}] 转发到 ${UDP_TARGET_PORT_EXTRA} 失败:`, err);
        return;
      }
      try {
        const dataView = new DataView(msg.buffer, msg.byteOffset, msg.byteLength);
        const message = new OSC.Message();
        message.unpack(dataView);
        if (message.address) { 
          const argsString = message.args.map(arg => typeof arg === 'object' ? JSON.stringify(arg) : arg).join(', ');
          console.log(`→ [WS #${ws.id} -> UDP:${UDP_TARGET_PORT_EXTRA}] OSC | 地址: ${message.address} | 值: ${argsString}`);
        } else { 
          console.log(`→ [WS #${ws.id} -> UDP:${UDP_TARGET_PORT_EXTRA}] OSC Bundle`);
          message.packets.forEach((packet, i) => {
            const argsString = packet.args.map(arg => typeof arg === 'object' ? JSON.stringify(arg) : arg).join(', ');
            console.log(`  - 包 #${i + 1}: 地址: ${packet.address} | 值: ${argsString}`);
          });
        }
      } catch (error) {
        console.warn(`→ [WS #${ws.id} -> UDP:${UDP_TARGET_PORT_EXTRA}] 转发了无法解析的WS消息 (${msg.length}字节)`);
        console.warn(`  - 错误详情: ${error.message}`);
      }
    });
  });

  ws.on('close', () => console.log(`📡 客户端 #${ws.id} 断开连接`));
});

// 确保预设目录存在
async function ensurePresetsDir() {
  try {
    await fs.access(PRESETS_DIR);
    console.log(`✓ 预设目录已验证存在：${PRESETS_DIR}`);
  } catch {
    await fs.mkdir(PRESETS_DIR, { recursive: true });
    console.log(`📁 创建预设目录：${PRESETS_DIR}`);
  }
}


// 获取预设
app.get('/presets/:role', async (req, res) => {
  await ensurePresetsDir();
  const safeRole = req.params.role.replace(/[^a-zA-Z]/g, '');
  const filePath = path.join(PRESETS_DIR, `${safeRole}.json`);
  
  console.log(`🔍 尝试读取预设文件：${filePath}`);
  try {
    const data = await fs.readFile(filePath, 'utf8');
    console.log(`📤 发送预设：${safeRole}.json`);
    console.log(`📄 文件内容：${data}`); // 打印文件内容
    res.json(JSON.parse(data));
  } catch (err) {
    console.error(`⚠️ 预设不存在或读取失败：${safeRole} | 错误详情：${err.message}`);
    res.status(404).send('Preset not found');
  }
});

// 保存预设（修改正则允许数字）
app.post('/presets/:role', async (req, res) => {
  console.log('[🔵] 收到保存请求，角色:', req.params.role);
  const safeRole = req.params.role.replace(/[^a-zA-Z0-9 ]/g, '').replace(/ /g, '_');
  const filePath = path.join(PRESETS_DIR, `${safeRole}.json`);

  try {
    const data = JSON.stringify(req.body, null, 2);
    await fs.writeFile(filePath, data);
    console.log(`[✅] 预设保存成功：${safeRole}.json`);
    res.send('Preset saved');
  } catch (err) {
    console.error(`[❌] 保存失败：${err.message}`);
    res.status(500).send('Save failed');
  }
});

// ===================== 错误处理 =====================
process.on('uncaughtException', (err) => {
  console.error('‼️ 未捕获异常:', err.message);
});

udpServer.on('error', (err) => {
  console.error('‼️ UDP 服务错误 (端口 ' + UDP_LISTEN_PORT + '):', err.message);
});

udpReaperFeedbackServer.on('error', (err) => { // 新增错误处理
  console.error('‼️ REAPER 反馈 UDP 服务错误 (端口 ' + UDP_REAPER_FEEDBACK_LISTEN_PORT + '):', err.message);
});

udpClient.on('error', (err) => {
  console.error('‼️ UDP 客户端错误:', err.message);
});

// 启动时强制读取 Vocal.json 测试
(async () => {
  await ensurePresetsDir();
  console.log('✅ 服务初始化完成');
  console.log('├── WebSocket 端口:', HTTP_PORT);
  console.log('├── UDP 监听端口 (REAPER 主要反馈):', UDP_LISTEN_PORT);
  console.log('├── UDP 监听端口 (REAPER 脚本反馈):', UDP_REAPER_FEEDBACK_LISTEN_PORT); // 新增日志
  console.log('└── 预设存储路径:', PRESETS_DIR);
  const filePath = path.join(PRESETS_DIR, 'Vocal.json');
  try {
    const data = await fs.readFile(filePath, 'utf8');
    console.log(`[✅] 启动时成功读取 Vocal.json，内容：${data}`);
  } catch (err) {
    console.error(`[❌] 启动时读取 Vocal.json 失败：${err.message}`);
  }
})();