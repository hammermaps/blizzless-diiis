<#
Builds CascLib.dll (x64, Release) from external/CascLib and copies it to src/DiIiS-NA/libs, where the server
loads it to read the installed game's CASC storage. Needs Visual Studio 2022 with the C++ workload.
#>
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$casc = Join-Path $repo 'external\CascLib'
if (-not (Test-Path "$casc\CascLib_dll.vcxproj")) {
    git -C $repo submodule update --init external/CascLib
}

# DllMain.rc includes MFC's afxres.h; winres.h covers what it uses, so the MFC workload isn't needed.
$shim = Join-Path $casc 'src\afxres.h'
if (-not (Test-Path $shim)) { Set-Content -Path $shim -Value '#include <winres.h>' -Encoding ASCII }

# CascLib asks for write access when it checks that .build.info and other local files open, although it only
# reads them. That fails wherever the game folder is read-only, Codex's sandbox included. Open them read-only.
$patch = Join-Path $PSScriptRoot 'casclib-read-only.patch'
if (-not (Select-String -Path "$casc\src\CascFiles.cpp" -SimpleMatch 'FileStream_OpenFile(szLocalPath, STREAM_FLAG_READ_ONLY)' -Quiet)) {
    git -C $casc apply $patch
    if ($LASTEXITCODE -ne 0) { throw "Could not apply $patch to external/CascLib." }
}

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = & $vswhere -version '[17.0,18.0)' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Select-Object -First 1
if (-not $vs) { throw 'Visual Studio 2022 with the C++ workload (toolset v143) is required.' }

& "$vs\MSBuild\Current\Bin\MSBuild.exe" "$casc\CascLib_dll.vcxproj" /p:Configuration=Release /p:Platform=x64 /m /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "CascLib build failed ($LASTEXITCODE)." }

Copy-Item "$casc\bin\CascLib_dll\x64\Release\CascLib.dll" (Join-Path $repo 'src\DiIiS-NA\libs\CascLib.dll') -Force
Write-Host 'CascLib.dll copied to src\DiIiS-NA\libs.'
