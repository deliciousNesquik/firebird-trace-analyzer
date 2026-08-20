using System.ComponentModel;
using System.Globalization;
using Avalonia.Data.Converters;

namespace FirebirdTraceAnalyzer.Localization;

/// <summary>
/// Источник привязок для расширения <see cref="TrExtension"/>. Синглтон с уведомлением: при смене
/// языка увеличивает <see cref="Generation"/>, из-за чего все привязки <c>{loc:Tr}</c> переоценивают
/// строку через <see cref="TrConverter"/>. Так текст в уже открытых окнах обновляется вживую, без
/// перезапуска и без построчного кода во вьюхах — аналог того, как темы обновляют кисти.
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    public static Localizer Instance { get; } = new();

    /// <summary>Конвертер для <see cref="TrExtension"/>: ключ приходит через ConverterParameter.</summary>
    public static readonly IValueConverter TrConverter = new TrValueConverter();

    private ILocalizationService? _service;

    private Localizer() { }

    /// <summary>Меняется при каждой смене языка — триггер переоценки привязок <c>{loc:Tr}</c>.</summary>
    public int Generation { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Подключает сервис локализации и подписывается на смену языка.</summary>
    public void Attach(ILocalizationService service)
    {
        if (_service != null)
            _service.LanguageChanged -= OnLanguageChanged;

        _service = service;
        _service.LanguageChanged += OnLanguageChanged;
        Invalidate();
    }

    /// <summary>Перевод ключа; если сервис ещё не подключён — возвращает сам ключ.</summary>
    public string Translate(string key) => _service?.Tr(key) ?? key;

    private void OnLanguageChanged(object? sender, EventArgs e) => Invalidate();

    private void Invalidate()
    {
        Generation++;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Generation)));
    }

    //TODO: вынести конвертер в отдельный файл в директорию Converters
    /// <summary>Игнорирует значение-триггер, переводит ключ из ConverterParameter.</summary>
    private sealed class TrValueConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => parameter is string key ? Instance.Translate(key) : parameter ?? string.Empty;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
