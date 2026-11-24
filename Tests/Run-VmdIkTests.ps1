param([string]$TestSource = 'Tests/VmdIkTests.cs', [switch]$WindowsPlayer)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    [xml]$projectXml = Get-Content Assembly-CSharp.csproj
    $references = @($projectXml.Project.ItemGroup.Reference | Where-Object { $_.HintPath } | ForEach-Object { $_.HintPath })
    $unityEngineDir = Split-Path ($references | Where-Object { $_ -like '*UnityEngine.CoreModule.dll' } | Select-Object -First 1)
    $editorData = Split-Path (Split-Path $unityEngineDir)
    $mono = Join-Path $editorData 'MonoBleedingEdge/bin/mono.exe'
    $compiler = Join-Path $editorData 'MonoBleedingEdge/lib/mono/4.5/csc.exe'
    $outputDir = Join-Path ([IO.Path]::GetTempPath()) ('LibMmdIkTests-' + [guid]::NewGuid())
    New-Item -ItemType Directory -Path $outputDir | Out-Null
    $assembly = Join-Path $outputDir 'VmdIkTests.exe'
    $compilerArgs = @('/nologo', '/target:exe', '/unsafe', ('/out:"' + $assembly + '"'))
    if ($WindowsPlayer) { $compilerArgs += '/define:UNITY_STANDALONE_WIN' }
    $compilerArgs += $references | ForEach-Object { '/reference:"' + $_ + '"' }
    $compilerArgs += $projectXml.Project.ItemGroup.ProjectReference | Where-Object { $_.Include } | ForEach-Object {
        '/reference:"Library/ScriptAssemblies/' + [IO.Path]::GetFileNameWithoutExtension($_.Include) + '.dll"'
    }
    $compilerArgs += $projectXml.Project.ItemGroup.Compile | Where-Object { $_.Include } | ForEach-Object { '"' + $_.Include + '"' }
    $compilerArgs += '"' + $TestSource + '"'
    $responseFile = Join-Path $outputDir 'tests.rsp'
    $compilerArgs | Set-Content $responseFile
    & $mono $compiler "@$responseFile"
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
    $previousMonoPath = $env:MONO_PATH
    try {
        $env:MONO_PATH = $unityEngineDir
        & $mono $assembly
        if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed' }
    } finally { $env:MONO_PATH = $previousMonoPath }
} finally { Pop-Location }
