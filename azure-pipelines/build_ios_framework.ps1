[CmdletBinding()]
param (
    [parameter()]
    [string]$Configuration = 'release'
)

$version = dotnet nbgv get-version -p $PSScriptRoot/../src/nerdbank-qrcodes -v SimpleVersion
$plist = Get-Content $PSScriptRoot/Info.plist
$plist = $plist.Replace('$version$', $version)
$IntermediatePlistPath = "$PSScriptRoot/../obj/Info.plist"
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
$RustTargetBaseDir = Resolve-Path "$PSScriptRoot/../src/nerdbank-qrcodes/target"
$RustDylibFileName = "libnerdbank_qrcodes.dylib"
$Arm64RustOutput = "$RustTargetBaseDir/aarch64-apple-ios/$Configuration/$RustDylibFileName"
$X64RustOutput = "$RustTargetBaseDir/x86_64-apple-ios/$Configuration/$RustDylibFileName"

$FrameworkDir = "$PSScriptRoot/../bin/$Configuration/nerdbank_qrcodes.framework"
New-Item -Path $FrameworkDir -ItemType Directory -Force | Out-Null
$FrameworkDir = Resolve-Path $FrameworkDir

Write-Host "Preparing Apple Framework at: $FrameworkDir" -ForegroundColor Cyan

Copy-Item $IntermediatePlistPath "$FrameworkDir/Info.plist"
Write-Host "Created Info.plist with version $version"

if ($IsMacOS) {
    # Create a universal binary that contains both arm64 and x64 architectures.
    lipo -create -output $FrameworkDir/nerdbank_qrcodes $Arm64RustOutput $X64RustOutput
    install_name_tool -id "@rpath/nerdbank_qrcodes.framework/nerdbank_qrcodes" "$FrameworkDir/nerdbank_qrcodes"
    chmod +x "$FrameworkDir/nerdbank_qrcodes"
}
else {
    Copy-Item $Arm64RustOutput "$FrameworkDir/nerdbank_qrcodes"
    Write-Warning "Skipped critical steps because this is not macOS."
}
Write-Host "Copied nerdbank_qrcodes to framework"
