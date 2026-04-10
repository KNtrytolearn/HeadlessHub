#!/bin/bash
# HeadlessHub Installation Script for RK3528

set -e

echo "╔══════════════════════════════════════════╗"
echo "║   HeadlessHub Installation for RK3528    ║"
echo "╚══════════════════════════════════════════╝"

INSTALL_DIR="/opt/headless-hub"
SERVICE_FILE="/etc/systemd/system/headless-hub.service"

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "[ERROR] Please run as root"
    exit 1
fi

echo "[INFO] Installing HeadlessHub..."

# Make executable
chmod +x "$INSTALL_DIR/headless-hub"

# Install systemd service
cp "$INSTALL_DIR/headless-hub.service" "$SERVICE_FILE"

# Reload systemd
systemctl daemon-reload

# Enable and start service
systemctl enable headless-hub
systemctl start headless-hub

echo ""
echo "✅ Installation complete!"
echo ""
echo "HeadlessHub is now running as a systemd service."
echo ""
echo "Commands:"
echo "  Check status:  systemctl status headless-hub"
echo "  View logs:     journalctl -u headless-hub -f"
echo "  Stop service:  systemctl stop headless-hub"
echo "  Restart:       systemctl restart headless-hub"
echo ""
echo "Access the Web UI at: http://$(hostname -I | awk '{print $1}'):5000"
echo ""
