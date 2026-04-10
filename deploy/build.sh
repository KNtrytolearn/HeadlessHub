#!/bin/bash
# HeadlessHub Build and Deploy Script for RK3528 (ARM64 Linux)

set -e

echo "╔══════════════════════════════════════════╗"
echo "║   HeadlessHub Build Script for RK3528    ║"
echo "╚══════════════════════════════════════════╝"

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
OUTPUT_DIR="$PROJECT_DIR/publish"

echo "[INFO] Project directory: $PROJECT_DIR"
echo "[INFO] Output directory: $OUTPUT_DIR"

# Clean previous build
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Build for Linux ARM64 (RK3528)
echo "[INFO] Building for linux-arm64..."
dotnet publish "$PROJECT_DIR/HeadlessHub.csproj" \
    --configuration Release \
    --runtime linux-arm64 \
    --self-contained true \
    --output "$OUTPUT_DIR" \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -p:IncludeNativeLibrariesForSelfExtract=true

# Copy deployment files
cp "$PROJECT_DIR/deploy/headless-hub.service" "$OUTPUT_DIR/"
cp "$PROJECT_DIR/deploy/install.sh" "$OUTPUT_DIR/"

# Create data directory
mkdir -p "$OUTPUT_DIR/data"

echo ""
echo "✅ Build complete!"
echo ""
echo "Files ready in: $OUTPUT_DIR"
echo ""
echo "To deploy to RK3528:"
echo "  1. Copy the 'publish' directory to RK3528:"
echo "     scp -r $OUTPUT_DIR root@<RK3528-IP>:/opt/headless-hub"
echo ""
echo "  2. SSH into RK3528 and install:"
echo "     cd /opt/headless-hub"
echo "     chmod +x install.sh"
echo "     ./install.sh"
echo ""
