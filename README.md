# Project Manager (Nugetsupdate)

[Русский](#русский) | [English](#english)

<a id="русский"></a>

## Русский

Project Manager — настольное WPF-приложение для Windows, которое помогает провести типовой релизный цикл для .NET-решения из одного интерфейса: проверить версии NuGet-пакетов, применить обновления, синхронизировать версию проектов, собрать решение и опубликовать изменения и тег в Git.

## Возможности

- открытие Visual Studio-решений (`.sln`) и автоматический поиск входящих в них C#-проектов (`.csproj`);
- отображение текущей версии каждого проекта и общей версии решения (`Mixed`, если версии различаются);
- проверка верхнеуровневых NuGet-зависимостей с автоматическим определением целевых фреймворков каждого проекта;
- поиск актуальных стабильных версий пакетов в настроенных NuGet V2/V3-источниках;
- выбор любой стабильной целевой версии между установленной и новейшей доступной;
- выбор обновлений отдельно, выбор всех доступных обновлений или очистка выбора;
- применение выбранных версий через `dotnet add package`;
- единая запись `Version`, `FileVersion` и `AssemblyVersion` во все проекты решения;
- восстановление пакетов и Release-сборка решения через заданный `MSBuild.exe`;
- выполнение `git add`, создание коммита и отправка текущей ветки;
- создание Git-тега по настраиваемому шаблону, например `v_{version}`, и отправка тега в `origin`;
- журнал этапов с выводом команд, кодами завершения и сообщениями об ошибках;
- сохранение пути к MSBuild и списка NuGet-источников между запусками.

## Требования

- Windows 10/11;
- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0) (проект закрепляет SDK `8.0.422` с переходом на более новый feature band);
- Visual Studio 2022 или Build Tools с MSBuild;
- PowerShell 5.1 (`powershell.exe`);
- Git, доступный через `PATH`;
- доступ к используемым NuGet-источникам и Git-репозиторию.

В одном solution могут одновременно находиться проекты для .NET Framework, .NET 8 и проекты с несколькими целевыми фреймворками. Для каждого `.csproj` приложение использует фреймворки, возвращённые `dotnet`.

## Сборка и запуск

Из корня репозитория выполните:

```powershell
dotnet restore ProjectManager.sln
dotnet build ProjectManager.sln -c Release
dotnet run --project ProjectManager.App\ProjectManager.App.csproj
```

Готовое приложение будет собрано в `ProjectManager.App\bin\Release\net8.0-windows`.

## Использование

1. Запустите приложение и нажмите **Open solution**, затем выберите `.sln`.
2. Откройте **Settings**:
   - укажите полный путь к `MSBuild.exe`;
   - добавьте адреса NuGet V2/V3 feeds и сохраните настройки.
3. Нажмите **Check updates**. Для выбранного в дереве проекта появится таблица пакетов с текущей и последней стабильной версией.
4. В колонке **Target** выберите для каждого пакета новейшую или одну из промежуточных стабильных версий. Отметьте нужные строки или нажмите **Use all**, затем **Apply versions**.
5. При необходимости задайте общую версию и нажмите **Set version**.
6. Нажмите **Build**, чтобы выполнить Restore и Release-сборку через MSBuild.
7. Укажите сообщение и нажмите **Commit**. Приложение выполнит добавление всех изменений, коммит и `git push`.
8. Проверьте шаблон тега и нажмите **Create tag**, чтобы создать тег и отправить его в `origin`.

Каждый этап запускается отдельно. Это позволяет проверить журнал и состояние рабочего дерева перед коммитом или публикацией тега.

## Настройки

Настройки хранятся в профиле текущего пользователя:

```text
%APPDATA%\ProjectManager\nuget-sources.json
```

По умолчанию приложение использует путь:

```text
C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe
```

Если установлена другая редакция Visual Studio или только Build Tools, путь необходимо изменить в окне **Settings**.

## Как работает обновление пакетов

Для каждого проекта приложение:

1. получает список зависимостей и всех целевых фреймворков командой `dotnet list <project> package --include-transitive --format json`;
2. обрабатывает верхнеуровневые зависимости из результата;
3. запрашивает доступные версии напрямую у настроенных V2/V3 NuGet-источников;
4. исключает prerelease-версии (версии с суффиксом через `-`, включая `alpha`);
5. предлагает новейшую найденную стабильную версию и добавляет все более новые стабильные версии в список **Target**;
6. применяет выбранную целевую версию командой `dotnet add <project> package <name> --version <version>`.

Если NuGet-источники не настроены или недоступны, приложение не сможет определить новые версии. Адреса приватных feeds должны быть доступны без дополнительной интерактивной авторизации либо уже настроены в окружении.

## Git-операции

Кнопка **Commit** последовательно выполняет:

```powershell
git add -A
git commit -m "<сообщение>"
git push
```

Кнопка **Create tag** выполняет:

```powershell
git tag "<тег>"
git push origin "<тег>"
```

Каталогом репозитория считается папка, в которой находится выбранный `.sln`. Перед запуском этих действий рекомендуется проверить текущую ветку, remote и состав изменений.

## Структура проекта

```text
ProjectManager.sln
└── ProjectManager.App
    ├── Models          # модели проектов, пакетов, настроек и результатов процессов
    ├── Services        # анализ solution, NuGet, версии, MSBuild, Git и настройки
    ├── ViewModels      # состояние интерфейса и orchestration рабочего процесса
    ├── MainWindow.*    # главное окно приложения
    └── NuGetSourcesWindow.* # настройки MSBuild и NuGet feeds
```

Приложение построено на .NET 8, WPF и шаблоне MVVM без сторонних библиотек.

## Текущие ограничения

- поддерживаются только C#-проекты `.csproj`, напрямую перечисленные в классическом `.sln`;
- пакеты с разными версиями в разных целевых фреймворках одного проекта сейчас объединяются по имени пакета;
- обновляются только верхнеуровневые `PackageReference`, даже если команда получения списка включает транзитивные зависимости;
- prerelease-версии не предлагаются;
- Git-команды применяются ко всем изменениям в рабочем дереве (`git add -A`);
- автоматического отката изменений при ошибке сборки, push или создания тега нет;
- приложение рассчитано на обычный Git-репозиторий с настроенным `origin` и учетными данными.

## Технологии

- C# / .NET 8
- WPF
- MVVM
- `dotnet` CLI
- MSBuild
- PowerShell
- Git

---

<a id="english"></a>

## English

Project Manager is a Windows desktop WPF application that brings the typical release workflow for a .NET solution into a single interface. It can check and update NuGet packages, synchronize project versions, build the solution, and publish Git changes and release tags.

### Features

- opens Visual Studio solutions (`.sln`) and automatically discovers their C# projects (`.csproj`);
- displays the current version of every project and the common solution version (`Mixed` when versions differ);
- checks top-level NuGet dependencies while automatically detecting each project's target frameworks;
- finds the latest stable package versions in configured NuGet V2/V3 sources;
- allows any stable target version between the installed and latest available version to be selected;
- supports selecting individual updates, selecting all available updates, and clearing the selection;
- applies selected versions through `dotnet add package`;
- writes the same `Version`, `FileVersion`, and `AssemblyVersion` to every project in the solution;
- restores packages and builds the solution in Release mode using the configured `MSBuild.exe`;
- stages all changes, creates a Git commit, and pushes the current branch;
- creates a Git tag from a configurable pattern such as `v_{version}` and pushes it to `origin`;
- provides a workflow log containing command output, exit codes, and error messages;
- persists the MSBuild path and NuGet source list between application sessions.

### Requirements

- Windows 10/11;
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (the repository pins SDK `8.0.422` and allows rolling forward to a newer feature band);
- Visual Studio 2022 or Visual Studio Build Tools with MSBuild;
- PowerShell 5.1 (`powershell.exe`);
- Git available through `PATH`;
- access to the required NuGet sources and Git repository.

A single solution may contain .NET Framework projects, .NET 8 projects, and projects targeting multiple frameworks. For every `.csproj`, the application uses the frameworks returned by `dotnet`.

### Build and run

Run the following commands from the repository root:

```powershell
dotnet restore ProjectManager.sln
dotnet build ProjectManager.sln -c Release
dotnet run --project ProjectManager.App\ProjectManager.App.csproj
```

The Release build is written to `ProjectManager.App\bin\Release\net8.0-windows`.

### Usage

1. Start the application, select **Open solution**, and choose a `.sln` file.
2. Open **Settings**:
   - enter the full path to `MSBuild.exe`;
   - add the required NuGet V2/V3 feed URLs and save the settings.
3. Select **Check updates**. The table for the selected project displays each package with its current and latest stable version.
4. In the **Target** column, choose the latest or any intermediate stable version for each package. Select individual rows or choose **Use all**, then select **Apply versions**.
5. If required, enter a common solution version and select **Set version**.
6. Select **Build** to restore packages and build the solution in Release mode through MSBuild.
7. Enter a commit message and select **Commit**. The application stages all changes, creates a commit, and runs `git push`.
8. Review the tag pattern and select **Create tag** to create the resolved tag and push it to `origin`.

Each stage is started separately, allowing you to review the workflow log and working tree before committing changes or publishing a tag.

### Settings

Settings are stored in the current Windows user profile:

```text
%APPDATA%\ProjectManager\nuget-sources.json
```

The default MSBuild path is:

```text
C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe
```

If a different Visual Studio edition or Build Tools is installed, update this path in **Settings**.

### How package updates work

For every project, the application:

1. retrieves its dependency list and all target frameworks with `dotnet list <project> package --include-transitive --format json`;
2. processes the top-level dependencies returned by that command;
3. requests available versions directly from the configured NuGet V2/V3 sources;
4. excludes prerelease versions, including any version containing a `-` suffix or `alpha`;
5. proposes the latest stable version and adds every newer stable version to the **Target** list;
6. applies the selected target with `dotnet add <project> package <name> --version <version>`.

The application cannot determine new versions when no NuGet sources are configured or when the configured sources are unavailable. Private feeds must either be accessible without interactive authentication or already be authenticated in the environment.

### Git operations

The **Commit** action runs:

```powershell
git add -A
git commit -m "<message>"
git push
```

The **Create tag** action runs:

```powershell
git tag "<tag>"
git push origin "<tag>"
```

The directory containing the selected `.sln` is treated as the repository directory. Before using these actions, verify the current branch, configured remote, and working-tree changes.

### Project structure

```text
ProjectManager.sln
└── ProjectManager.App
    ├── Models          # projects, packages, settings, and process result models
    ├── Services        # solution analysis, NuGet, versions, MSBuild, Git, and settings
    ├── ViewModels      # UI state and workflow orchestration
    ├── MainWindow.*    # main application window
    └── NuGetSourcesWindow.* # MSBuild and NuGet feed settings
```

The application is built with .NET 8, WPF, and the MVVM pattern without third-party libraries.

### Current limitations

- only C# `.csproj` files directly listed in a traditional `.sln` file are supported;
- packages using different versions across target frameworks in the same project are currently grouped by package name;
- only top-level `PackageReference` dependencies are updated, even though the listing command includes transitive dependencies;
- prerelease versions are not offered;
- Git operations stage every working-tree change with `git add -A`;
- changes are not rolled back automatically when the build, push, or tag creation fails;
- the application expects a regular Git repository with a configured `origin` and valid credentials.

### Technology stack

- C# / .NET 8
- WPF
- MVVM
- `dotnet` CLI
- MSBuild
- PowerShell
- Git
