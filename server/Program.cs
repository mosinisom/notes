using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<QRCodeGeneratorService>();

var app = builder.Build();

app.UseWebSockets();

app.UseStaticFiles();

app.MapGet("/", () => "Hello World!");

app.MapControllers();

app.Run();