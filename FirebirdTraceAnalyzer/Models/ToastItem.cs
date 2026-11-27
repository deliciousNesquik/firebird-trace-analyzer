using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FirebirdTraceAnalyzer.Models;

/// <summary>
/// Одно тост-уведомление снизу-справа. <see cref="IsShown"/> управляет анимацией: элемент
/// добавляется в коллекцию как <c>false</c> (скрыт справа), затем на следующем кадре становится
/// <c>true</c> (въезд), а на закрытии снова <c>false</c> (выезд) — после чего сервис убирает его.
/// </summary>
public sealed partial class ToastItem : ObservableObject
{
    /// <summary>Идентификатор для адресного закрытия.</summary>
    public required Guid Id { get; init; }

    /// <summary>Тип: успех / предупреждение / ошибка.</summary>
    public required ToastType Type { get; init; }

    /// <summary>Заголовок (жирная строка).</summary>
    public required string Title { get; init; }

    /// <summary>Необязательная вторая строка с деталями.</summary>
    public string? Message { get; init; }

    /// <summary>
    /// Подпись кнопки действия (null = кнопки нет). Команда в <see cref="ActionCommand"/>
    /// выполняет <see cref="Action"/> и закрывает тост.
    /// </summary>
    public string? ActionLabel { get; init; }

    /// <summary>Действие кнопки (например, «открыть лог»); null допустим.</summary>
    public Action? Action { get; init; }

    /// <summary>Команда кнопки действия; создаёт сервис при показе тоста (set — сразу после создания item'а).</summary>
    public ICommand? ActionCommand { get; set; }

    /// <summary>Показан ли тост (управляет въездом/выездом через стили). Меняет только сервис на UI-потоке.</summary>
    [ObservableProperty] private bool _isShown;

    // Признаки типа для привязки классов/видимости в шаблоне (без конвертеров).
    public bool IsSuccess => Type == ToastType.Success;
    public bool IsWarning => Type == ToastType.Warning;
    public bool IsError => Type == ToastType.Error;
}
