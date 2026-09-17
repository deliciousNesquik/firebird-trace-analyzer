using Avalonia.Controls;

namespace FirebirdTraceAnalyzer.UserControls;

/// <summary>
/// Хост компактного (однострочного) представления события: по типу события выбирает
/// соответствующий компакт-контрол из <c>Controls/EventCards/Compact</c>. Это отдельная
/// точка регистрации «тип → компакт-карточка» для плотного режима списка (полные карточки
/// регистрируются в App.axaml; там на тип допустим лишь один неявный шаблон).
/// </summary>
public partial class CompactEventItem : UserControl
{
    public CompactEventItem()
    {
        InitializeComponent();
    }
}
