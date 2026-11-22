using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using FirebirdTraceAnalyzer.Interfaces;
using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.Services.Shortcuts;
using FirebirdTraceAnalyzer.ViewModels;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Строит нативное меню macOS как зеркало главного in-window меню (<c>MainMenuView</c>), включая
/// динамические списки отчётов (NativeMenu не поддерживает <c>ItemsSource</c>).
/// <para>
/// Каноничный путь Avalonia (см. NativeMenu.NeedsUpdate, PR AvaloniaUI/Avalonia#3762): меню назначается
/// РОВНО ОДИН раз (<see cref="NativeMenu.SetMenu"/>), а динамические пункты обновляются в обработчике
/// события <see cref="NativeMenu.NeedsUpdate"/> — оно вызывается экспортёром прямо перед показом меню
/// (и перед нажатием хоткея — это гарантированная точка синхронизации жестов при перепривязке).
/// Повторный <c>SetMenu</c> на macOS падает («The menu being updated does not match»), а мутация
/// <c>Items</c> ВНЕ <c>NeedsUpdate</c> рассинхронивает экспортёр и делает меню инертным — поэтому оба
/// пути не используются. На Windows/Linux SetMenu — no-op (там работает in-window меню).
/// </para>
/// <para>
/// На macOS строка меню не показывает плоские команды верхнего уровня — каждый верхний пункт обязан
/// быть подменю. Поэтому Settings уходит в меню приложения (первый пункт = app-menu, ⌘,), а Plugins и
/// Parser Stats — в подменю Tools.
/// </para>
/// <para>
/// Жесты пунктов берутся из реестра горячих клавиш (<see cref="IShortcutRegistry"/>), а не хардкодятся:
/// при перепривязке пользователем свойство Gesture отслеженных пунктов обновляется напрямую
/// (смена свойства существующего пункта — не мутация Items, экспортёру не мешает), а полная
/// пересборка внутри NeedsUpdate подхватывает жесты отчётов.
/// </para>
/// </summary>
internal sealed class NativeMainMenu
{
    private readonly Window _window;
    private readonly MainWindowViewModel _vm;
    private readonly IShortcutRegistry? _registry;

    // Отслеженные пункты с жестами: (пункт, id действия). Обновляются при Changed реестра.
    private readonly List<(NativeMenuItem Item, string ActionId)> _gestureItems = [];
    private readonly List<(NativeMenuItem Item, string ActionId)> _reportGestureItems = [];

    private NativeMenu _quickReportsMenu = null!;
    private NativeMenu _customReportsMenu = null!;
    private NativeMenuItem _customReportsItem = null!;

    private NativeMainMenu(Window window, MainWindowViewModel vm, IShortcutRegistry? registry)
    {
        _window = window;
        _vm = vm;
        _registry = registry;

        if (registry is not null)
        {
            registry.Changed += (_, _) => RefreshGestures();
            // На время записи сочетания снимаем и нативные ускорители (macOS: ⌘-ускоритель
            // срабатывает на уровне ОС до любого перехвата — задавить его может только
            // пустой Gesture на самом пункте).
            Shortcut.SuspensionChanged += (_, _) => RefreshGestures();
        }
    }

    public static void Attach(Window window, MainWindowViewModel vm, IShortcutRegistry? registry)
    {
        var owner = new NativeMainMenu(window, vm, registry);
        NativeMenu.SetMenu(window, owner.Build()); // единственный SetMenu окна; динамика — через NeedsUpdate
    }

    // Пункт Settings/Preferences задаётся ДЕКЛАРАТИВНО в App.axaml (NativeMenu на уровне Application) —
    // так он попадает в системное меню приложения (About/Quit) с правильным таймингом.
    // Его жест (⌘,) принадлежит действию App.OpenSettings с IsRebindable=false.

    private NativeMenu Build() => new()
    {
        Items =
        {
            Submenu("NativeMenu.Open",
                Item("NativeMenu.Open.Local", _vm.OpenLocalFileCommand, "App.OpenLocalFile"),
                Item("NativeMenu.Open.Remote", _vm.OpenRemoteFileCommand, "App.OpenRemoteFile"),
                Item("NativeMenu.Open.Cancel", _vm.CancelLoadingCommand, "App.CancelLoading")),

            Submenu("NativeMenu.Edit",
                Submenu("NativeMenu.Edit.Reparse",
                    Item("NativeMenu.Common.AllFiles", _vm.ReparseAllFilesCommand, "App.ReparseAllFiles"),
                    Item("NativeMenu.Common.SelectedFiles", _vm.ReparseSelectedFilesCommand, "App.ReparseSelectedFiles")),
                Submenu("NativeMenu.Edit.Close",
                    Item("NativeMenu.Common.AllFiles", _vm.CloseAllFilesCommand, "App.CloseAllFiles"),
                    Item("NativeMenu.Common.SelectedFiles", _vm.CloseSelectedFilesCommand, "App.CloseSelectedFiles"))),

            ReportsMenu(),
            WindowMenu(),

            Submenu("NativeMenu.Storage",
                Item("NativeMenu.StorageManagement", _vm.OpenStoreManagementCommand),
                Item("NativeMenu.StorageAnalysis", _vm.OpenStorageAnalysisCommand)),

            ToolsMenu(),
        }
    };

    private NativeMenuItem ToolsMenu()
    {
        var parserStats = Item("NativeMenu.ParserStats", _vm.OpenParserStatisticsCommand);
        parserStats.Bind(NativeMenuItem.IsVisibleProperty, new Binding(nameof(_vm.IsStatisticsMode)) { Source = _vm });

        return new NativeMenuItem
        {
            Header = Loc.Tr("NativeMenu.Tools"),
            Menu = new NativeMenu
            {
                Items =
                {
                    Item("NativeMenu.Plugins", _vm.OpenPluginsCommand),
                    parserStats,
                }
            }
        };
    }

    private NativeMenuItem WindowMenu() => new()
    {
        Header = Loc.Tr("NativeMenu.Window"),
        Menu = new NativeMenu
        {
            Items =
            {
                CheckItem("NativeMenu.Window.Files", _vm.SwitchVisibleTraceFilesSectionCommand, nameof(_vm.IsTraceFilesSectionVisible), "App.ToggleFilesSection"),
                CheckItem("NativeMenu.Window.Search", _vm.SwitchVisibleSearchSectionCommand, nameof(_vm.IsSearchSectionVisible), "App.ToggleSearchSection"),
                CheckItem("NativeMenu.Window.Events", _vm.SwitchEventsSectionVisibleCommand, nameof(_vm.IsEventsSectionVisible), "App.ToggleEventsSection"),
                CheckItem("NativeMenu.Window.Statistics", _vm.SwitchStatisticsSectionVisibleCommand, nameof(_vm.IsStatisticsSectionVisible), "App.ToggleStatisticsSection"),
                CheckItem("NativeMenu.Window.Logs", _vm.SwitchLogsSectionVisibleCommand, nameof(_vm.IsLogsSectionVisible), "App.ToggleLogsSection"),
                new NativeMenuItemSeparator(),
                Item("NativeMenu.Window.Reset", _vm.GoToFactorySettingsSectionCommand, "App.ResetLayout"),
            }
        }
    };

    private NativeMenuItem ReportsMenu()
    {
        _quickReportsMenu = new NativeMenu();
        _customReportsMenu = new NativeMenu();
        _customReportsItem = new NativeMenuItem { Header = Loc.Tr("NativeMenu.Reports.Custom"), Menu = _customReportsMenu };

        var reportsMenu = new NativeMenu
        {
            Items =
            {
                new NativeMenuItem { Header = Loc.Tr("NativeMenu.Reports.Quick"), Menu = _quickReportsMenu },
                _customReportsItem,
                new NativeMenuItemSeparator(),
                Item("NativeMenu.Reports.Manage", _vm.OpenManageTemplatesCommand),
                new NativeMenuItemSeparator(),
                Item("NativeMenu.Reports.Recent", _vm.OpenRecentReportsCommand),
            }
        };

        // Динамика через NeedsUpdate: экспортёр вызывает его прямо перед показом меню — тут безопасно
        // менять Items (в отличие от повторного SetMenu / мутации вне NeedsUpdate). Заодно покрывает
        // отчёты, созданные в сессии, без перезапуска.
        reportsMenu.NeedsUpdate += (_, _) => RefreshReports();
        RefreshReports(); // начальное наполнение (на случай, если NeedsUpdate не сработает при первом показе)

        return new NativeMenuItem { Header = Loc.Tr("NativeMenu.Reports"), Menu = reportsMenu };
    }

    private void RefreshReports()
    {
        // ОБА списка отчётов заполняются из ОДНОГО трака отслеженных жестов: очищаем один раз
        // ПЕРЕД обоими заполнениями. Clear() внутри FillReports затирал записи built-in отчётов
        // при заполнении custom — жесты built-in переставали обновляться при перепривязке.
        _reportGestureItems.Clear();
        FillReports(_quickReportsMenu, _vm.BuiltInReports);
        FillReports(_customReportsMenu, _vm.CustomReports);
        _customReportsItem.IsVisible = _vm.CustomReports.Count > 0;
    }

    private void FillReports(NativeMenu menu, IEnumerable<ReportTemplate> reports)
    {
        menu.Items.Clear();

        foreach (var template in reports)
        {
            var item = new NativeMenuItem
            {
                Header = template.Name,
                Command = _vm.GenerateQuickReportCommand,
                CommandParameter = template.Id
            };

            BindGesture(item, ReportShortcutSource.ActionIdFor(template.Id), _reportGestureItems);
            menu.Items.Add(item);
        }
    }

    private void RefreshGestures()
    {
        // Прямая смена свойства Gesture существующего пункта — не мутация Items, экспортёру
        // не мешает; полную пересборку (жесты отчётов при изменившемся наборе) делает
        // NeedsUpdate перед следующим показом меню / нажатием хоткея.
        RefreshList(_gestureItems);
        RefreshList(_reportGestureItems);
    }

    private void RefreshList(List<(NativeMenuItem Item, string ActionId)> items)
    {
        if (_registry is null)
            return;

        // Режим записи: ускорители сняты (жест вернётся при выходе из записи через RefreshGestures).
        foreach (var (item, actionId) in items)
            item.Gesture = Shortcut.IsSuspended ? null : _registry.Resolve(actionId);
    }

    private void BindGesture(NativeMenuItem item, string actionId, List<(NativeMenuItem, string)> list)
    {
        if (_registry is null)
            return;

        item.Gesture = _registry.Resolve(actionId);
        list.Add((item, actionId));
    }

    private NativeMenuItem Item(string key, ICommand? command, string? actionId = null)
    {
        var item = new NativeMenuItem { Header = Loc.Tr(key), Command = command };

        if (actionId is not null)
            BindGesture(item, actionId, _gestureItems);

        return item;
    }

    private static NativeMenuItem Submenu(string key, params NativeMenuItemBase[] children)
    {
        var menu = new NativeMenu();
        foreach (var child in children)
            menu.Items.Add(child);
        return new NativeMenuItem { Header = Loc.Tr(key), Menu = menu };
    }

    private NativeMenuItem CheckItem(string key, ICommand command, string boolProperty, string actionId)
    {
        var item = new NativeMenuItem
        {
            Header = Loc.Tr(key),
            Command = command,
            ToggleType = MenuItemToggleType.CheckBox,
        };

        BindGesture(item, actionId, _gestureItems);
        item.Bind(NativeMenuItem.IsCheckedProperty, new Binding(boolProperty) { Source = _vm, Mode = BindingMode.OneWay });
        return item;
    }
}
