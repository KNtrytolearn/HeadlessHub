# HeadlessHub

无头设备版 darts-hub，专为 RK3528 (ARM64 Linux) 设计。

## 功能

- **Profile 管理**: 创建和管理不同的游戏配置
- **App 管理**: 下载、安装、启动和停止扩展应用
- **Web API**: 通过浏览器或 API 管理
- **实时日志**: WebSocket 推送运行日志

## 支持的扩展

| 扩展 | 功能 |
|------|------|
| darts-caller | 飞镖报分（语音播报） |
| darts-wled | WLED LED 灯带控制 |
| darts-pixelit | PixelIt 像素屏控制 |

## 构建和部署

### 在开发机上构建

```bash
cd HeadlessHub/deploy
chmod +x build.sh
./build.sh
```

### 部署到 RK3528

```bash
# 1. 复制到 RK3528
scp -r ../publish root@<RK3528-IP>:/opt/headless-hub

# 2. SSH 登录并安装
ssh root@<RK3528-IP>
cd /opt/headless-hub
chmod +x install.sh
./install.sh
```

## Web API

启动后访问: `http://<RK3528-IP>:5000`

### API 端点

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | /api/status | 系统状态 |
| GET | /api/profiles | 列出所有 Profile |
| POST | /api/profiles/{name}/start | 启动 Profile |
| POST | /api/profiles/{name}/stop | 停止 Profile |
| GET | /api/apps | 列出所有 App |
| POST | /api/apps/{name}/download | 下载 App |
| POST | /api/apps/{name}/run | 启动 App |
| POST | /api/apps/{name}/stop | 停止 App |
| PUT | /api/apps/{name}/config | 更新 App 配置 |
| WS | /ws/logs | 实时日志流 |

## 配置文件

配置文件存储在 `data/` 目录下：

- `apps-downloadable.json` - 可下载应用配置
- `profiles.json` - Profile 配置

## 硬件要求

- RK3528 开发板
- Debian 12 (ARM64)
- USB 音箱（用于 darts-caller）
- 网络连接（WLED/PixelIt 控制器在同一局域网）

## 开发

基于 [darts-hub](https://github.com/lbormann/darts-hub) 源码，复用了核心逻辑并移除了 Avalonia GUI 依赖。
