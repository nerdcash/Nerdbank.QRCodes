[CmdletBinding()]
param (
    [parameter()]
    [string]$Configuration = 'release'
)

$repoRoot = Resolve-Path "$PSScriptRoot/.."
$version = dotnet nbgv get-version -p $repoRoot/src/nerdbank-qrcodes -v SimpleVersion
$plist = Get-Content $PSScriptRoot/Info.plist
$plist = $plist.Replace('$version$', $version)
$IntermediatePlistPath = "$repoRoot/obj/Info.plist"
Set-Content -Path $IntermediatePlistPath -Value $plist -Encoding utf8NoBOM
if ($IsMacOS) {
    plutil -convert binary1 $IntermediatePlistPath
    chmod +x $IntermediatePlistPath
}
else {
    Write-Warning "Skipped plutil invocation because this is not macOS."
}

# copy Info.plist and the binary into the appropriate .framework directory structure
# so that when NativeBindings.targets references it with ResolvedFileToPublish, it will be treated appropriately.
$RustTargetBaseDir = "$repoRoot/src/nerdbank-qrcodes/target"
$RustDylibFileName = "libnerdbank_qrcodes.dylib"
$DeviceRustOutput = "$RustTargetBaseDir/aarch64-apple-ios/$Configuration/$RustDylibFileName"
$SimulatorX64RustOutput = "$RustTargetBaseDir/x86_64-apple-ios/$Configuration/$RustDylibFileName"
$SimulatorArm64RustOutput = "$RustTargetBaseDir/aarch64-apple-ios-sim/$Configuration/$RustDylibFileName"

$DeviceFrameworkDir = "$repoRoot/bin/$Configuration/device/nerdbank_qrcodes.framework"
$SimulatorFrameworkDir = "$repoRoot/bin/$Configuration/simulator/nerdbank_qrcodes.framework"
New-Item -Path $DeviceFrameworkDir,$SimulatorFrameworkDir -ItemType Directory -Force | Out-Null

Write-Host "Preparing Apple iOS and iOS-simulator frameworks"

Copy-Item $IntermediatePlistPath "$DeviceFrameworkDir/Info.plist"
Copy-Item $IntermediatePlistPath "$SimulatorFrameworkDir/Info.plist"
Write-Host "Created Info.plist with version $version"

if ($IsMacOS) {
    # Rename the binary that contains the arm64 architecture for device.
    lipo -create -output $DeviceFrameworkDir/nerdbank_qrcodes $DeviceRustOutput
    install_name_tool -id "@rpath/nerdbank_qrcodes.framework/nerdbank_qrcodes" "$DeviceFrameworkDir/nerdbank_qrcodes"
    chmod +x "$DeviceFrameworkDir/nerdbank_qrcodes"

    # Create a universal binary that contains both arm64 and x64 architectures for simulator.
    lipo -create -output $SimulatorFrameworkDir/nerdbank_qrcodes $SimulatorX64RustOutput $SimulatorArm64RustOutput
    install_name_tool -id "@rpath/nerdbank_qrcodes.framework/nerdbank_qrcodes" "$SimulatorFrameworkDir/nerdbank_qrcodes"
    chmod +x "$SimulatorFrameworkDir/nerdbank_qrcodes"
}
else {
    Copy-Item $SimulatorArm64RustOutput "$SimulatorArm64RustOutput/nerdbank_qrcodes"
    Copy-Item $DeviceRustOutput "$DeviceFrameworkDir/nerdbank_qrcodes"
    Write-Warning "Skipped critical steps because this is not macOS."
}
Write-Host "Copied nerdbank_qrcodes to framework"

# Build the xcframework
$xcframeworkOutputDir = "$repoRoot/bin/$Configuration/nerdbank_qrcodes.xcframework"
xcodebuild -create-xcframework -framework $SimulatorFrameworkDir -framework $DeviceFrameworkDir -output $xcframeworkOutputDir
Write-Host "Created nerdbank_qrcodes.xcframework at $xcframeworkOutputDir"
