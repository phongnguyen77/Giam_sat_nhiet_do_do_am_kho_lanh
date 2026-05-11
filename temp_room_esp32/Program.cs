using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSession();

// register EF Core Sqlite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Sqlite") ?? "Data Source=temperature.db"));

builder.Services.AddSingleton<MqttDataService>();

// SignalR
builder.Services.AddSignalR();

var app = builder.Build();

// ensure database created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

var mqttService = app.Services.GetRequiredService<MqttDataService>();
_ = mqttService.StartAsync(); // chạy không chờ

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseSession();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<MqttHub>("/mqttHub");

app.Run();
