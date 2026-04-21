namespace KiteTodo.Models;

public class TodoSubtask
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }
}