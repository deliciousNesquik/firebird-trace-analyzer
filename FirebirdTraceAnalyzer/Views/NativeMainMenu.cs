using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using FirebirdTraceAnalyzer.Localization;
using FirebirdTraceAnalyzer.Models.Reports;
using FirebirdTraceAnalyzer.ViewModels;

namespace FirebirdTraceAnalyzer.Views;

/// <summary>
/// Строит нативное меню macOS как зеркало главного in-window меню (<c>MainMenuView</c>), включая
/// динамические списки отчётов (NativeMenu не поддерживает <c>ItemsSource</c>).
/// <para>
/// Каноничный путь Avalonia (см. NativeMenu.NeedsUpdate, PR AvaloniaUI/Avalonia#3762): меню назначается
/// РОВНО ОДИН раз (<see cref="NativeMenu.SetMenu"/>), а динамические пункты обновляются в обработчике
/// события <see cref="NativeMenu.NeedsUpdate"/> — оно вызывается экспортёром прямо перед показом меню.
/// Повторный <c>SetMenu</c> на macOS падает («The menu being updated does not match»), а мутация
/// <c>Items</c> ВНЕ <c>NeedsUpdate</c> рассинхронивает экспортёр и делает меню инертным — поэтому оба
/// пути не используются. На Windows/Linux SetMenu — no-op (там работает in-window меню).
/// </para>
/// <para>
/// На macOS строка меню не показывает плоские команды верхнего уровня — каждый верхний пункт обязан
/// быть подменю. Поэтому Settings уходит в меню приложения (первый пункт = app-menu, ⌘,), а Plugins и
/// Parser Stats — в подменю Tools.
/// </para>
/// </summary>
internal sealed class NativeMainMenu
{
    private readonly Window _window;
    private readonly MainWindowViewModel _vm;

    private NativeMenu _quickReportsMenu = null!;
    private NativeMenu _customReportsMenu = null!;
    private NativeMenuItem _customReportsItem = null!;

    private NativeMainMenu(Window window, MainWindowViewModel vm)
    {
        _window = window;
        _vm = vm;
    }

    public static void Attach(Window window, MainWindowViewModel vm)
    {
        var owner = new NativeMainMenu(window, vm);
        NativeMenu.SetMenu(window, owner.Build()); // единственный SetMenu окна; динамика — через NeedsUpdate
    }

    // Пункт Settings/Preferences задаётся ДЕКЛАРАТИВНО в App.axaml (NativeMenu на уровне Application) —
    // так он попадает в системное меню приложения (About/Quit) с правильным таймингом.

    private NativeMenu Build() => new()
    {
        Items =
        {
            Submenu("NativeMenu.Open",
                Item("NativeMenu.Open.Local", _vm.OpenLocalFileCommand, Key.O, KeyModifiers.Meta),
                Item("NativeMenu.Open.Remote", _vm.OpenRemoteFileCommand, Key.O, KeyModifiers.Meta | KeyModifiers.Shift),
                Item("NativeMenu.Open.Cancel", _vm.CancelLoadingCommand, Key.Escape, KeyModifiers.Meta)),

            Submenu("NativeMenu.Edit",
                Submenu("NativeMenu.Edit.Reparse",
                    Item("NativeMenu.Common.AllFiles", _vm.ReparseAllFilesCommand, Key.R, KeyModifiers.Meta),
                    Item("NativeMenu.Common.SelectedFiles", _vm.ReparseSelectedFilesCommand, Key.R, KeyModifiers.Meta | KeyModifiers.Shift)),
                Submenu("NativeMenu.Edit.Close",
                    Item("NativeMenu.Common.AllFiles", _vm.CloseAllFilesCommand, Key.W, KeyModifiers.Meta | KeyModifiers.Shift),
                    Item("NativeMenu.Common.SelectedFiles", _vm.CloseSelectedFilesCommand, Key.W, KeyModifiers.Meta))),

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
        FillReports(_quickReportsMenu, _vm.BuiltInReports);
        FillReports(_customReportsMenu, _vm.CustomReports);
        _customReportsItem.IsVisible = _vm.CustomReports.Count > 0;
    }

    private void FillReports(NativeMenu menu, ObservableCollection<ReportTemplate> reports)
    {
        menu.Items.Clear();
        foreach (var template in reports)
            menu.Items.Add(new NativeMenuItem
            {
                Header = template.Name,
                Command = _vm.GenerateQuickReportCommand,
                CommandParameter = template.Id
            });
    }

    private NativeMenuItem WindowMenu() => new()
    {
        Header = Loc.Tr("NativeMenu.Window"),
        Menu = new NativeMenu
        {
            Items =
            {
                CheckItem("NativeMenu.Window.Files", _vm.SwitchVisibleTraceFilesSectionCommand, nameof(_vm.IsTraceFilesSectionVisible), Key.D1),
                CheckItem("NativeMenu.Window.Search", _vm.SwitchVisibleSearchSectionCommand, nameof(_vm.IsSearchSectionVisible), Key.D2),
                CheckItem("NativeMenu.Window.Events", _vm.SwitchEventsSectionVisibleCommand, nameof(_vm.IsEventsSectionVisible), Key.D3),
                CheckItem("NativeMenu.Window.Statistics", _vm.SwitchStatisticsSectionVisibleCommand, nameof(_vm.IsStatisticsSectionVisible), Key.D4),
                CheckItem("NativeMenu.Window.Logs", _vm.SwitchLogsSectionVisibleCommand, nameof(_vm.IsLogsSectionVisible), Key.D5),
                new NativeMenuItemSeparator(),
                Item("NativeMenu.Window.Reset", _vm.GoToFactorySettingsSectionCommand, Key.D0, KeyModifiers.Meta),
            }
        }
    };

    // --- Хелперы построения -------------------------------------------------------------------

    private static NativeMenuItem Item(string key, ICommand? command, Key? gestureKey = null, KeyModifiers modifiers = KeyModifiers.None)
    {
        var item = new NativeMenuItem { Header = Loc.Tr(key), Command = command };
        if (gestureKey is { } k)
            item.Gesture = new KeyGesture(k, modifiers);
        return item;
    }

    private static NativeMenuItem Submenu(string key, params NativeMenuItemBase[] children)
    {
        var menu = new NativeMenu();
        foreach (var child in children)
            menu.Items.Add(child);
        return new NativeMenuItem { Header = Loc.Tr(key), Menu = menu };
    }

    private NativeMenuItem CheckItem(string key, ICommand command, string boolProperty, Key gestureKey)
    {
        var item = new NativeMenuItem
        {
            Header = Loc.Tr(key),
            Command = command,
            ToggleType = MenuItemToggleType.CheckBox,
            Gesture = new KeyGesture(gestureKey, KeyModifiers.Meta)
        };
        item.Bind(NativeMenuItem.IsCheckedProperty, new Binding(boolProperty) { Source = _vm, Mode = BindingMode.OneWay });
        return item;
    }
}
