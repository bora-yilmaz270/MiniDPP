using Microsoft.EntityFrameworkCore;
using MiniDPP.Web.Data;
using MiniDPP.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")
        ?? "Data Source=minidpp.db"));
builder.Services.AddScoped<CsvImportService>();
builder.Services.AddScoped<DppService>();

var app = builder.Build();

// A tiny assessment app: create the SQLite file automatically on first run.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AppDbContext>()
        .Database.EnsureCreatedAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();

// Allows WebApplicationFactory-based integration tests later.
public partial class Program { }
