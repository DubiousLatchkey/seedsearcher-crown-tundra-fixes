[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vsRoot = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
if (-not $vsRoot) { throw 'Visual Studio with MSBuild and C++ v142 tools is required.' }
$msbuild = Join-Path $vsRoot 'MSBuild\Current\Bin\MSBuild.exe'
# A fresh directory prevents obsolete GPU runtimes leaking into a release.
$outputDir = Join-Path $repoRoot ('artifacts\release-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $outputDir | Out-Null
Push-Location $repoRoot
try {
    $nuget = Join-Path $PSScriptRoot 'nuget.exe'
    if (-not (Test-Path $nuget)) { Invoke-WebRequest 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile $nuget }
    & $nuget restore SeedSearcher.sln -NonInteractive
    if ($LASTEXITCODE) { throw 'Package restore failed.' }
    & $msbuild SeedSearcherLib/SeedSearcherLib.vcxproj /t:Rebuild /p:Configuration=Release /p:Platform=x64 "/p:OutDir=$outputDir\" "/p:SolutionDir=$repoRoot\" /v:minimal /nologo
    if ($LASTEXITCODE) { throw 'Native build failed.' }
    & $msbuild SeedSearcher/SeedSearcherGui.csproj /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU "/p:OutDir=$outputDir\" /v:minimal /nologo
    if ($LASTEXITCODE) { throw 'Managed build failed.' }
    Copy-Item -LiteralPath Events -Destination $outputDir -Recurse
    Copy-Item -LiteralPath LICENSE,README.md -Destination $outputDir
    Copy-Item -LiteralPath docs -Destination $outputDir -Recurse
    $licenseDir = Join-Path $outputDir 'licenses'
    New-Item -ItemType Directory -Path $licenseDir | Out-Null
    Copy-Item -LiteralPath packages/ILGPU.1.5.3/LICENSE.txt -Destination (Join-Path $licenseDir 'ILGPU.txt')
    Copy-Item -LiteralPath packages/ILGPU.1.5.3/LICENSE-3RD-PARTY.txt -Destination (Join-Path $licenseDir 'ILGPU-dependencies.txt')
    # Native linker/debug artifacts are not part of the runtime distribution.
    Get-ChildItem -LiteralPath $outputDir -File | Where-Object { $_.Extension -in '.pdb','.lib','.exp','.xml' } | Remove-Item
    if (Get-ChildItem -LiteralPath $outputDir -Recurse | Where-Object { $_.Name -match 'Alea|EnableCuda|CudaSetup' }) {
        throw 'Obsolete GPU files detected in release.'
    }
    Compress-Archive -Path (Join-Path $outputDir '*') -DestinationPath ($outputDir + '.zip')
    Write-Output "Release: $outputDir"
}
finally { Pop-Location }
