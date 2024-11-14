using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<QRCodeGeneratorService>();
builder.Services.AddScoped<NotesService>();
builder.Services.AddScoped<UsersService>();

var app = builder.Build();

app.UseWebSockets();
app.UseStaticFiles();
app.MapGet("/shared/{token}", async context =>
{
  var path = Path.Combine(app.Environment.WebRootPath, "shared.html");
  context.Response.ContentType = "text/html";
  await context.Response.SendFileAsync(path);
});
app.MapGet("/", () => "Hello World!");
app.MapControllers();
app.Run();