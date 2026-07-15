# Project Manager (Nugetsupdate)

[Deutsch](#deutsch) | [English](#english) | [Русский](#русский)

<a id="deutsch"></a>

## Deutsch

Project Manager ist eine WPF-Desktopanwendung für Windows, die den typischen Release-Ablauf einer .NET-Solution in einer Oberfläche zusammenführt. Sie prüft und ändert NuGet-Paketversionen, synchronisiert Projektversionen, baut die Solution und veröffentlicht Änderungen sowie Release-Tags in Git.

### Funktionen

- Öffnen von Visual-Studio-Solutions (`.sln`) und automatische Erkennung der enthaltenen C#-Projekte (`.csproj`);
- Anzeige der aktuellen Version jedes Projekts und der gemeinsamen Solution-Version (`Mixed`, wenn sich die Versionen unterscheiden);
- Prüfung der direkten NuGet-Abhängigkeiten mit automatischer Erkennung aller Target Frameworks eines Projekts;
- Ermittlung stabiler Paketversionen aus den konfigurierten NuGet-V2/V3-Quellen;
- Auswahl jeder verfügbaren stabilen Paketversion, einschließlich Zwischenversionen und älterer Versionen für Downgrades;
- separate Zielversion je eindeutigem NuGet-Paket und Übernahme in alle Projekte der Solution, die dieses Paket referenzieren;
- gruppierte Zuweisung einer Paketversion anhand von Präfix, Teilname oder exaktem Paketnamen;
- sofortige Filterung der Paketliste während der Eingabe eines Suchmusters;
- Auswahl einzelner Updates, aller verfügbaren Updates oder Aufhebung der gesamten Auswahl;
- Anwendung der gewählten Paketversionen über `dotnet add package`;
- einheitliche Aktualisierung von `Version`, `FileVersion` und `AssemblyVersion` in allen Projekten der Solution;
- Restore und Release-Build der Solution über die konfigurierte `MSBuild.exe`;
- Ausführung von `git add`, Erstellung eines Commits und Push der aktuellen Branch;
- Erstellung eines Git-Tags anhand eines konfigurierbaren Musters wie `v_{version}` und Push nach `origin`;
- Workflow-Protokoll mit Befehlsausgaben, Exitcodes und Fehlermeldungen;
- grafische Projektstatus `Pending`, `Working`, `Completed` und `Failed` während der Prüfung und Anwendung von Versionen;
- veränderbare Aufteilung zwischen Projektliste und Pakettabelle;
- persistente Speicherung des MSBuild-Pfads und der NuGet-Quellen.

### Voraussetzungen

- Windows 10/11;
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (das Repository verwendet SDK `8.0.422` und erlaubt den Roll-forward auf ein neueres Feature Band);
- Visual Studio 2022 oder Visual Studio Build Tools mit MSBuild;
- PowerShell 5.1 (`powershell.exe`);
- Git im `PATH`;
- Zugriff auf die verwendeten NuGet-Quellen und das Git-Repository.

Eine Solution darf gleichzeitig .NET-Framework-Projekte, .NET-8-Projekte und Multi-Target-Projekte enthalten. Die Anwendung verarbeitet für jede `.csproj`-Datei die von `dotnet` zurückgegebenen Target Frameworks.

Die Projekte müssen `PackageReference` verwenden und mit `dotnet list package` sowie `dotnet add package` kompatibel sein. Das klassische Paketformat `packages.config` wird nicht unterstützt. In der Regel handelt es sich daher um SDK-Style-Projekte; klassische `.csproj`-Dateien können ebenfalls funktionieren, sofern sie `PackageReference` verwenden und mit der `dotnet` CLI kompatibel sind.

### Build und Start

Im Stammverzeichnis des Repositorys ausführen:

```powershell
dotnet restore ProjectManager.sln
dotnet build ProjectManager.sln -c Release
dotnet run --project ProjectManager.App\ProjectManager.App.csproj
```

Der Release-Build wird unter `ProjectManager.App\bin\Release\net8.0-windows` erstellt.

### Verwendung

1. Anwendung starten, **Open solution** wählen und eine `.sln`-Datei öffnen.
2. **Settings** öffnen:
   - vollständigen Pfad zur `MSBuild.exe` eintragen;
   - benötigte NuGet-V2/V3-Feed-URLs hinzufügen und speichern.
3. **Check updates** wählen. Die Projektliste zeigt grafisch, welches Projekt gerade geprüft wird, welche Projekte abgeschlossen sind und wo Fehler aufgetreten sind. Für das ausgewählte Projekt erscheinen die aktuellen und neuesten stabilen Paketversionen.
4. **Package targets...** öffnen. Im oberen Bereich Pakete über `Prefix`, `Contains` oder `Exact name` gruppieren. Die Tabelle wird bereits während der Eingabe des Patterns gefiltert. Eine gemeinsame verfügbare Version auswählen, **Set group target** wählen, die Ziele prüfen und anschließend mit **Apply targets** übernehmen.
5. Bei Bedarf einzelne Ziele direkt in der Haupttabelle ändern, die gewünschten Zeilen markieren oder **Use all** wählen und danach **Apply versions** ausführen. Auch dabei wird der Bearbeitungsstatus pro Projekt angezeigt.
6. Optional eine gemeinsame Projektversion eingeben und **Set version** wählen.
7. Mit **Build** Restore und Release-Build über MSBuild starten.
8. Commit-Nachricht eingeben und **Commit** wählen. Die Anwendung fügt alle Änderungen hinzu, erstellt den Commit und führt `git push` aus.
9. Tag-Muster prüfen und **Create tag** wählen, um den aufgelösten Tag zu erstellen und nach `origin` zu pushen.

Jeder Arbeitsschritt wird separat gestartet. Dadurch können Workflow-Protokoll und Working Tree vor einem Commit oder der Veröffentlichung eines Tags kontrolliert werden.

### Einstellungen

Die Einstellungen werden im Profil des aktuellen Windows-Benutzers gespeichert:

```text
%APPDATA%\ProjectManager\nuget-sources.json
```

Der voreingestellte MSBuild-Pfad lautet:

```text
C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe
```

Wenn eine andere Visual-Studio-Edition oder nur Build Tools installiert ist, muss dieser Pfad unter **Settings** angepasst werden.

### Funktionsweise der Paketaktualisierung

Die Anwendung führt für jedes Projekt folgende Schritte aus:

1. Abhängigkeiten und alle Target Frameworks mit `dotnet list <project> package --include-transitive --format json` ermitteln;
2. die direkten Abhängigkeiten aus dem Ergebnis verarbeiten;
3. verfügbare Versionen direkt aus den konfigurierten NuGet-V2/V3-Quellen abfragen;
4. Prerelease-Versionen ausschließen, einschließlich Versionen mit einem `-`-Suffix oder `alpha`;
5. die ersten beiden numerischen Komponenten einzeln vergleichen und bei unterschiedlich langen Versionsnummern die restlichen Komponenten als eine Zahl ohne Punkte behandeln. Daher ist `2310.0.4.12` neuer als `221.10105.1` und `231.1.2.37` neuer als `231.1.17`;
6. standardmäßig die neueste stabile Version vorschlagen und die vollständige verfügbare stabile Historie, einschließlich älterer Versionen, in der Liste **Target** anbieten;
7. das gewählte Ziel mit `dotnet add <project> package <name> --version <version>` anwenden.

Ohne konfigurierte oder erreichbare NuGet-Quellen kann die Anwendung keine verfügbaren Versionen ermitteln. Private Feeds müssen ohne interaktive Anmeldung erreichbar oder bereits in der Umgebung authentifiziert sein.

Wird eine Version unterhalb der installierten Version gewählt, führt die Anwendung ein Downgrade aus. Dies funktioniert sowohl für eine einzelne Projektzeile als auch über **Package targets...** für alle referenzierenden Projekte. Die Kompatibilität wird erst durch den anschließenden Restore beziehungsweise Build geprüft; eine automatische Kompatibilitätsanalyse vor der Änderung findet nicht statt.

### Git-Operationen

**Commit** führt nacheinander aus:

```powershell
git add -A
git commit -m "<Nachricht>"
git push
```

**Create tag** führt aus:

```powershell
git tag "<Tag>"
git push origin "<Tag>"
```

Als Repository-Verzeichnis gilt der Ordner der ausgewählten `.sln`-Datei. Vor diesen Aktionen sollten die aktuelle Branch, der konfigurierte Remote und alle Änderungen im Working Tree geprüft werden.

### Projektstruktur

```text
ProjectManager.sln
└── ProjectManager.App
    ├── Models          # Modelle für Projekte, Pakete, Einstellungen und Prozessergebnisse
    ├── Services        # Solution-Analyse, NuGet, Versionen, MSBuild, Git und Einstellungen
    ├── ViewModels      # UI-Zustand und Steuerung des Workflows
    ├── MainWindow.*    # Hauptfenster der Anwendung
    └── NuGetSourcesWindow.* # Einstellungen für MSBuild und NuGet-Feeds
```

Die Anwendung basiert auf .NET 8, WPF und MVVM und verwendet keine Drittanbieterbibliotheken.

### Aktuelle Einschränkungen

- unterstützt werden nur C#-Projekte (`.csproj`), die direkt in einer klassischen `.sln` aufgeführt sind;
- unterstützt werden nur `PackageReference`-basierte Projekte, die mit der `dotnet` CLI kompatibel sind; `packages.config` wird nicht unterstützt;
- unterschiedliche Versionen desselben Pakets in mehreren Target Frameworks eines Projekts werden derzeit nach Paketname zusammengefasst;
- aktualisiert werden nur direkte `PackageReference`-Abhängigkeiten, obwohl die Abfrage auch transitive Abhängigkeiten enthält;
- Prerelease-Versionen werden nicht angeboten;
- Git-Operationen erfassen mit `git add -A` alle Änderungen im Working Tree;
- bei Fehlern während Build, Push oder Tag-Erstellung erfolgt kein automatischer Rollback;
- erwartet wird ein reguläres Git-Repository mit konfiguriertem `origin` und gültigen Zugangsdaten.

### Technologien

- C# / .NET 8
- WPF
- MVVM
- `dotnet` CLI
- MSBuild
- PowerShell
- Git
<a id="english"></a>

## English

Project Manager is a Windows desktop WPF application that brings the typical release workflow for a .NET solution into a single interface. It can check and update NuGet packages, synchronize project versions, build the solution, and publish Git changes and release tags.

### Features

- opens Visual Studio solutions (`.sln`) and automatically discovers their C# projects (`.csproj`);
- displays the current version of every project and the common solution version (`Mixed` when versions differ);
- checks top-level NuGet dependencies while automatically detecting each project's target frameworks;
- finds the latest stable package versions in configured NuGet V2/V3 sources;
- allows any available stable package version to be selected, including intermediate and older versions;
- selects a separate target version for every unique NuGet package and applies it across all projects that reference that package;
- assigns a shared target version to package groups matched by prefix, partial name, or exact name;
- supports selecting individual updates, selecting all available updates, and clearing the selection;
- applies selected versions through `dotnet add package`;
- writes the same `Version`, `FileVersion`, and `AssemblyVersion` to every project in the solution;
- restores packages and builds the solution in Release mode using the configured `MSBuild.exe`;
- stages all changes, creates a Git commit, and pushes the current branch;
- creates a Git tag from a configurable pattern such as `v_{version}` and pushes it to `origin`;
- provides a workflow log containing command output, exit codes, and error messages;
- shows color-coded `Pending`, `Working`, `Completed`, and `Failed` badges beside projects while checking or applying versions;
- persists the MSBuild path and NuGet source list between application sessions.

### Requirements

- Windows 10/11;
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (the repository pins SDK `8.0.422` and allows rolling forward to a newer feature band);
- Visual Studio 2022 or Visual Studio Build Tools with MSBuild;
- PowerShell 5.1 (`powershell.exe`);
- Git available through `PATH`;
- access to the required NuGet sources and Git repository.

A single solution may contain .NET Framework projects, .NET 8 projects, and projects targeting multiple frameworks. For every `.csproj`, the application uses the frameworks returned by `dotnet`.

Projects must use `PackageReference` and be readable by `dotnet list package` and `dotnet add package`. The legacy `packages.config` package-management format is not supported. This normally means SDK-style projects, although traditional `.csproj` files may also work when they use `PackageReference` and are compatible with the `dotnet` CLI.

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
4. Open **Package targets...**. In the upper section, match a package group by `Prefix`, `Contains`, or `Exact name`; the main table filters immediately as the pattern is entered. Choose a version common to every match and select **Set group target**, then review the targets and select **Apply targets**.
5. If necessary, adjust individual rows in the main table. Select the required updates or choose **Use all**, then select **Apply versions**.
6. If required, enter a common project version and select **Set version**.
7. Select **Build** to restore packages and build the solution in Release mode through MSBuild.
8. Enter a commit message and select **Commit**. The application stages all changes, creates a commit, and runs `git push`.
9. Review the tag pattern and select **Create tag** to create the resolved tag and push it to `origin`.

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
5. compares the first two numeric components independently and, for differently sized versions, treats the remaining components as one number without dots (therefore `2310.0.4.12` is newer than `221.10105.1`, while `231.1.2.37` is newer than `231.1.17`);
6. proposes the latest stable version by default and adds the complete available stable history, including older versions, to the **Target** list;
7. applies the selected target with `dotnet add <project> package <name> --version <version>`.

The application cannot determine new versions when no NuGet sources are configured or when the configured sources are unavailable. Private feeds must either be accessible without interactive authentication or already be authenticated in the environment.

Selecting a version below the installed version performs a downgrade. Downgrades are supported for an individual project row and through **Package targets...** for every project referencing the selected package. Compatibility is validated by the subsequent restore/build; the application does not analyze compatibility before applying the version.

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
- only `PackageReference`-based projects compatible with the `dotnet` CLI are supported; `packages.config` projects are not supported;
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

<a id="русский"></a>

## Русский

Project Manager — настольное WPF-приложение для Windows, которое помогает провести типовой релизный цикл для .NET-решения из одного интерфейса: проверить версии NuGet-пакетов, применить обновления, синхронизировать версию проектов, собрать решение и опубликовать изменения и тег в Git.

## Возможности

- открытие Visual Studio-решений (`.sln`) и автоматический поиск входящих в них C#-проектов (`.csproj`);
- отображение текущей версии каждого проекта и общей версии решения (`Mixed`, если версии различаются);
- проверка верхнеуровневых NuGet-зависимостей с автоматическим определением целевых фреймворков каждого проекта;
- поиск актуальных стабильных версий пакетов в настроенных NuGet V2/V3-источниках;
- выбор любой доступной стабильной версии пакета, включая промежуточные и более старые версии;
- отдельный выбор целевой версии для каждого уникального NuGet-пакета и её установка во всех проектах solution, которые используют этот пакет;
- групповое назначение версии пакетам по префиксу, части имени или точному имени;
- выбор обновлений отдельно, выбор всех доступных обновлений или очистка выбора;
- применение выбранных версий через `dotnet add package`;
- единая запись `Version`, `FileVersion` и `AssemblyVersion` во все проекты решения;
- восстановление пакетов и Release-сборка решения через заданный `MSBuild.exe`;
- выполнение `git add`, создание коммита и отправка текущей ветки;
- создание Git-тега по настраиваемому шаблону, например `v_{version}`, и отправка тега в `origin`;
- журнал этапов с выводом команд, кодами завершения и сообщениями об ошибках;
- цветные статусы `Pending`, `Working`, `Completed` и `Failed` рядом с проектами во время проверки и применения версий;
- сохранение пути к MSBuild и списка NuGet-источников между запусками.

## Требования

- Windows 10/11;
- [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0) (проект закрепляет SDK `8.0.422` с переходом на более новый feature band);
- Visual Studio 2022 или Build Tools с MSBuild;
- PowerShell 5.1 (`powershell.exe`);
- Git, доступный через `PATH`;
- доступ к используемым NuGet-источникам и Git-репозиторию.

В одном solution могут одновременно находиться проекты для .NET Framework, .NET 8 и проекты с несколькими целевыми фреймворками. Для каждого `.csproj` приложение использует фреймворки, возвращённые `dotnet`.

Проекты должны использовать `PackageReference` и корректно обрабатываться командами `dotnet list package` и `dotnet add package`. Классический формат управления пакетами `packages.config` не поддерживается. Это обычно означает SDK-style-проекты, однако также могут работать классические `.csproj`, если они используют `PackageReference` и совместимы с `dotnet` CLI.

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
4. Откройте **Package targets...**. В верхнем блоке можно найти группу пакетов по `Prefix`, `Contains` или `Exact name`; основная таблица фильтруется сразу при вводе pattern. Выберите общую доступную версию и нажмите **Set group target**. Проверьте цели и нажмите **Apply targets**.
5. При необходимости скорректируйте отдельные строки в основной таблице, отметьте нужные обновления или нажмите **Use all**, затем **Apply versions**.
6. При необходимости задайте общую версию проектов и нажмите **Set version**.
7. Нажмите **Build**, чтобы выполнить Restore и Release-сборку через MSBuild.
8. Укажите сообщение и нажмите **Commit**. Приложение выполнит добавление всех изменений, коммит и `git push`.
9. Проверьте шаблон тега и нажмите **Create tag**, чтобы создать тег и отправить его в `origin`.

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
5. сравнивает первые два числовых компонента по отдельности, а оставшуюся часть версий разной длины — как единое число без точек (поэтому `2310.0.4.12` новее `221.10105.1`, а `231.1.2.37` новее `231.1.17`);
6. предлагает новейшую найденную стабильную версию по умолчанию и добавляет всю доступную стабильную историю, включая старые версии, в список **Target**;
7. применяет выбранную целевую версию командой `dotnet add <project> package <name> --version <version>`.

Если NuGet-источники не настроены или недоступны, приложение не сможет определить новые версии. Адреса приватных feeds должны быть доступны без дополнительной интерактивной авторизации либо уже настроены в окружении.

Выбор версии ниже текущей выполняет downgrade. Он поддерживается как для отдельной строки проекта, так и через **Package targets...** для всех проектов, использующих выбранный пакет. Совместимость старой версии проверяется последующим restore/build; автоматического анализа совместимости до применения нет.

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
- поддерживаются только проекты на основе `PackageReference`, совместимые с `dotnet` CLI; проекты с `packages.config` не поддерживаются;
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
