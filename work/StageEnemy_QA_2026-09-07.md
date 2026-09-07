# Stage enemy placement — 2026-09-07
Scope: Enemy only. No MapGenerator or Shop/Chest changes.
Scene: moon_branch Jeon Yong. Existing EnemySpawner GameObject renamed Stage Spawner; same EnemySpawner component retained; StageSpawner added with direct map and enemy-spawner references.
Flow: UIFlow -> StageSpawner -> EnemySpawner.SpawnEnemy(tile,data) -> existing EnemySpawned event -> BattleSceneInstaller -> UnitRegistry. No new event subscription; existing subscribe/unsubscribe lifetime preserved.
Stage data: 1=normal5; 2=normal6+elite1; 3=normal8+elite2; 4=boss1. Test values, not final balance. Candidates use EnemyData.id, not array position.
Logic verification: 19 checks passed with lightweight Unity stubs (roster validation, stable IDs after reorder, rank mismatch, 3x3 exclusions, safe zone, distinct tiles, duplicate calls, insufficient tiles). This does not verify Unity runtime.
Scene verification: required local references resolve; all four data assets connected; MapGenerator block unchanged.
Play Mode: NOT VERIFIED. Select initialStageNumber 1-4 before play; confirm counts and models, safe area, no enemies on Store/Box, HP/MP and enemy turn registration, stage announcement. Source yeop/moon_branch scenes retain old flow.
Not implemented: automatic stage progression, enemy skills/animations, boss summon execution and summon lifetime/cooldown, mercenaries. StagePlacement.CollectSummonTiles provides only the spatial query; caller must supply current occupied tiles.
