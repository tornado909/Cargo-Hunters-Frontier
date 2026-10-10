# Frontier Universal Repair Kit 0.1.1 — тестовая интеграция

**Статус: EXPERIMENTAL / NATIVE LOAD VERIFIED / REPAIR USE NOT VERIFIED.**

10 октября 2026: версия установлена в `D:\Games\Cargo Hunters Modded`. Проверен нормальный запуск `CargoHunters.exe`, BepInEx загружает DLL, три исходные операции сохраняются при перекомпозиции пакета, игровые каталоги `item_templates`, `shop_templates`, `craft_recipes` проходят `objects-verified`. Основной сейв не изменён.

Набор имеет собственный ID `df757459-6db2-581c-9038-917f741c0866`, цену 5 000 кредитов, одноразовый ресурс, штатную визуальную модель и отдельные имя/описание. Предусмотрены предложение в стартовом магазине и рецепт из ресурсов Frontier.

## Серверный эффект

Отдельная BepInEx DLL перехватывает **только авторитетные** операции:
- `RestoreConditionOperation.ExecuteAuthoritative`;
- `RestoreConditionContinuousOperation.ExecuteOneTimeAuthoritative`;
- `RestoreConditionContinuousOperation.FinishAuthoritative`.

После штатной проверки и выполнения операции она подтверждает, что использовался именно новый комплект, его заряд был потрачен и состояние предмета допустимо. Только тогда восстанавливает уменьшенный максимум через `ConditionComponent.SetMaxValue(PerfectValue)`. При несовместимой сигнатуре перехват отключается с ошибкой, а не меняет сохранения.

**Ограничение:** реальная покупка, расходование набора, синхронизация изменённой максимальной прочности с клиентом и постоянным сохранением, а также двухкомпьютерная игра пока не проверены. Сообщение `UNIVERSAL_REPAIR_MAX_RESTORED` в журнале ещё не наблюдалось (нулевое число применений). Не выдавать за стабильный релиз.

## Почему меняются исходные файлы

`Frontier.Modular` пересобирает `compiled/` при запуске. Поэтому источником является `modules/workshop/operations.json`, `localization.json` и контрольные SHA-256 в `module.json`. Инструменты: `tools/generate_universal_repair_catalog.py` и `tools/stage_universal_repair_module.py`.

## Сборка и откат

DLL компилируется на ASC-AI через `dotnet build -p:GameReferenceRoot=...`, не на игровом ПК. Библиотеки IL2CPP не коммитятся. Стабильный FrontierPack не пересобирался.

Резервная копия модульных JSON находится в `C:\Users\ascento\FrontierKitWork\backups\source-main-20261010T044323Z`; исходный сейв — в `main-save-20261010T044315Z`. Откат: закрыть игру, восстановить три исходных файла workshop и отключить новый `FrontierUniversalRepairKit` (с сохранением установленной DLL). Не сбрасывать игровые сохранения.

В кооперативе набор исходных файлов и DLL должен быть **одинаковым у всех игроков**, иначе работает штатная блокировка несовместимого пакета.
