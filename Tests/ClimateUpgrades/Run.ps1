param([string]$UnityEditorPath = 'C:/Program Files/Unity/Hub/Editor/6000.3.27f2/Editor')
$ErrorActionPreference = 'Stop'
$taskProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskOutputDir = Join-Path $taskProjectRoot 'Temp/ClimateUpgrades'
New-Item -ItemType Directory -Force -Path $taskOutputDir | Out-Null
$taskLines = @('-nologo', '-target:exe', '-langversion:9.0', '-nostdlib+', '-nowarn:0067,0649',
    ('-out:"' + (Join-Path $taskOutputDir 'Checks.exe') + '"'))
foreach ($taskAssembly in @('mscorlib', 'System', 'System.Core')) {
    $taskLines += '-r:"' + (Join-Path $UnityEditorPath ('Data/MonoBleedingEdge/lib/mono/4.5/' + $taskAssembly + '.dll')) + '"'
}
foreach ($taskSource in @(
    'Tests/ClimateUpgrades/Stubs.cs', 'Tests/ClimateUpgrades/Checks.cs',
    'Assets/_Project/Scripts/Core/App/GameManager.cs', 'Assets/_Project/Scripts/Core/App/GameState.cs',
    'Assets/_Project/Scripts/Core/Events/EventBus.Game.cs',
    'Assets/_Project/Scripts/Core/Events/EventBus.Tooltip.cs',
    'Assets/_Project/Scripts/Input/AttackInputGate.cs',
    '../vector-violet/Utilities/Scripts/Infrastructure/Timer.cs',
    'Assets/_Project/Scripts/Gameplay/Climate/SurvivalTimer.cs',
    'Assets/_Project/Scripts/Gameplay/Fire/FireSpawnSchedule.cs',
    'Assets/_Project/Scripts/Gameplay/Tasks/TaskDefinition.cs',
    'Assets/_Project/Scripts/Gameplay/Tasks/TaskRewardManager.cs',
    'Assets/_Project/Scripts/Gameplay/Tasks/CarbonPointLedger.cs',
    'Assets/_Project/Scripts/Gameplay/Climate/ClimateManager.cs',
    'Assets/_Project/Scripts/Gameplay/Climate/EcoUpgradeDefinition.cs',
    'Assets/_Project/Scripts/Gameplay/Climate/EcoUpgradeSite.cs',
    'Assets/_Project/Scripts/Gameplay/Climate/EcoUpgradeManager.cs',
    'Assets/_Project/Scripts/UI/EcoUpgradeButton.cs'
)) { $taskLines += '"' + (Join-Path $taskProjectRoot $taskSource) + '"' }
$taskResponsePath = Join-Path $taskOutputDir 'Checks.rsp'
[IO.File]::WriteAllLines($taskResponsePath, $taskLines, [Text.UTF8Encoding]::new($false))
& (Join-Path $UnityEditorPath 'Data/NetCoreRuntime/dotnet.exe') (Join-Path $UnityEditorPath 'Data/DotNetSdkRoslyn/csc.dll') ('@' + $taskResponsePath)
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& (Join-Path $UnityEditorPath 'Data/MonoBleedingEdge/bin/mono.exe') (Join-Path $taskOutputDir 'Checks.exe')
exit $LASTEXITCODE
