[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0.0'
)

$ErrorActionPreference = 'Stop'
$installerRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Split-Path -Parent $installerRoot
$project = Join-Path $repositoryRoot 'src\Alpha.Branding\Alpha.Branding.csproj'
$bootstrapper = Join-Path $installerRoot 'Bootstrapper\Bootstrapper.csproj'
$artifactDirectory = Join-Path $repositoryRoot 'artifacts'
$artifact = Join-Path $artifactDirectory 'Alpha.Branding.Setup.exe'
$stage = Join-Path ([System.IO.Path]::GetTempPath()) ('Alpha.Branding-installer-' + [guid]::NewGuid().ToString('N'))
$publish = Join-Path $stage 'publish'
$bootstrapPublish = Join-Path $stage 'bootstrapper'
$payload = Join-Path $stage 'payload.zip'
$marker = 'ALPHA_BRANDING_PAYLOAD_V1'

function Require-File([string]$Path, [string]$Description) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required $Description was not found: $Path" }
}

try {
    Require-File $project 'WPF project'
    Require-File $bootstrapper 'bootstrapper project'
    New-Item -ItemType Directory -Path $publish, $bootstrapPublish, $artifactDirectory -Force | Out-Null

    Write-Host 'Publishing self-contained win-x64 application...'
    & dotnet publish $project --configuration Release --runtime win-x64 --self-contained true --output $publish --nologo
    if ($LASTEXITCODE -ne 0) { throw "Application publish failed with exit code $LASTEXITCODE." }
    Require-File (Join-Path $publish 'Alpha.Branding.exe') 'published executable'
    Require-File (Join-Path $publish 'Assets\logo_phoenix.png') 'published phoenix logo asset'
    Require-File (Join-Path $publish 'Assets\logo w name.png') 'published full logo asset'
    Require-File (Join-Path $publish 'Assets\alpha_branding.png') 'published branding asset'
    Set-Content -LiteralPath (Join-Path $publish 'InstallerVersion.txt') -Value $Version -Encoding ASCII

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($publish, $payload, [System.IO.Compression.CompressionLevel]::Optimal, $false)
    Require-File $payload 'publish payload archive'

    Write-Host 'Publishing self-contained single-file bootstrapper...'
    & dotnet publish $bootstrapper --configuration Release --runtime win-x64 --self-contained true --output $bootstrapPublish --nologo -p:PublishSingleFile=true
    if ($LASTEXITCODE -ne 0) { throw "Bootstrapper publish failed with exit code $LASTEXITCODE." }
    $bootstrapExe = Join-Path $bootstrapPublish 'Bootstrapper.exe'
    Require-File $bootstrapExe 'published bootstrapper executable'

    if (Test-Path -LiteralPath $artifact) { Remove-Item -LiteralPath $artifact -Force }
    Copy-Item -LiteralPath $bootstrapExe -Destination $artifact
    $payloadBytes = [System.IO.File]::ReadAllBytes($payload)
    $markerBytes = [System.Text.Encoding]::UTF8.GetBytes($marker)
    $lengthBytes = [BitConverter]::GetBytes([int64]$payloadBytes.Length)
    $stream = [System.IO.File]::Open($artifact, [System.IO.FileMode]::Append, [System.IO.FileAccess]::Write, [System.IO.FileShare]::Read)
    try { $stream.Write($payloadBytes, 0, $payloadBytes.Length); $stream.Write($markerBytes, 0, $markerBytes.Length); $stream.Write($lengthBytes, 0, $lengthBytes.Length) } finally { $stream.Dispose() }
    Require-File $artifact 'installer executable'
    if ((Get-Item -LiteralPath $artifact).Length -le 0) { throw 'Installer executable is empty.' }
    $bytes = [System.IO.File]::ReadAllBytes($artifact)
    $length = [BitConverter]::ToInt64($bytes, $bytes.Length - 8)
    $markerAt = $bytes.Length - 8 - $markerBytes.Length
    $actualMarker = [System.Text.Encoding]::UTF8.GetString($bytes, $markerAt, $markerBytes.Length)
    if ($length -ne $payloadBytes.Length -or $actualMarker -cne $marker) { throw 'Installer trailer validation failed.' }
    Write-Host ("Created {0} ({1} bytes)." -f $artifact, (Get-Item -LiteralPath $artifact).Length)
} finally {
    if (Test-Path -LiteralPath $stage) {
        try { Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue } catch { }
    }
}
