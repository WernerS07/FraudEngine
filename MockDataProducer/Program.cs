using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddControllers();

// Register Worker as a singleton so controller can access it
builder.Services.AddSingleton<Worker>();
builder.Services.AddHostedService<Worker>(sp => sp.GetRequiredService<Worker>());


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//app.UseHttpsRedirection();

// Configure the HTTP request pipeline
app.MapControllers(); // This maps controller routes

app.Run();