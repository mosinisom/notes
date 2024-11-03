using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

class Program
{
  static async Task Main(string[] args)
  {
    using (var client = new ClientWebSocket())
    {
      await client.ConnectAsync(new Uri("ws://localhost:5088/ws"), CancellationToken.None);
      Console.WriteLine("Connected to WebSocket server");

      // Проверка регистрации пользователя
      var registerMessage = JsonSerializer.Serialize(new
      {
        action = "register",
        username = "testuser",
        password_hash = "testpasswordhash"
      });
      await SendMessage(client, registerMessage);

      // Проверка авторизации пользователя
      var loginMessage = JsonSerializer.Serialize(new
      {
        action = "login",
        username = "testuser",
        password_hash = "testpasswordhash"
      });
      await SendMessage(client, loginMessage);

      // Проверка создания заметки
      var createNoteMessage = JsonSerializer.Serialize(new
      {
        action = "create_note",
        title = "Test Note",
        text = "This is a test note"
      });
      await SendMessage(client, createNoteMessage);

      // Проверка редактирования заметки
      var editNoteMessage = JsonSerializer.Serialize(new
      {
        action = "edit_note",
        id = 1,
        title = "Updated Test Note",
        text = "This is an updated test note"
      });
      await SendMessage(client, editNoteMessage);

      // Проверка удаления заметки
      var deleteNoteMessage = JsonSerializer.Serialize(new
      {
        action = "delete_note",
        id = 1
      });
      await SendMessage(client, deleteNoteMessage);

      await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
      Console.WriteLine("WebSocket connection closed");
    }
  }

  static async Task SendMessage(ClientWebSocket client, string message)
  {
    var messageBytes = Encoding.UTF8.GetBytes(message);
    await client.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, CancellationToken.None);

    var buffer = new byte[1024 * 4];
    var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
    var response = Encoding.UTF8.GetString(buffer, 0, result.Count);
    Console.WriteLine($"Received: {response}");
  }
}