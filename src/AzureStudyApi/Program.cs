using AzureStudyApi.Services;
using AzureStudyDomain.Models;
using AzureStudyInfra.Configurations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Azure.Identity;
using Azure.Messaging.ServiceBus;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddScoped<BlobStorageService>();
builder.Services.AddDbContext<AppDbContext>(
    options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"), 
            sql => sql.EnableRetryOnFailure()));

var keyVaultUrl = builder.Configuration["KeyVaultUrl"];
builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUrl!), new DefaultAzureCredential());
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new ServiceBusClient(config["ServiceBusConnectionString"]!);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation(
        "Request {Method} {Path} at {Time}",
        context.Request.Method,
        context.Request.Path,
        DateTime.UtcNow);
    await next();
});

app.MapHealthChecks("/health");

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapGet("/config-secret", async (IConfiguration config) =>
{
    return Results.Ok(config["OpenAiApiKey"]);
});

//app.MapGet("/secret-test", async (IConfiguration config) =>
//{
//    var keyVaultUrl = config["KeyVaultUrl"];

//    var client = new SecretClient(new Uri(keyVaultUrl!), new DefaultAzureCredential());

//    var secret = await client.GetSecretAsync("OpenAiApiKey");
//    return Results.Ok(new { 
//        SecretName = secret.Value.Name,
//        SecretValue = secret.Value.Value
//    });
//});

app.MapPost("/orders", async (ServiceBusClient client) =>
{
    var sender = client.CreateSender("orders");

    var message = new ServiceBusMessage(
        """
        {
            "orderId": 1,
            "customer": "Luciano",
            "total": 100
        }
        """);
    await sender.SendMessageAsync(message);

    return Results.Ok("Mensagem enviada");
});

app.MapPost("/upload", async (IFormFile file, [FromServices] BlobStorageService storage) =>
{
    await storage.UploadAsync(file.FileName, file.OpenReadStream());

    return Results.Ok(new
    {
        file.FileName,
        file.Length
    });
})
.DisableAntiforgery();

app.MapGet("/files", async ([FromServices] BlobStorageService storage) =>
{
    var files = await storage.ListAsync();

    return Results.Ok(files);
});

app.MapGet("/files/{fileName}/download-url", async ([FromRoute] string fileName, [FromServices] BlobStorageService storage) =>
{
    var url = await storage.GenerateDownloadUrlAsync(fileName);
    return Results.Ok(new { Url = url });
});

app.MapGet("/testlog", (ILogger<Program> logger) =>
{
    logger.LogInformation("Endpoint /testlog chamado em {Time}", DateTime.UtcNow);
    return Results.Ok("Log gerado");
});

app.MapGet("/config", (IConfiguration config) =>
{
	return Results.Ok(new 
	{
		Version = config["ApiSettings:Version"]
	});
});

app.MapGet("/db", (IConfiguration config) => {
    return Results.Ok(new 
	{
		ConnectionString = config.GetConnectionString("DefaultConnection")
	});
});

app.MapPost("/products", async(Product product, AppDbContext db) =>
{
    db.Products.Add(product);
    await db.SaveChangesAsync();
    return Results.Created($"/products/{product.Id}", product);
});

app.MapPut("/products/{id}", async (int id, Product request, AppDbContext db) =>
{
    var product = await db.Products.FindAsync(id);

    if (product is null)
        return Results.NotFound();

    product.Name = request.Name;
    product.Price = request.Price;
    product.Category = request.Category;

    await db.SaveChangesAsync();

    return Results.NoContent();
});


app.MapDelete("/products/{id}", async (int id, AppDbContext db) =>
{
    var product = await db.Products.FindAsync(id);

    if (product is null)
        return Results.NotFound();

    db.Products.Remove(product);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.MapGet("/products",
async (AppDbContext db) =>
{
    return await db.Products.AsNoTracking().ToListAsync();
});

app.MapGet("/products-summary",
async (AppDbContext db) =>
{
    return await db.Products.AsNoTracking().OrderBy(o => o.Id).Select(s => new { s.Name, s.Price }).ToListAsync();
});

//app.MapGet("/health", () =>
//{
//    return Results.Ok(new
//    {
//        Status = "Healthy",
//        Time = DateTime.UtcNow
//    });
//});

app.MapGet("/version", () =>
{
    return Results.Ok("2.0.0");
});

app.MapGet("/env", (IHostEnvironment env) =>
{
    return Results.Ok(new
    {
        Environment = env.EnvironmentName
    });
});

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
