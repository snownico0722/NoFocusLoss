param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $msbuild = (Get-Command MSBuild.exe -ErrorAction SilentlyContinue).Source
    if (-not $msbuild) {
        $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
        $msbuild = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    }
    if (-not $msbuild) { throw 'Install Visual Studio C++ build tools (v143) and the Windows SDK.' }
    foreach ($platform in @('Win32')) {
        & $msbuild NoFocusLoss/NoFocusLoss.vcxproj /nologo /m "/p:Configuration=$Configuration" "/p:Platform=$platform"
        if ($LASTEXITCODE -ne 0) { throw "Native $platform build failed." }
    }
    dotnet publish NoFocusLossGUI/NoFocusLossGUI.csproj -c $Configuration -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o artifacts/app
    if ($LASTEXITCODE -ne 0) { throw '.NET 10 WPF publish failed.' }
    Copy-Item README.md artifacts/app/README.md
    Copy-Item LICENSE.txt artifacts/app/LICENSE.txt
    Copy-Item SharpestInjector/LICENSE.txt artifacts/app/SharpestInjector-LICENSE.txt
    Copy-Item NoFocusLoss/minhook/LICENSE.txt artifacts/app/MinHook-LICENSE.txt
} finally { Pop-Location }
