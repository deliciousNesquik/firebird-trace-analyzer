namespace FirebirdTraceAnalyzer.Enums.Reports;

public enum ReportVariableType
{
    // Файлы
    FileNames,              // Имена файлов
    FilePaths,              // Полные пути
    FileCount,              // Количество файлов
    FileSizeTotal,          // Общий размер
    
    // События
    TotalEventsCount,       // Всего событий
    FilteredEventsCount,    // После фильтрации
    VisibleEventsCount,     // Видимых (после сортировки/лимита)
    
    // Временные диапазоны
    TraceStartTime,         // Начало трассировки
    TraceEndTime,           // Конец трассировки
    TraceDuration,          // Длительность
    
    // Фильтры
    ActiveFilters,          // Активные фильтры
    ActiveSort,             // Активная сортировка
    
    // Статистика
    AverageExecutionTime,   // Среднее время выполнения
    MaxExecutionTime,       // Максимальное время
    MinExecutionTime,       // Минимальное время
    
    // Мета
    GeneratedDate,          // Дата создания отчёта
    GeneratedBy,            // Кто создал
    ApplicationVersion      // Версия приложения
}