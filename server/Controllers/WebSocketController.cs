using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

[ApiController]
[Route("ws")]
public class WebSocketController : ControllerBase
{
  private readonly ApplicationDbContext _context;

  public WebSocketController(ApplicationDbContext context)
  {
    _context = context;
  }

  [HttpGet]
  public async Task Get()
  {
    if (HttpContext.WebSockets.IsWebSocketRequest)
    {
      var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
      await HandleWebSocket(webSocket);
    }
    else
    {
      HttpContext.Response.StatusCode = 400;
    }
  }

  private async Task HandleWebSocket(WebSocket webSocket)
  {
    var buffer = new byte[1024 * 4];
    WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

    while (!result.CloseStatus.HasValue)
    {
      var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
      var response = ProcessMessage(message);
      var responseBytes = Encoding.UTF8.GetBytes(response);

      await webSocket.SendAsync(new ArraySegment<byte>(responseBytes), result.MessageType, result.EndOfMessage, CancellationToken.None);
      result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
    }

    await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
  }

  private string ProcessMessage(string message)
  {
    var json = JsonDocument.Parse(message);
    var action = json.RootElement.GetProperty("action").GetString();

    switch (action)
    {
      case "create_note":
        return CreateNote(json.RootElement);
      case "edit_note":
        return EditNote(json.RootElement);
      case "delete_note":
        return DeleteNote(json.RootElement);
      case "register":
        return RegisterUser(json.RootElement);
      case "login":
        return LoginUser(json.RootElement);
      default:
        return "Unknown action";
    }
  }

  private string CreateNote(JsonElement json)
  {
    var note = new Note
    {
      Title = json.GetProperty("title").GetString(),
      Text = json.GetProperty("text").GetString(),
      Date = DateTime.Now,
      IsDeleted = false,
      UserId = int.Parse(json.GetProperty("user_id").GetString()),
      IsFolder = json.GetProperty("is_folder").GetBoolean(),
      ParentId = json.TryGetProperty("parent_id", out JsonElement parentId) ?
            parentId.GetInt32() : null
    };

    _context.Notes.Add(note);
    _context.SaveChanges();

    return JsonSerializer.Serialize(new { status = "success", note.Id });
  }

  private string EditNote(JsonElement json)
  {
    var id = json.GetProperty("id").GetInt32();
    var userId = int.Parse(json.GetProperty("user_id").GetString());

    var note = _context.Notes.FirstOrDefault(n => n.Id == id && n.UserId == userId);

    if (note == null)
    {
      return JsonSerializer.Serialize(new { status = "error", message = "Note not found" });
    }

    note.Title = json.GetProperty("title").GetString();
    note.Text = json.GetProperty("text").GetString();

    if (json.TryGetProperty("parent_id", out JsonElement parentId))
    {
      note.ParentId = parentId.GetInt32();
    }

    _context.SaveChanges();

    return JsonSerializer.Serialize(new { status = "success" });
  }

  private string DeleteNote(JsonElement json)
  {
    var id = json.GetProperty("id").GetInt32();
    var userId = int.Parse(json.GetProperty("user_id").GetString());

    var note = _context.Notes.FirstOrDefault(n => n.Id == id && n.UserId == userId);

    if (note == null)
    {
      return JsonSerializer.Serialize(new { status = "error", message = "Note not found" });
    }

    // Рекурсивно помечаем как удаленные все дочерние заметки
    MarkNoteAsDeleted(note);
    _context.SaveChanges();

    return JsonSerializer.Serialize(new { status = "success" });
  }

  private void MarkNoteAsDeleted(Note note)
  {
    note.IsDeleted = true;
    var children = _context.Notes.Where(n => n.ParentId == note.Id);
    foreach (var child in children)
    {
      MarkNoteAsDeleted(child);
    }
  }

  // Добавляем новый метод для получения структуры папок
  private string GetNoteStructure(JsonElement json)
  {
    var userId = int.Parse(json.GetProperty("user_id").GetString());
    var notes = _context.Notes
        .Where(n => n.UserId == userId && !n.IsDeleted)
        .OrderBy(n => n.IsFolder)
        .ThenBy(n => n.Title)
        .ToList();

    var structure = BuildNoteTree(notes, null);
    return JsonSerializer.Serialize(new { status = "success", structure });
  }

  private List<object> BuildNoteTree(List<Note> allNotes, int? parentId)
  {
    var items = new List<object>();
    var children = allNotes.Where(n => n.ParentId == parentId).ToList();

    foreach (var child in children)
    {
      if (child.IsFolder)
      {
        items.Add(new
        {
          id = child.Id,
          title = child.Title,
          is_folder = true,
          children = BuildNoteTree(allNotes, child.Id)
        });
      }
      else
      {
        items.Add(new
        {
          id = child.Id,
          title = child.Title,
          is_folder = false,
          text = child.Text
        });
      }
    }

    return items;
  }

  private string RegisterUser(JsonElement json)
  {
    var username = json.GetProperty("username").GetString();
    var passwordHash = json.GetProperty("password_hash").GetString();

    if (_context.Users.Any(u => u.Username == username))
    {
      return JsonSerializer.Serialize(new { status = "error", message = "User already exists" });
    }

    var user = new User
    {
      Username = username,
      PasswordHash = passwordHash,
      AuthToken = Guid.NewGuid().ToString()
    };

    _context.Users.Add(user);
    _context.SaveChanges();

    return JsonSerializer.Serialize(new { status = "success", auth_token = user.AuthToken });
  }

  private string LoginUser(JsonElement json)
  {
    var username = json.GetProperty("username").GetString();
    var passwordHash = json.GetProperty("password_hash").GetString();

    var user = _context.Users.SingleOrDefault(u => u.Username == username && u.PasswordHash == passwordHash);

    if (user == null)
    {
      return JsonSerializer.Serialize(new { status = "error", message = "Invalid credentials" });
    }

    return JsonSerializer.Serialize(new { status = "success", auth_token = user.AuthToken });
  }
}