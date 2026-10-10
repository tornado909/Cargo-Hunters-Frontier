# Сборка Frontier

**GitHub Actions настроен для ZIP с корневой папкой BepInEx.** Подробный регламент: [RELEASE_PIPELINE.md](RELEASE_PIPELINE.md).

## Что собирается автоматически

- src/Plugins/UniversalRepairKit
- src/Plugins/SurvivorWearGuard
- src/Plugins/SurvivorExitGuard

Каждый из этих модулей собран с version-matched референсами, и результат на проверенном build host ранее совпал SHA-256 с установленной DLL.

В Release также входят **32 других проверенных DLL Frontier** из SHA-256-зафиксированного production-снимка. Они **не** пересобираются из исходников в Actions — текущая исходная база неполная. Полная сборка всех модулей запрещена до переноса актуальных production-исходников и игровых испытаний.

## Принципы

- Game-version pin: **0.26.38.59**, несоответствие останавливает Release.
- Не класть game-generated IL2CPP референсы, дампы, сохранения, Steam-данные, артефакты BepInEx и чужие игровые исходники в публичный Git.
- Не собирать на игровом ПК; GitHub-hosted runner.
- Не заменять актуальный установленный Survivor 0.2.0 устаревшими src/Survivor исходниками.
- Не подменять production Core или Loot декомпилированными reference-проектами.
- Выдавать именно папку **BepInEx/** внутри ZIP.
- Публиковать SHA256SUMS и BUILD-INFO с честным происхождением каждой DLL.
- Минимизировать Actions storage: release assets напрямую в Releases; тестовый artifact хранить 1 день.

Стабильные изменения требуют ручной приемки в игре, а в мультиплеере — идентичного модпака у каждого игрока.
