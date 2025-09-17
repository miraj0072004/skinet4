using API.Middleware;
using Core.Interfaces;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddDbContext<StoreContext>(opt =>
{
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddCors();
builder.Services.AddSingleton<IConnectionMultiplexer>(config =>
{
    var consString = builder.Configuration.GetConnectionString("Redis");
    if (string.IsNullOrEmpty(consString))
    {
        throw new InvalidOperationException("Redis connection string is not configured.");
    }
    var configuration = ConfigurationOptions.Parse(consString, true);
    return ConnectionMultiplexer.Connect(configuration);

});

builder.Services.AddSingleton<ICartService, CartService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMiddleware<ExceptionMiddleware>();

app.UseCors(x => x.AllowAnyHeader().AllowAnyMethod().WithOrigins("http://localhost:4200","https://localhost:4200"));

app.MapControllers();

try
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<StoreContext>();

    // Retry logic for database migration and seeding
    var maxRetries = 10;
    var retryDelay = TimeSpan.FromSeconds(5);
    var retries = 0;
    var dbReady = false;

    while (!dbReady && retries < maxRetries)
    {
        try
        {
            Console.WriteLine("Attempting to apply migrations...");
            await context.Database.MigrateAsync();
            Console.WriteLine("Migrations applied successfully!");

            Console.WriteLine("Seeding database...");
            await StoreContextSeed.SeedAsync(context);
            Console.WriteLine("Database seeded successfully!");
            dbReady = true;
        }
        catch (Exception ex)
        {
            retries++;
            Console.WriteLine($"Database not ready. Retrying in {retryDelay.TotalSeconds} seconds... ({retries}/{maxRetries})");
            Console.WriteLine($"Error: {ex.Message}");

            if (retries >= maxRetries)
            {
                throw new Exception("Failed to connect to the database after multiple retries.", ex);
            }

            await Task.Delay(retryDelay);
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine("Application failed to start due to database issues:");
    Console.WriteLine(ex);
    throw;
}

app.Run();
