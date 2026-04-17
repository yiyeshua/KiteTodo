using System.IO;
using System.Text.Json;
using KiteTodo.Models;

namespace KiteTodo.Services;

public class FlowchartStorageService
{
    private readonly DatabaseService _db = DatabaseService.Instance;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public List<FlowchartStorageItem> GetAll()
    {
        return _db.Flowcharts.FindAll()
            .OrderByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.Name)
            .ToList();
    }

    public FlowchartStorageItem? FindById(int id)
    {
        return _db.Flowcharts.FindById(id);
    }

    public void Delete(int id)
    {
        _db.Flowcharts.Delete(id);
    }

    public FlowchartStorageItem? FindByFilePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        var normalizedPath = Path.GetFullPath(filePath);
        return _db.Flowcharts.FindOne(item => item.StorageType == FlowchartStorageType.File && item.FilePath == normalizedPath);
    }

    public FlowchartStorageItem Save(FlowchartDocument document, string name, FlowchartStorageType storageType, string? filePath, int? existingId = null)
    {
        document.Name = name.Trim();
        var json = JsonSerializer.Serialize(document, _jsonOptions);
        var now = DateTime.Now;
        var normalizedPath = string.IsNullOrWhiteSpace(filePath) ? null : Path.GetFullPath(filePath);

        FlowchartStorageItem? item = null;
        if (existingId.HasValue)
            item = _db.Flowcharts.FindById(existingId.Value);

        if (item == null && storageType == FlowchartStorageType.File && !string.IsNullOrWhiteSpace(normalizedPath))
            item = FindByFilePath(normalizedPath);

        item ??= new FlowchartStorageItem
        {
            CreatedAt = now
        };

        item.Name = document.Name;
        item.StorageType = storageType;
        item.FilePath = normalizedPath;
        item.ContentJson = json;
        item.UpdatedAt = now;

        if (storageType == FlowchartStorageType.File)
        {
            if (string.IsNullOrWhiteSpace(normalizedPath))
                throw new InvalidOperationException("保存到文件时必须指定文件路径。");

            var directory = Path.GetDirectoryName(normalizedPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(normalizedPath, json);
        }

        _db.Flowcharts.Upsert(item);
        return item;
    }

    public FlowchartDocument Load(FlowchartStorageItem item)
    {
        string json;
        if (item.StorageType == FlowchartStorageType.File && !string.IsNullOrWhiteSpace(item.FilePath) && File.Exists(item.FilePath))
            json = File.ReadAllText(item.FilePath);
        else
            json = item.ContentJson;

        var document = JsonSerializer.Deserialize<FlowchartDocument>(json, _jsonOptions) ?? new FlowchartDocument();
        if (string.IsNullOrWhiteSpace(document.Name))
            document.Name = item.Name;
        return document;
    }

    public FlowchartDocument LoadFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<FlowchartDocument>(json, _jsonOptions) ?? new FlowchartDocument();
    }
}