namespace KiteTodo.Models;

public class NavigationDirectoryCategoryItem
{
    public required string Name { get; set; }

    public int ItemCount { get; set; }

    public bool CanDelete { get; set; }

    public string DisplayText => $"{Name} ({ItemCount})";
}