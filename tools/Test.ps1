[CmdletBinding()]
param([ValidateSet('CPU','CUDA','OpenCL')][string]$Backend = 'CUDA', [string]$Filter = '', [switch]$Acceptance, [switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
if ($Acceptance -and $Filter) { throw 'Choose -Acceptance or -Filter, not both.' }
if ($Acceptance) {
    if ($Backend -ne 'CPU') {
        $Filter = 'Name~Test_Fixed1_|Name~Test_Fixed2_6_|Name=Test_Fixed2_5_Deviation0|Name=Test_Fixed2_5_Deviation0_LSB_Bad|Name=Test_Fixed2_4_Deviation0|Name=Test_Fixed2_4_Deviation0_LSB_Bad|Name=Test_Fixed3_5_Deviation0|Name=Test_Fixed3_5_Deviation0_LSB_Bad|Name=Test_Fixed3_4_Deviation0|Name=Test_Fixed3_4_Deviation0_LSB_Bad|FullyQualifiedName~GpuRuntimeTests|FullyQualifiedName~GpuEncounterTests'
    }
    else {
        $Filter = 'Name=Test_Fixed1_Deviation0|Name=Test_Fixed2_6_Deviation0|Name=Test_Fixed2_6_Deviation0_LSB_Bad|Name=Test_Fixed2_5_Deviation0|Name=NativeFour_ForwardGeneratedCandidate|Name=CpuAccelerator_BoundedCoverage|Name=MissingDevice_ReportsFailure'
    }
}

$repoRoot = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vsRoot = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
if (-not $vsRoot) { throw 'Visual Studio with MSBuild, .NET 4.8 targeting pack, and C++ v142 tools is required.' }
$msbuild = Join-Path $vsRoot 'MSBuild\Current\Bin\MSBuild.exe'
$runner = Join-Path $vsRoot 'Common7\IDE\Extensions\TestPlatform\vstest.console.exe'
Push-Location $repoRoot
$oldBackend = $env:SEEDSEARCHER_TEST_BACKEND
try {
    if (-not $SkipBuild) {
        $nuget = Join-Path $PSScriptRoot 'nuget.exe'
        if (-not (Test-Path $nuget)) { Invoke-WebRequest 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile $nuget }
        & $nuget restore SeedSearcher.sln -NonInteractive
        if ($LASTEXITCODE) { throw 'Package restore failed.' }
        & $msbuild SeedSearcher.sln /p:Configuration=Release /p:Platform=x64 /v:minimal /nologo
        if ($LASTEXITCODE) { throw 'Build failed.' }
        Copy-Item -LiteralPath SeedSearcher/bin/Release/x64/SeedSearcherLib.dll -Destination SeedSearcherTest/bin/Release/SeedSearcherLib.dll
    }
    $env:SEEDSEARCHER_TEST_BACKEND = $Backend
    $arguments = @('SeedSearcherTest/bin/Release/SeedSearcherTest.dll', '/Platform:x64', "/Logger:trx;LogFileName=$($Backend.ToLower())-tests.trx")
    if ($Filter) { $arguments += "/TestCaseFilter:$Filter" }
    & $runner @arguments
    if ($LASTEXITCODE) { throw 'Tests failed; inspect TestResults.' }
}
finally { $env:SEEDSEARCHER_TEST_BACKEND = $oldBackend; Pop-Location }
