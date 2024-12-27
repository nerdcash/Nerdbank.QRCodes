$root = "$PSScriptRoot\..\..\src\nerdbank-qrcodes\target"
if (!(Test-Path $root)) { return }

$files = @()
Get-ChildItem $root\*-*-* -Directory |% {
    $files += Get-ChildItem "$($_.FullName)\*\*nerdbank_qrcodes*"
}

@{
    $root = $files
}
