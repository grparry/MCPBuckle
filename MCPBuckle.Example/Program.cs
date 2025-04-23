using MCPBuckle.Extensions;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "MCPBuckle Example API", Version = "v1" });
    
    // Enable XML comments
    var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
});

// Configure MCPBuckle with options
builder.Services.AddMcpBuckle(options =>
{
    // Customize the route prefix
    options.RoutePrefix = "/.well-known/mcp-context";
    
    // Include controller name in tool name
    options.IncludeControllerNameInToolName = true;
    
    // Add custom metadata
    options.Metadata.Add("contact", new Dictionary<string, string>
    {
        { "name", "MCPBuckle Team" },
        { "url", "https://github.com/example/mcpbuckle" }
    });
    
    // Include property descriptions
    options.IncludePropertyDescriptions = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Use MCPBuckle middleware to expose the MCP context at /.well-known/mcp-context
app.UseMcpBuckle();

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

// Map controller routes
app.MapControllers();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
