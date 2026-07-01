using CommunityToolkit.Mvvm.ComponentModel;
using FirebirdTraceAnalyzer.Core;
using FirebirdTraceAnalyzer.ViewModels;

namespace FirebirdTraceAnalyzer.Models;

/// <summary>
/// Информация об удалённом файле
/// </summary>
public partial class RemoteFileInfo: ViewModelBase
{
    /// <summary>Имя файла</summary>
    [ObservableProperty]
    public partial string FileName { get; set; } = string.Empty;

    /// <summary>Полный путь на удалённом сервере</summary>
    [ObservableProperty]
    public partial string FullPath { get; set; } = string.Empty;
    
    /// <summary>Размер файла в байтах</summary>
    [ObservableProperty]
    public partial long Size { get; set; }
    
    /// <summary>Дата последней модификации</summary>
    [ObservableProperty]
    public partial DateTime LastModified { get; set; }
    
    /// <summary>Права доступа</summary>
    [ObservableProperty]
    public partial Permissions Permissions { get; set; } = new(false, false, false, false, false, false, false, false, false);
    
    /// <summary>Владелец файла</summary>
    [ObservableProperty]
    public partial string Owner { get; set; } = string.Empty;
    
    /// <summary>Выбран ли файл для загрузки</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Размер в читаемом формате</summary>
    public string FormattedSize => ByteSizeFormatter.FormatBytes(Size);

    /// <summary>Дата в читаемом формате</summary>
    public string FormattedDate => LastModified.ToString("yyyy-MM-dd HH:mm:ss");
}