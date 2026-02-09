using Backend.Services;
using Microsoft.AspNetCore.Http.Features;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(); // Agregar SwaggerGen
builder.Services.AddScoped<IPdfParserService, PdfParserService>();

// Configure form options to handle file uploads in memory (avoid temp file permission issues)
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52428800; // 50 MB max file size
    options.MemoryBufferThreshold = 52428800; // Keep in memory up to 50 MB
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        builder =>
        {
            builder.WithOrigins("http://localhost:4200")
                   .AllowAnyHeader()
                   .AllowAnyMethod();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger(); // Generar JSON v2/v3 para Swashbuckle
    app.UseSwaggerUI(); // Habilitar UI en /swagger
    // app.MapScalarApiReference(); // Temporarily disabled due to restore lock
}

app.UseHttpsRedirection(); // Optional: might disable if certificate issues arise locally

app.UseCors("AllowAngular");

app.UseAuthorization();

app.MapControllers();

app.Run();
