using proset.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("secrets.json");

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<SqlContext>(options =>
   options.UseNpgsql(builder.Configuration.GetConnectionString("SqlContext")));
builder.Services.AddSingleton<IEventEmitter, GameEventEmitter>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "room",
    pattern: "room/{*room_id}",
    defaults: new { controller = "Room", action = "Index" }
);
app.MapControllerRoute(
    name: "room-api-sse",
    pattern: "api/sse/{*room_id}",
    defaults: new { controller = "RoomApi", action = "SSE" }
);
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();
