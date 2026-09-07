$paths = @('work/stage_pool_checks.cs','Assets/renew/Battle/Stage/StageObjData.cs','Assets/renew/Battle/Stage/StageEnemyBridge.cs','Assets/renew/Battle/Stage/StagePlacement.cs','Assets/renew/Battle/Stage/StageSpawner.cs','Assets/renew/Battle/Stage/EnemyPool.cs')
$ErrorActionPreference = 'Stop'
Add-Type -Path $paths -CompilerOptions '/nowarn:0649'
[StagePoolChecks]::Run()
