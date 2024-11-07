public class Note
{
  public int Id { get; set; }
  public string Title { get; set; }
  public string Text { get; set; }
  public DateTime Date { get; set; }
  public bool IsDeleted { get; set; }
  public int UserId { get; set; } // Внешний ключ для User
  public User User { get; set; } // Навигационное свойство
  public string ShareToken { get; set; }

  // Поля для паттерна Composite
  public bool IsFolder { get; set; }
  public int? ParentId { get; set; }
  public Note Parent { get; set; }
  public List<Note> Children { get; set; }
}