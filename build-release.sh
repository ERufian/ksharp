#!/usr/bin/env bash
# Cross-platform release build script for K3CSharp (bash version)
# Produces self-contained single-file binaries for Windows, Linux, and macOS

set -e

PROJECT_PATH="K3CSharp/K3CSharp.csproj"
SOLUTION_PATH="K3CSharp.sln"
OUTPUT_BASE="publish"
SKIP_TESTS=0
ZIP=0

while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-tests|-SkipTests) SKIP_TESTS=1; shift ;;
        --zip|-Zip) ZIP=1; shift ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

platforms=(
    "Win-x64:win-x64:exe:ksharp-win-x64"
    "Linux-x64:linux-x64::ksharp-linux-x64"
    "macOS-x64:osx-x64::ksharp-macos-x64"
    "macOS-arm64:osx-arm64::ksharp-macos-arm64"
)

echo "========================================"
echo "  K3CSharp Cross-Platform Release Build"
echo "========================================"

# Verify .NET SDK
if ! command -v dotnet &> /dev/null; then
    echo "FAIL: .NET SDK not found. Install from https://dotnet.microsoft.com/download"
    exit 1
fi

echo "Using .NET SDK: $(dotnet --version)"

# Clean
echo ""
echo "Step 1: Clean"
dotnet clean "$SOLUTION_PATH" -c Release -v quiet

# Restore
echo ""
echo "Step 2: Restore"
dotnet restore "$SOLUTION_PATH" --verbosity quiet

# Build
echo ""
echo "Step 3: Build Release"
dotnet build "$SOLUTION_PATH" -c Release --no-restore -v quiet

# Tests
if [ $SKIP_TESTS -eq 0 ]; then
    echo ""
    echo "Step 4: Run Tests"
    (cd K3CSharp.Tests && dotnet run --verbosity quiet)
    echo "  OK: Tests passed"
else
    echo "Skipping tests (--skip-tests specified)"
fi

# Publish
echo ""
echo "Step 5: Publish Cross-Platform Binaries"

for plat in "${platforms[@]}"; do
    IFS=':' read -r profile rid ext output_name <<< "$plat"

    echo ""
    echo -n "  Publishing $profile ($rid)..."

    publish_dir="$OUTPUT_BASE/$rid"
    if dotnet publish "$PROJECT_PATH" -c Release -p:PublishProfile="$profile" -o "$publish_dir"; then
        binary_name="K3CSharp${ext:+.$ext}"
        binary_path="$publish_dir/$binary_name"

        if [ -f "$binary_path" ]; then
            size_mb=$(du -m "$binary_path" | cut -f1)
            echo " OK -> $publish_dir (${size_mb} MB)"

            # Rename
            new_name="$output_name"
            [ -n "$ext" ] && new_name="$new_name.$ext"
            cp "$binary_path" "$publish_dir/$new_name"

            # Zip
            if [ $ZIP -eq 1 ]; then
                zip_name="$OUTPUT_BASE/$output_name.zip"
                (cd "$publish_dir" && zip -qr "../../$zip_name" .)
                echo "  Archive -> $zip_name"
            fi
        else
            echo " FAIL: Binary not found at $binary_path"
        fi
    else
        echo " FAIL: Publish failed for $profile"
    fi
done

echo ""
echo "========================================"
echo "  Build Complete"
echo "========================================"
echo "All binaries published to ./$OUTPUT_BASE/"
