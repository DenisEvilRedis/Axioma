# Axioma

## RU

**Axioma** — визуальная новелла на Unity, созданная в рамках учебного курса **МИП-19**.

Это не просто линейный прототип с диалогами. Проект построен на системе битов с условиями ветвления, очками направлений, флагами состояния, звуковыми триггерами переходов, музыкальными cue и несколькими концовками. Основная настройка истории выполняется прямо в Unity через сцену и Inspector.

### Обзор проекта

- Жанр: визуальная новелла
- Движок: Unity 6
- Тип проекта: учебный проект
- Основной фокус: интерактивное повествование, ветвление, управление состоянием истории, музыка и звуковое сопровождение сцен

### Технологии

- Unity: `6000.4.1f1`
- Render Pipeline: `URP`
- UI: `UGUI` + `TextMeshPro`
- Input: `com.unity.inputsystem`

### Как открыть проект

1. Откройте **Unity Hub**
2. Добавьте папку проекта:
   `C:\Users\Denis\Unity Project\Axioma`
3. Откройте проект в версии Unity `6000.4.1f1`

Основная рабочая сцена:

`Assets/Scenes/StartNV.unity`

### Структура репозитория

- `Assets/Scenes` — сцены проекта
- `Assets/Scripts` — логика истории, интерфейса, меню, музыки и звуков
- `Assets/Editor` — редакторские утилиты и property drawer'ы
- `Packages` — конфигурация Unity-пакетов
- `ProjectSettings` — настройки проекта Unity
- `Fonts` — исходные шрифты проекта

Папки `Library`, `Logs`, `obj`, `Build`, `.vs` и локальные временные файлы исключены из Git.

### Архитектура истории

История хранится в сериализованных данных внутри Unity, а не во внешнем JSON-файле.

- `VNScene` хранит упорядоченный список битов
- `VNManager` собирает все биты в общую карту по `beatId`
- старт истории определяется через `startBeatId`
- переходы между узлами выполняются по явным ссылкам, правилам ветвления или через концовки

Такой подход делает сцену и Inspector основной средой редактирования сюжета.

### Система битов

Каждый узел истории представлен структурой `VNManager.BeatData`.

Бит может содержать:

- уникальный `beatId`
- режим бита
- имя говорящего и портрет
- текст реплики или повествования
- фоновое изображение
- музыкальный cue
- флаг остановки музыки
- ссылку на следующий бит
- категорию звука для перехода
- эффекты при входе

Поддерживаемые типы битов:

- `Dialogue` — показывает текст и переводит историю к следующему биту
- `Choice` — показывает варианты выбора игрока
- `Branch` — автоматически выбирает следующий бит по правилам
- `Ending` — завершает маршрут, открывает концовку и может запускать отдельную ending sequence

### Система выборов

Для выборов используется `ChoiceData[]`.

Каждый вариант выбора содержит:

- текст кнопки
- следующий бит
- необязательную категорию звука перехода
- эффекты, применяемые после выбора

Эффекты могут:

- изменять очки маршрутов
- добавлять флаги
- удалять флаги

### Ветвление и условия

Для условных переходов используется `BranchRuleData[]` и запасной путь через `defaultNextBeatId`.

Правило ветвления может проверять:

- обязательный флаг
- запрещённый флаг
- доминирующий маршрут
- минимальный отрыв одного маршрута от остальных
- минимальный `Escape`
- минимальный `Vanity`
- минимальный `Honesty`

Если ни одно правило не подошло, используется переход по умолчанию.

### Состояние истории

`VNManager` хранит текущее состояние прохождения.

Состояние включает:

- очки `Escape`
- очки `Vanity`
- очки `Honesty`
- набор активных строковых флагов
- текущий бит
- открытые концовки через `PlayerPrefs`

Также менеджер умеет вычислять:

- доминирующий путь
- отрыв одного пути от остальных

### Звук и музыка

В проекте отдельно реализованы система звуковых эффектов и система фоновой музыки.

#### Категории звуков

`AudioManager` воспроизводит звуки по `categoryId`.

Категория звука может задавать:

- идентификатор
- режим воспроизведения
- громкость
- разброс pitch
- список клипов
- tempo queue для ритмического воспроизведения

Поддерживаемые режимы:

- `Random`
- `Sequential`
- `SequentialLoop`

Переходы истории могут запускать звук через:

- `nextBeatSoundCategoryId` у бита
- `nextBeatSoundCategoryId` у выбора
- `nextBeatSoundCategoryId` у правила ветвления

Отдельно используется категория звука перелистывания страницы новеллы.

#### Музыкальные cue

`MusicManager` управляет фоновой музыкой по именованным cue.

Музыкальный cue содержит:

- идентификатор
- аудиоклип
- громкость
- длительность перехода
- режим перехода
- флаг зацикливания

Поддерживаемые переходы:

- `Crossfade`
- `FadeOutIn`

Система музыки также поддерживает:

- остановку музыки из бита
- ducking
- восстановление предыдущего воспроизведения
- отдельную обработку перехода в концовки

### Концовки

Система поддерживает три основные концовки:

- `Escape`
- `Vanity`
- `Honesty`

При наличии нужных данных проект может запускать отдельную `EndingSequencePlayer`, а не только статический финальный экран.

### Ключевые скрипты

- `VNManager.cs` — основной контроллер истории, переходов, условий, выборов и концовок
- `VNScene.cs` — контейнер битов в сцене
- `VNDialogAuto.cs` — авто-режим чтения
- `MenuManager.cs` — меню и навигация
- `AudioManager.cs` — категории звуков и их воспроизведение
- `MusicManager.cs` — музыкальные cue и переходы между ними
- `EndingSequencePlayer.cs` — воспроизведение концовок
- `EndingScoreDebugOverlay.cs` — отладочный оверлей очков маршрутов

### Примечания по редактированию

- Основная рабочая сцена: `Assets/Scenes/StartNV.unity`
- Значительная часть истории редактируется через Inspector
- Изменения сюжета могут затрагивать не только `.cs`, но и `.unity`-данные сцены
- Каждый `beatId` должен быть уникальным
- `VNManager.RebuildBeatMap()` предупреждает о дубликатах

## EN

**Axioma** is a Unity-based visual novel created as part of the **MIP-19** academic course project.

This is not a simple linear dialogue prototype. The project is built around a beat-based narrative system with branching conditions, route scores, state flags, transition sound triggers, music cues, and multiple endings. Most story authoring is done directly in Unity through the scene and Inspector.

### Project Overview

- Genre: visual novel
- Engine: Unity 6
- Project type: educational course project
- Core focus: interactive storytelling, branching flow, story-state management, music, and scene-based presentation

### Tech Stack

- Unity: `6000.4.1f1`
- Render Pipeline: `URP`
- UI: `UGUI` + `TextMeshPro`
- Input: `com.unity.inputsystem`

### Opening The Project

1. Open **Unity Hub**
2. Add the project folder:
   `C:\Users\Denis\Unity Project\Axioma`
3. Open the project with Unity version `6000.4.1f1`

Main working scene:

`Assets/Scenes/StartNV.unity`

### Repository Structure

- `Assets/Scenes` — project scenes
- `Assets/Scripts` — story, UI, menu, music, and audio logic
- `Assets/Editor` — editor utilities and custom drawers
- `Packages` — Unity package configuration
- `ProjectSettings` — Unity project settings
- `Fonts` — source font assets

Folders such as `Library`, `Logs`, `obj`, `Build`, `.vs`, and local scratch files are excluded from Git.

### Story Architecture

The narrative is authored as serialized Unity data rather than an external JSON dialogue pipeline.

- `VNScene` stores an ordered list of beats
- `VNManager` rebuilds all beats into a global map by `beatId`
- the novel starts from a configured `startBeatId`
- transitions are resolved through explicit links, branch rules, or ending nodes

This makes the Unity scene and Inspector the primary authoring environment for narrative content.

### Beat System

Each story node is represented by `VNManager.BeatData`.

A beat can include:

- unique `beatId`
- beat mode
- speaker name and portrait
- dialogue or narration text
- background sprite
- music cue
- stop-music flag
- next beat target
- transition sound category
- enter effects

Supported beat modes:

- `Dialogue` — displays text and advances to the next beat
- `Choice` — displays player choices
- `Branch` — automatically resolves the next beat through rule evaluation
- `Ending` — completes the route, unlocks an ending, and can launch a dedicated ending sequence

### Choice System

Choices use `ChoiceData[]`.

Each choice contains:

- button label
- next beat target
- optional transition sound category
- effects applied after selection

Effects can:

- change route scores
- add flags
- remove flags

### Branching And Conditions

Conditional branching uses `BranchRuleData[]` together with a fallback path through `defaultNextBeatId`.

A branch rule can check:

- required flag
- forbidden flag
- dominant route
- minimum lead over the other routes
- minimum `Escape`
- minimum `Vanity`
- minimum `Honesty`

If no rule matches, the system falls back to the default target.

### Story State

`VNManager` stores the current playthrough state.

The state includes:

- `Escape` score
- `Vanity` score
- `Honesty` score
- active string flags
- current beat
- unlocked endings stored through `PlayerPrefs`

The manager can also calculate:

- dominant route
- route lead over the alternatives

### Audio And Music

The project uses separate systems for sound effects and background music.

#### Sound Categories

`AudioManager` plays audio by `categoryId`.

A sound category can define:

- identifier
- playback mode
- volume
- pitch variation
- clip list
- tempo queue behavior

Supported playback modes:

- `Random`
- `Sequential`
- `SequentialLoop`

Story transitions can trigger audio through:

- `nextBeatSoundCategoryId` on a beat
- `nextBeatSoundCategoryId` on a choice
- `nextBeatSoundCategoryId` on a branch rule result

There is also a dedicated page-turn sound category for novel progression.

#### Music Cues

`MusicManager` controls background music through named cues.

A music cue contains:

- identifier
- audio clip
- volume
- transition duration
- transition mode
- loop flag

Supported transitions:

- `Crossfade`
- `FadeOutIn`

The music system also supports:

- stopping music from a beat
- ducking
- restoring suspended playback
- dedicated ending transition handling

### Endings

The project supports three main endings:

- `Escape`
- `Vanity`
- `Honesty`

When the required data is available, the project can launch `EndingSequencePlayer` instead of falling back to a static final state.

### Key Scripts

- `VNManager.cs` — central controller for story traversal, branching, conditions, choices, and endings
- `VNScene.cs` — serialized beat container
- `VNDialogAuto.cs` — auto-reading mode
- `MenuManager.cs` — menu and navigation flow
- `AudioManager.cs` — sound category playback
- `MusicManager.cs` — music cues and transitions
- `EndingSequencePlayer.cs` — ending playback
- `EndingScoreDebugOverlay.cs` — debug overlay for route score inspection

### Authoring Notes

- Main working scene: `Assets/Scenes/StartNV.unity`
- A significant part of the story is edited through the Inspector
- Narrative updates may affect both `.cs` files and serialized `.unity` scene data
- Every `beatId` should be unique
- `VNManager.RebuildBeatMap()` warns about duplicate beat IDs
