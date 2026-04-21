namespace KiteTodo.Helpers;

public static class SearchNavigationRequest
{
    public static DateTime? PendingTodoDate { get; set; }

    public static int? PendingNoteId { get; set; }

    public static void ClearTodo()
    {
        PendingTodoDate = null;
    }

    public static void ClearNote()
    {
        PendingNoteId = null;
    }
}