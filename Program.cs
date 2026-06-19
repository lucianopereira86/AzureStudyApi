using AzureStudyApi.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddScoped<BlobStorageService>();

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
