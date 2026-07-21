using System.Text.Json;
using System.Text.RegularExpressions;
using FirebirdTraceParser.Exceptions;
using FirebirdTraceParser.Parsing.Engine;
using Microsoft.Extensions.Caching.Memory;
using NLog;

namespace FirebirdTraceParser.Parsing.Rules;

public sealed class JsonRuleLoader(IMemoryCache cache, ILogger logger, ParseOptions? parseOptions = null) : IRuleLoader
{
    private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private const int SupportedSchemaVersion = 1;

    // Единый управляемый барьер против ReDoS: таймаут матча берём из опций (иначе безопасный дефолт 1 c).
    private readonly TimeSpan _regexTimeout =
        parseOptions is { RegexTimeout: var t } && t > TimeSpan.Zero ? t : TimeSpan.FromSeconds(1);
    
    // делаем маппинг флагов регулярных выражений
    private static readonly Dictionary<string, RegexOptions> FlagMap = new()
    {
        ["IgnoreCase"] = RegexOptions.IgnoreCase,
        ["Multiline"] = RegexOptions.Multiline,
        ["Singleline"] = RegexOptions.Singleline,
        ["IgnorePatternWhitespace"] = RegexOptions.IgnorePatternWhitespace,
        ["ExplicitCapture"] = RegexOptions.ExplicitCapture
    };

    public IReadOnlyDictionary<string, Regex> LoadRules(string configPath)
    {
        var fileInfo = new FileInfo(configPath);
        if (!fileInfo.Exists)
        {
            _logger.Fatal("Config file not found: {ConfigPath}", configPath);
            throw new RuleValidationException($"Config file not found: {configPath}");   
        }
        
        var cacheKey = $"Rules_{configPath}_{fileInfo.LastWriteTimeUtc.Ticks}";
        
        return _cache.GetOrCreate(cacheKey, IReadOnlyDictionary<string, Regex> (entry) =>
        {
            entry.SetSlidingExpiration(TimeSpan.FromHours(1));
            
            _logger.Info("Loading rules from: {ConfigPath}", configPath);
            
            // парсим правила из файла
            var json = File.ReadAllText(configPath);
            using var document = JsonDocument.Parse(json);
            
            // валидируем правила из файла
            var schemaVersion = document.RootElement.GetProperty("schemaVersion").GetInt32();
            if (schemaVersion != SupportedSchemaVersion)
                throw new SchemaVersionException(SupportedSchemaVersion, schemaVersion);
            
            // десериализуем правила из файла
            var config = JsonSerializer.Deserialize<RuleConfiguration>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
            
            // компилируем и валидируем по предоставленным примерам
            var compiled = CompileAndValidate(config.Rules);
            
            _logger.Info("Loaded {RuleCount} rules successfully", compiled.Count);
            return compiled;
        })!;
    }
    
    private Dictionary<string, Regex> CompileAndValidate(Dictionary<string, RuleDefinition> rules)
    {
        var compiled = new Dictionary<string, Regex>(rules.Count);
        
        foreach (var (name, rule) in rules)
        {
            try
            {
                // парсим флаги
                var options = ParseFlags(rule.Flags) | RegexOptions.Compiled;
                
                // компилируем регулярные выражения с настраиваемым тайм-аутом матча
                var regex = new Regex(rule.Pattern, options, _regexTimeout);
                
                // валидируем необходимые группы которые указанны в конфиге
                var specGroups = new HashSet<string>(rule.RequiredGroups);
                var actualGroups = new HashSet<string>(regex.GetGroupNames().Where(g => !int.TryParse(g, out _)));
                var missingGroups = specGroups.Except(actualGroups).ToList();
                
                if (missingGroups.Any())
                {
                    _logger.Fatal($"Rule {name} does not contain group(s): '{string.Join(", ", missingGroups)}'");
                    throw new RuleValidationException(
                        $"Rule '{name}' does not contain group(s): {string.Join(", ", missingGroups)}",
                        name);
                }
                
                // валидация sample примера, в случае не совпадения падает с исключением!
                if (!string.IsNullOrEmpty(rule.Sample) && !regex.IsMatch(rule.Sample))
                {
                    _logger.Fatal($"Rule '{name}' does not match sample data: '{rule.Sample}'");
                    throw new RuleValidationException(
                        $"Rule '{name}' does not match sample data",
                        name) 
                    { 
                        SampleData = rule.Sample 
                    };
                }
                
                compiled[name] = regex;
                _logger.Debug("Compiled rule: {RuleName}", name);
            }
            catch (Exception ex) when (ex is not RuleValidationException)
            {
                _logger.Fatal(ex, $"Rule '{name}' failed compilation");
                throw new RuleValidationException($"Rule failed compilation '{name}': {ex.Message}", name);
            }
        }
        
        return compiled;
    }
    
    private static RegexOptions ParseFlags(string[]? flags)
    {
        if (flags == null || flags.Length == 0)
            return RegexOptions.IgnorePatternWhitespace;
        
        var result = RegexOptions.None;
        foreach (var flag in flags)
        {
            if (!FlagMap.TryGetValue(flag, out var option))
                throw new RuleValidationException($"Неизвестный флаг regex: {flag}");
            
            result |= option;
        }
        return result;
    }
}