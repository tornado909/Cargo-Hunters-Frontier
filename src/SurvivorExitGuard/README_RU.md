# Survivor Exit Guard 0.1.0 — исправление выхода из Survivor

**Экспериментальный модуль, не заменяет Frontier.Survivor.dll.**

## Основание

Из предоставленного пользователем `BepInEx Logs(20261010-054012).zip`:
- `ErrorLog.log`: `System.AccessViolationException` внутри
  `Unity.Entities.EntityManager.GetComponentData<Translation>` →
  `Frontier.Survivor.SurvivorRun.Tick` →
  `Frontier.Survivor.ReserveLoop.Update`.
- `Frontier-survivor-report.json`: Survivor 0.2.0, карта
  `Frontier_Survivor`, `status=RUNNING`, 169,88 секунд, 41 убийство,
  ранг F2. Нет статуса завершения мира.
- Декомпиляция фактически установленной `Frontier.Survivor.dll`: в
  `SurvivorRun.Tick` после `EntityManager.Exists(playerEntity)`
  выполняется `GetComponentData<Translation>(playerEntity)`, без
  `HasComponent<Translation>`. Путь `Spawn` имеет ещё одно чтение
  `Translation` для участников.
- Оригинальный `Hooks.ClientLeave`/`ServerLeave` освобождает
  `WorldBridge` **после** нативных `Leave/Shutdown`.

Нативный `AccessViolationException` нельзя безопасно перехватить
стандартным `try/catch`.

## Исправление

Два уровня защиты только для карты `Frontier_Survivor`:

1. Harmony-prefix для `EnterExitState`, `EnterResultsState`,
   `EnterConnectionLostState`, `LeaveBattleState`, `Leave` и
   `Shutdown` у `ClientBattleState`, плюс `EnterExitState`,
   `LeaveBattleState`, `Leave` у `ServerBattleState`. Непосредственно
   перед началом нативного завершения ставится latch «забег закрывается».
   Повторные вызовы идемпотентны; при новом `EnterBattleState` latch
   сбрасывается.
2. Harmony-prefix для `SurvivorRun.Tick` не позволяет тиковому
   коду читать ECS после начала выхода. В работающей сессии он проверяет,
   что серверный и клиентский мир существуют и у существующих живых
   человеческих ECS-сущностей ещё есть компонент `Translation`.

**Никакие функции Spawn, Kill, Bonus, Ranks, Saver, Weapons, WearGuard,
Ammo или Nanorepair не изменяются.** Исходная DLL Survivor 0.2.0
сохраняется.

## Проверки и статусы

- Исходная DLL Survivor 0.2.0 SHA256:
  `aa77a6b3f3f4b5f4d3d24bbf841371de6d9ba72cf7e0681db80de7c7114ebb92`.
- Сборка выполняется только на ASC-AI, с теми же interop-библиотеками
  Cargo Hunters 0.26.38.59.
- Управляемые тесты `ExitGate`: 4 009 проверок — PASS.
- **Нативная проверка реальной эвакуации в хаб обязательна**; успешная
  компиляция не доказывает исправления падения.
- Пакет для хоста и гостя должен быть одинаковым. Не обходить
  Frontier pack signature/admission.
- Установку тестировать с отдельным профилем и сохранением, основной
  сейв резервировать; не проводить автовосстановление после краша
  без подтверждённой корректной операции.

## Что считать PASS

В одиночной игре:
1. Игрок входит в Survivor, проходит 2–4 минуты,
   использует настоящий выход и возвращается в хаб.
2. Журнал содержит `SURVIVOR_EXIT_GUARD_ARMED`, затем
   `SURVIVOR_TICK_SKIPPED_DURING_EXIT`, без `AccessViolationException`.
3. Игра снова запускает Survivor из хаба (latch переустановился).
4. Здоровье, патроны, прочность и инвентарь после выхода работают штатно.

В кооперативе:
1. Оба игрока стартуют с одинаковым комплектом модов.
2. Выход хоста/гостя и смерть одного участника не приводят к крашу
   и не завершают чужой забег преждевременно.

До прохождения нативного теста статус: **EXPERIMENTAL**.
