# Cargo Hunters — Frontier

Модпак Frontier для **Cargo Hunters**: режимы, экипировка, улучшения, ранги, модификации частей тела, ремонт и другие возможности.

**[Скачать последнюю версию](../../releases/latest)** · [Сборки GitHub Actions](../../actions/workflows/build-release.yml) · [Документация](docs/RELEASE_PIPELINE.md)

## Установка

1. Требуется **Cargo Hunters 0.26.38.59** и установленный совместимый **BepInEx IL2CPP**.
2. Сделайте резервную копию старого модпака и сохранений.
3. Скачайте ZIP вида **Cargo-Hunters-Frontier-v*.zip** из Releases.
4. Распакуйте находящуюся внутри папку **BepInEx** в каталог игры, объединив папки.
5. Перезапустите игру; результат загрузки — **BepInEx/Frontier-report.json**.

Установочный ZIP содержит **BepInEx/plugins/FrontierPack**, а также три дополнительных модуля: **FrontierUniversalRepairKit**, **FrontierSurvivorWearGuard** и **FrontierSurvivorExitGuard**.

**Teammate Flags выпускается отдельно:** [Cargo-Hunters-Teammate-Flags](https://github.com/QueenDekim/Cargo-Hunters-Teammate-Flags).

Для мультиплеера у всех участников должны совпадать состав и версии Frontier.

## Сборка и происхождение бинарников

Workflow **.github/workflows/build-release.yml** запускается вручную или при публикации Git-тега, начинающегося с v.

- GitHub-hosted runner загружает защищённый зашифрованный набор IL2CPP-референсов и проверенный снимок пакета для соответствующей версии игры.
- **3 плагина** — UniversalRepairKit, SurvivorWearGuard, SurvivorExitGuard — действительно компилируются из исходников репозитория.
- **Оставшиеся 32 DLL** берутся из SHA-256-верифицированного production-снимка, а **не** пересобираются из неполных или устаревших исходников.
- GitHub Release содержит ZIP с корнем **BepInEx/**, файлы **SHA256SUMS.txt** и **BUILD-INFO.json** с перечнем собранных и готовых бинарников.

Это честный переходный вариант, **не** заявка на полную сборку всего Frontier из исходников. В частности, исходники в **reconstruction/** и исторический **src/Survivor/** не равнозначны установленным рабочим DLL. Их автоматическая замена в релизе запрещена до полного восстановления и игровых испытаний.

Подробности: [RELEASE_PIPELINE.md](docs/RELEASE_PIPELINE.md) · [SOURCE_PROVENANCE.md](docs/SOURCE_PROVENANCE.md).

## Безопасность и права

- Не обновляйте модпак на неподдерживаемой версии игры без проверки.
- **FrontierPack/compiled** пересоздаётся при запуске игры и не включается в ZIP.
- Приватные референсы, сохранения, учётные данные и Steam-токены не включаются в открытые исходники.
- Сам по себе статус Public не предоставляет прав на сторонние игровые материалы. Cargo Hunters и BepInEx принадлежат соответствующим правообладателям.

---

**English:** Frontier is a Cargo Hunters mod pack. Download the BepInEx-rooted archive from GitHub Releases. An existing matching BepInEx IL2CPP install is required. GitHub Actions builds three verified auxiliary plugins from source and packages 32 other verified prebuilt module DLLs until their production sources can be recovered and accepted.
