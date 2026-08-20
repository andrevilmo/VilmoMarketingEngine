using Microsoft.EntityFrameworkCore;
using Vilmo.Api;
using Vilmo.Data;
using Vilmo.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddVilmo(builder.Configuration);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));

var app = builder.Build();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapVilmo();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SeedData.ApplyAsync(db, app.Configuration);
}

app.Run();

public partial class Program;
