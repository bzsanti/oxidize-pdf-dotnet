#!/bin/bash
set -e

# Build script for cross-compiling oxidize-pdf-ffi to all target platforms
# Usage: ./build-native.sh [target]
#   target: linux-x64, win-x64, osx-x64, all (default: all)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
NATIVE_DIR="$PROJECT_ROOT/native"
DOTNET_DIR="$PROJECT_ROOT/dotnet/OxidizePdf.NET"

# Color output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m' # No Color

log() {
    echo -e "${BLUE}[BUILD]${NC} $1"
}

success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

error() {
    echo -e "${RED}[ERROR]${NC} $1"
    exit 1
}

# Check if cargo is installed
if ! command -v cargo &> /dev/null; then
    error "cargo not found. Please install Rust: https://rustup.rs/"
fi

# Build for specific target
build_target() {
    local rust_target=$1
    local rid=$2
    local lib_name=$3

    log "Building for $rid (Rust target: $rust_target)"

    cd "$NATIVE_DIR"

    # Add target if not already installed
    if ! rustup target list --installed | grep -q "$rust_target"; then
        log "Adding Rust target: $rust_target"
        rustup target add "$rust_target"
    fi

    # Build with cargo
    cargo build --release --target "$rust_target"

    # Copy to dotnet runtimes folder
    local output_dir="$DOTNET_DIR/runtimes/$rid/native"
    mkdir -p "$output_dir"

    local source_file="$NATIVE_DIR/target/$rust_target/release/$lib_name"
    if [ ! -f "$source_file" ]; then
        error "Build failed: $source_file not found"
    fi

    cp "$source_file" "$output_dir/"
    success "Built $rid: $output_dir/$(basename $lib_name)"
}

# Build for linux-x64
build_linux() {
    build_target "x86_64-unknown-linux-gnu" "linux-x64" "liboxidize_pdf_ffi.so"
}

# Build for win-x64 (requires mingw-w64)
build_windows() {
    if ! rustup target list --installed | grep -q "x86_64-pc-windows-gnu"; then
        log "Installing windows target (requires mingw-w64)"
        rustup target add x86_64-pc-windows-gnu
    fi

    build_target "x86_64-pc-windows-gnu" "win-x64" "oxidize_pdf_ffi.dll"
}

# Build for osx-x64
build_osx() {
    build_target "x86_64-apple-darwin" "osx-x64" "liboxidize_pdf_ffi.dylib"
}

# Parse arguments
TARGET="${1:-all}"

case "$TARGET" in
    linux-x64)
        build_linux
        ;;
    win-x64)
        build_windows
        ;;
    osx-x64)
        build_osx
        ;;
    all)
        log "Building for all platforms..."

        # Build native platform first
        if [[ "$OSTYPE" == "linux-gnu"* ]]; then
            build_linux
            log "Cross-compiling for Windows and macOS requires additional setup"
            log "Use GitHub Actions or Docker for full cross-compilation"
        elif [[ "$OSTYPE" == "darwin"* ]]; then
            build_osx
            log "Cross-compiling for Linux and Windows requires additional setup"
            log "Use GitHub Actions or Docker for full cross-compilation"
        elif [[ "$OSTYPE" == "msys" ]] || [[ "$OSTYPE" == "cygwin" ]]; then
            build_windows
            log "Cross-compiling for Linux and macOS requires WSL2 or Docker"
        fi
        ;;
    *)
        error "Unknown target: $TARGET. Use: linux-x64, win-x64, osx-x64, or all"
        ;;
esac

success "Build complete!"
echo ""
log "Native libraries location:"
find "$DOTNET_DIR/runtimes" -name "*.so" -o -name "*.dll" -o -name "*.dylib" 2>/dev/null || echo "  (none built yet)"
