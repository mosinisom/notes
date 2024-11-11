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
  private readonly QRCodeGeneratorService _qrGenerator;

  public WebSocketController(ApplicationDbContext context, QRCodeGeneratorService qrGenerator)
  {
    _context = context;
    _qrGenerator = qrGenerator;
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

    string response;
    switch (action)
    {
      case "create_note":
        response = CreateNote(json.RootElement);
        break;
      case "edit_note":
        response = EditNote(json.RootElement);
        break;
      case "delete_note":
        response = DeleteNote(json.RootElement);
        break;
      case "register":
        response = RegisterUser(json.RootElement);
        break;
      case "login":
        response = LoginUser(json.RootElement);
        break;
      case "share_note":
        response = ShareNote(json.RootElement);
        break;
      case "get_shared_note":
        response = GetSharedNote(json.RootElement);
        break;
      case "test":
        response = JsonSerializer.Serialize(new { action = "test", status = "success", message = "Hello Client!" });
        break;
      default:
        response = JsonSerializer.Serialize(new { action = "undefined", status = "error", message = "Unknown action" });
        break;
    }

    return response;
  }

  private string GenerateShareToken()
  {
    return Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Replace("/", "_").Replace("+", "-");
  }

  private string ShareNote(JsonElement json)
  {
    var noteId = json.GetProperty("id").GetInt32();
    var userId = int.Parse(json.GetProperty("user_id").GetString());

    var note = _context.Notes.FirstOrDefault(n => n.Id == noteId && n.UserId == userId);

    if (note == null)
    {
      return JsonSerializer.Serialize(new { action = "share_note", status = "error", message = "Note not found" });
    }

    if (string.IsNullOrEmpty(note.ShareToken))
    {
      note.ShareToken = GenerateShareToken();
      _context.SaveChanges();
    }

    var shareUrl = $"{Request.Scheme}://{Request.Host}/shared/{note.ShareToken}";
    var qrCode = _qrGenerator.GenerateQRCode(shareUrl);

    return JsonSerializer.Serialize(new { action = "share_note", status = "success", shareUrl, qrCode });
  }

  private string GetSharedNote(JsonElement json)
  {
    var token = json.GetProperty("token").GetString();

    var note = _context.Notes.FirstOrDefault(n => n.ShareToken == token && !n.IsDeleted);

    if (note == null)
    {
      return JsonSerializer.Serialize(new { action = "get_shared_note", status = "error", message = "Note not found" });
    }

    return JsonSerializer.Serialize(new
    {
        action = "get_shared_note",
        status = "success",
        note = new
        {
            title = note.Title,
            text = note.Text,
            date = note.Date
        }
    });
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
        ParentId = json.TryGetProperty("parent_id", out JsonElement parentId) ? parentId.GetInt32() : (int?)null
    };

    _context.Notes.Add(note);
    _context.SaveChanges();

    return JsonSerializer.Serialize(new { action = "create_note", status = "success", note.Id });
  }

  private string EditNote(JsonElement json)
  {
    var id = json.GetProperty("id").GetInt32();
    var userId = int.Parse(json.GetProperty("user_id").GetString());

    var note = _context.Notes.FirstOrDefault(n => n.Id == id && n.UserId == userId);

    if (note == null)
    {
        return JsonSerializer.Serialize(new { action = "edit_note", status = "error", message = "Note not found" });
    }

    note.Title = json.GetProperty("title").GetString();
    note.Text = json.GetProperty("text").GetString();

    if (json.TryGetProperty("parent_id", out JsonElement parentId))
    {
        note.ParentId = parentId.GetInt32();
    }

    _context.SaveChanges();

    return JsonSerializer.Serialize(new { action = "edit_note", status = "success" });
  }

  private string DeleteNote(JsonElement json)
  {
    var id = json.GetProperty("id").GetInt32();
    var userId = int.Parse(json.GetProperty("user_id").GetString());

    var note = _context.Notes.FirstOrDefault(n => n.Id == id && n.UserId == userId);

    if (note == null)
    {
        return JsonSerializer.Serialize(new { action = "delete_note", status = "error", message = "Note not found" });
    }

    MarkNoteAsDeleted(note);
    _context.SaveChanges();

    return JsonSerializer.Serialize(new { action = "delete_note", status = "success" });
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
        return JsonSerializer.Serialize(new { action = "register", status = "error", message = "User already exists" });
    }

    var user = new User
    {
        Username = username,
        PasswordHash = passwordHash,
        AuthToken = Guid.NewGuid().ToString()
    };

    _context.Users.Add(user);
    _context.SaveChanges();

    return JsonSerializer.Serialize(new { action = "register", status = "success", auth_token = user.AuthToken });
  }

  private string LoginUser(JsonElement json)
  {
    var username = json.GetProperty("username").GetString();
    var passwordHash = json.GetProperty("password_hash").GetString();

    var user = _context.Users.SingleOrDefault(u => u.Username == username && u.PasswordHash == passwordHash);

    if (user == null)
    {
        return JsonSerializer.Serialize(new { action = "login", status = "error", message = "Invalid credentials" });
    }

    return JsonSerializer.Serialize(new { action = "login", status = "success", auth_token = user.AuthToken });
  }
}