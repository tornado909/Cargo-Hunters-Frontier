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


## Красная полоса максимальной прочности — экспериментальный фикс 0.1.2

10.10.2026: игрок подтвердил применение универсального набора к голове:
жёлтая прочность восстановилась, но красная осталась. При этом в журнале
было UNIVERSAL_REPAIR_MAX_RESTORED path=immediate from=0.197 to=5 kitSpent=yes.

Причина возможной рассинхронизации: 0.1.1 вызывал
ConditionComponent.SetMaxValue в Harmony-postfix авторитетной операции
RestoreConditionOperation.ExecuteAuthoritative. К моменту его вызова
игра уже могла сформировать ответные ItemDto. Событие MAX_RESTORED было
ошибочно объявлено успешным без проверки клиентской и сохранённой копии.

Новый 0.1.2, собранный на ASC-AI, выполняет восстановление максимума
только внутри штатной авторитетной операции и только при совпадении
настоящего TemplateId комплекта и указателя целевого компонента.
Значение меняется во время native ConditionComponent.Repair(float),
до формирования итогового ItemDto. По завершении модуль отдельно
проверяет факт расхода набора, MaxValue и NotPerfectMaxValue.
Если native-комплект не был потрачен, изменение откатывается.
Прежнее сообщение MAX_RESTORED не используется.

СТАТУС: сборка NET6 прошла (0 ошибок/предупреждений), но реальное
применение, репликация клиенту, сохранение после перезапуска и
восстановление красной части ЕЩЁ НЕ ПОДТВЕРЖДЕНЫ. Основная игра
во время подготовки была запущена, поэтому DLL не заменена.
Первый тест — только в изолированной Runtime.

Существующий ID комплекта, цена 5 000 и рецепт сохранены; прямое
редактирование сохранений не применяется.


## Приёмка Universal Repair Kit 0.1.5 — PASS (10.10.2026)

Исправлена реальная причина незакрывавшейся красной части: только
`SetMaxValue` недостаточно, когда максимум уже равен эталонному,
но `Value` предмета остаётся `0.196754`. Реализован согласованный
ремонт на клиентской предикции `ExecuteLocally` и авторитетной
`ExecuteAuthoritative`, обе стороны используют только штатную
операцию и `ConditionComponent.Repair` (до построения ItemDto).

Изолированная Runtime с **копией** актуального offline.save:

- Цель: голова `f310f0c0-f9df-4f8b-a1a8-844f274a3306`.
- До: `Value=0.196754`, `MaxValue=5`, `PerfectValue=5`.
- Оригинальный купленный набор `65424712-b194-4c09-a1ce-5bffcee86e18`, заряд `1`.
- `IItemsOperationProcessor.CanExecute = true`, `Execute = true`.
- Сервер: `SuccessItemOperationResponse`, `kitSpent=yes`, `savedMax=5`, `savedCurrent=5`, `preDtoVerified=True`.
- Клиент спустя несколько секунд: `Value=5`, `MaxValue=5`, расходованный набор отсутствует.
- Вызвано **штатное** `ExitToMainMenuClickedEventArgs`; журнал содержит
  `OfflineAccountContextPersistenceService: Account context successfully saved`.
- Тестовый `offline.save` изменился. После повторного входа в убежище
  нативный компонент снова показал `Value=5`, `MaxValue=5`,
  расходованный комплект не появился; сохранённый `Condition_d`
  (0.196754) отсутствует как необязательное поле полной прочности.
- Основной сейв с SHA256 `9bcc41038ed2414a96753e2396614be8713c6b4f4c4aac5c20d80020db9567f7`
  **не изменялся** при тестировании и установке.
- Главная DLL `Frontier.UniversalRepairKit.dll` 0.1.5 с SHA256
  `f8145c3d47a2ba5d1a0bbe8bf94ea7f41773e0b25600baff044994ae4710f930`
  установлена; `CargoHunters.exe` запущена, BepInEx отчитался об успешной
  загрузке версии и об отсутствии ошибок.

Резервная копия основной DLL и сейва:
`C:\\Users\\ascento\\FrontierKitWork\\backups\\universal-repair-v015-main-20261010T091317Z`.

Пакет для второго ПК, полный набор изменённых исходных каталогов workshop
и DLL без сохранений:
https://github.com/tornado909/Cargo-Hunters-Frontier/blob/builds/universal-repair-kit-0.1.5-native-verified/builds/Frontier_UniversalRepairKit_0.1.5_NATIVE_VERIFIED.zip

**Открыто:** мультиплеерный хост/гость не тестировались. Остальные категории
проверены на совместимость фильтром и по загрузке каталогов, но полная
нативная приёмка каждого типа экипировки не выполнена.
