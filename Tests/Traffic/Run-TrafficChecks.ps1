param([string]$UnityEditorDirectory)

$ErrorActionPreference = 'Stop'
$trafficProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $UnityEditorDirectory) {
    $trafficVersionLine = Get-Content -LiteralPath (Join-Path $trafficProjectRoot 'ProjectSettings/ProjectVersion.txt') |
        Where-Object { $_ -match '^m_EditorVersion: ' } | Select-Object -First 1
    $trafficUnityVersion = ($trafficVersionLine -replace '^m_EditorVersion: ', '').Trim()
    $UnityEditorDirectory = Join-Path 'C:/Program Files/Unity/Hub/Editor' ($trafficUnityVersion + '/Editor')
}
$trafficData = Join-Path $UnityEditorDirectory 'Data'
$trafficDotnet = Join-Path $trafficData 'NetCoreRuntime/dotnet.exe'
$trafficCompiler = Join-Path $trafficData 'DotNetSdkRoslyn/csc.dll'
$trafficMono = Join-Path $trafficData 'MonoBleedingEdge/bin/mono.exe'
$trafficLibraries = Join-Path $trafficData 'MonoBleedingEdge/lib/mono/4.5'
$trafficUnityAssemblies = Join-Path $trafficData 'Managed/UnityEngine'
$trafficOutput = Join-Path $trafficProjectRoot 'Temp/Traffic'
New-Item -ItemType Directory -Path $trafficOutput -Force | Out-Null
$trafficExecutable = Join-Path $trafficOutput 'TrafficChecks.exe'
$trafficResponse = Join-Path $trafficOutput 'Checks.rsp'
$trafficArguments = @(
    '-nologo', '-target:exe', '-langversion:9.0', '-nostdlib+',
    ('-out:"' + $trafficExecutable + '"'),
    ('-r:"' + $trafficLibraries + '/mscorlib.dll"'),
    ('-r:"' + $trafficLibraries + '/System.dll"'),
    ('-r:"' + $trafficLibraries + '/System.Core.dll"'),
    ('-r:"' + $trafficLibraries + '/Facades/netstandard.dll"'),
    ('-r:"' + $trafficUnityAssemblies + '/UnityEngine.CoreModule.dll"')
)
foreach ($trafficSourceName in @('TrafficPath.cs', 'TrafficGeometry.cs', 'TrafficReservations.cs', 'TrafficGate.cs')) {
    $trafficSourcePath = Join-Path $trafficProjectRoot ('Assets/_Project/Scripts/Gameplay/Traffic/' + $trafficSourceName)
    $trafficArguments += '"' + $trafficSourcePath + '"'
}
$trafficArguments += '"' + (Join-Path $PSScriptRoot 'TrafficChecks.cs') + '"'
Set-Content -LiteralPath $trafficResponse -Value $trafficArguments -Encoding UTF8
& $trafficDotnet $trafficCompiler ('@' + $trafficResponse)
if ($LASTEXITCODE -ne 0) { throw 'Traffic checks failed to compile.' }
$trafficPreviousMonoPath = $env:MONO_PATH
try {
    $env:MONO_PATH = $trafficUnityAssemblies
    & $trafficMono $trafficExecutable
    if ($LASTEXITCODE -ne 0) { throw 'Traffic checks failed.' }
}
finally { $env:MONO_PATH = $trafficPreviousMonoPath }
