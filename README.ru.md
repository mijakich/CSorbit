# CSorbit

[English](README.md) | [Русский](README.ru.md)

CSorbit — порт на C# библиотеки событий [Orbit](https://github.com/MeteorDevelopment/orbit) от Meteor Development. Библиотека поддерживает синхронную доставку событий, подписки через атрибуты и делегаты, приоритеты и отмену событий.

Основная платформа — **Godot 3.6.3 Mono**. Также поддерживаются Godot 4 .NET, Unity и приложения .NET. Сборка и пространство имён называются `CSorbit`. Внешних зависимостей во время выполнения нет.

## Установка

Скачайте архив для своей платформы со страницы **Releases** этого репозитория или [соберите библиотеку из исходников](#сборка-из-исходников).

| Платформа | Файл релиза | Целевая платформа DLL |
| --- | --- | --- |
| Godot 3.6.3 Mono | `CSorbit-godot3.zip` | `netstandard2.0` |
| Godot 4 .NET с .NET 8 или новее | `CSorbit-godot4.zip` | `net8.0` |
| Unity с совместимым профилем .NET Standard | `CSorbit-unity.zip` | `netstandard2.0` |
| Приложения .NET | `CSorbit-dotnet.zip` | `netstandard2.0`, `netstandard2.1`, `net8.0` |

Распакуйте подходящий `CSorbit.dll`. В архивах также находятся XML-документация, отладочные символы, README на обоих языках и лицензия. Каждый релиз содержит пакет `CSorbit.<version>.nupkg`, который можно подключить через локальный источник NuGet.

В C#-проекте скопируйте DLL в `libs/CSorbit.dll` и добавьте ссылку в `.csproj`:

```xml
<ItemGroup>
  <Reference Include="CSorbit">
    <HintPath>libs/CSorbit.dll</HintPath>
    <Private>true</Private>
  </Reference>
</ItemGroup>
```

Выбирайте DLL, совместимую с целевой платформой приложения. Вместо DLL можно добавить `ProjectReference` на `src/CSorbit/CSorbit.csproj`.

## Быстрый старт

```csharp
using CSorbit;

public sealed class DamageEvent : CancellableEvent
{
    public int Amount { get; }
    public DamageEvent(int amount) { Amount = amount; }
}

public sealed class DamageGuard
{
    [EventHandler(EventPriority.High)]
    private void OnDamage(DamageEvent value)
    {
        if (value.Amount > 50)
            value.Cancel();
    }
}

public static class Program
{
    public static void Main()
    {
        var bus = new EventBus();
        var guard = new DamageGuard();
        bus.Subscribe(guard);
        var listener = bus.Subscribe<DamageEvent>(value =>
            System.Console.WriteLine("Damage: " + value.Amount));

        bus.Post(new DamageEvent(10)); // Выведет Damage: 10.
        var blocked = bus.Post(new DamageEvent(100));
        System.Console.WriteLine(blocked.IsCancelled); // True; listener не вызван.

        bus.Unsubscribe(listener);
        bus.Unsubscribe(guard);
    }
}
```

Метод с атрибутом должен возвращать `void` и принимать ровно один параметр ссылочного типа. Поддерживаются закрытые, унаследованные и статические методы. `Subscribe(instance)` находит обработчики экземпляра и статические обработчики; `Subscribe(typeof(MyHandlers))` — только статические. Подписка через делегат возвращает слушателя, который можно передать в `Unsubscribe`.

## Поведение событий

- `Post` синхронно вызывает обработчики на вызывающем потоке и возвращает тот же объект события.
- Доставка определяется **точным типом объекта во время выполнения**. Подписки на базовый класс или интерфейс не получают производные события автоматически.
- Больший приоритет вызывается раньше: `Highest = 200`, `High = 100`, `Medium = 0`, `Low = -100`, `Lowest = -200`. При равном приоритете сохраняется порядок добавления слушателей.
- Для события с `ICancellable` доставка прекращается, когда `IsCancelled` становится `true`. Базовый класс `CancellableEvent` предоставляет это свойство и `Cancel()`. Перед доставкой отмена сбрасывается, если есть слушатели; без слушателей её состояние сохраняется.
- Повторная подписка того же объекта, типа или слушателя не создаёт дубликатов. Изменения подписок во время доставки влияют на последующие отправки, включая вложенные.
- Исключение обработчика передаётся вызывающему коду и прекращает доставку. Хранилище подписок защищено от одновременных изменений; доступ к обработчикам и изменяемым событиям нужно согласовывать отдельно.

`IsSubscribed(...)` проверяет подписку, `IsListening<T>()` — наличие слушателей, `Clear()` снимает все подписки. Шина удерживает ссылки на подписчиков: отписывайте их при завершении жизненного цикла.

Соответствие API и отличия от Java Orbit описаны в [заметках о портировании](docs/PORTING.md).

## Godot

Используйте редактор Godot с поддержкой **Mono/.NET**. В своём проекте добавьте ссылку на DLL из примера выше; создавайте подписки в `_Ready` и снимайте их в `_ExitTree`. Если обработчики обращаются к объектам Godot, отправляйте события из главного потока.

Сначала соберите библиотеку, затем откройте `project.godot` примера в соответствующем редакторе, соберите C#-проект и запустите сцену:

| Пример | Настройки проекта |
| --- | --- |
| [Godot 3](examples/Godot3/project.godot) | Godot 3.6.3 Mono, `Godot.NET.Sdk/3.3.0`, `net472` |
| [Godot 4](examples/Godot4/project.godot) | Godot 4.6.3 .NET, `Godot.NET.Sdk/4.6.3`, `net8.0` |

Для примера Godot 3 нужны reference assemblies .NET Framework 4.7.2 из подходящей установки Mono или инструментов сборки .NET Framework. Для другой версии Godot 4 согласуйте SDK и целевую платформу примера с C#-шаблоном установленного редактора. Обе сцены показывают доставку и отмену событий урона.

## Unity

Скопируйте `CSorbit.dll` из архива для Unity в `Assets/Plugins/CSorbit/`. [Пример Unity](examples/Unity/README.md) содержит компонент с подпиской в `OnEnable`, отпиской в `OnDisable` и настройками сохранения обработчиков для IL2CPP.

Для AOT предпочтительны явные подписки `Subscribe<T>(Action<T>)`. Атрибутные обработчики могут использовать `new EventBus(ListenerInvocationMode.Reflection)`; их необходимо сохранять от удаления неиспользуемого кода. Проверяйте итоговую сборку Player на целевой платформе.

## Сборка из исходников

Требования: **.NET SDK 8 или новее** и **PowerShell** (Windows PowerShell 5.1 или PowerShell 7). Выполните из корня репозитория одну из команд:

```powershell
# Windows PowerShell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1

# PowerShell 7
pwsh -NoProfile -File scripts/build.ps1
```

Скрипт собирает библиотеку для всех трёх целевых платформ, запускает тесты ядра, собирает консольный пример и создаёт ZIP-архивы и NuGet-пакет в `artifacts/`. Для сборки самой библиотеки игровые движки не нужны.

Тесты и консольный пример можно запустить отдельно:

```shell
dotnet run --project tests/CSorbit.Tests -c Release
dotnet run --project examples/Console -c Release
```

Охват проверок и запуск сцен движков описаны в [инструкции по тестированию](docs/TESTING.md). Создаваемые файлы исключены через `.gitignore`.

## Лицензия

[MIT](LICENSE). Уведомление об авторских правах исходной Orbit сохранено. Сведения об авторах — в [уведомлениях о сторонних компонентах](docs/THIRD_PARTY_NOTICES.md).
