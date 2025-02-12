using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("DefaultConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("DefaultConnection"))));

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80);
    //options.ListenAnyIP(443);
});

builder.Services.AddControllers();
builder.Services.AddControllers()
    .AddXmlSerializerFormatters(); // Enable XML output support

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Enable CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});


builder.Services.AddSingleton<DatabaseMigrationService>();
builder.Services.AddHttpClient<TcmbService>(); // Register HttpClient for TcmbService
builder.Services.AddHostedService<TcmbService>(); // Register as background service
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect("redis:6379"));

var app = builder.Build();

app.UseCors("AllowAll"); // Apply CORS policy

var migrationService = app.Services.GetRequiredService<DatabaseMigrationService>();
await migrationService.MigrateDatabaseAsync();

//if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
