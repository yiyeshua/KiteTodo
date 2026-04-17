using System.IO;

namespace KiteTodo.Models;

public enum FlowchartStorageType
{
    Database = 0,
    File = 1
}

public class FlowchartStorageItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public FlowchartStorageType StorageType { get; set; } = FlowchartStorageType.Database;
    public string? FilePath { get; set; }
    public string ContentJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public string StorageTypeText => StorageType == FlowchartStorageType.Database ? "数据库" : "文件";

    public string LocationText => StorageType == FlowchartStorageType.Database
        ? "保存在数据库"
        : string.IsNullOrWhiteSpace(FilePath)
            ? "文件路径未设置"
            : Path.GetFileName(FilePath);
}