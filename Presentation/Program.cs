using Application.Interfaces;
using Application.Services;
using Domain.Interfaces;
using Infrastructure; 
using Infrastructure.Repositories; 
using Infrastructure.HttpClients;
using Microsoft.OpenApi.Models;
using System.Data;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
Console.WriteLine("=== Configuration Check ===");
Console.WriteLine($"RabbitMQ Host: {builder.Configuration["RabbitMQ:Host"]}");
Console.WriteLine($"RabbitMQ Port: {builder.Configuration["RabbitMQ:Port"]}");
Console.WriteLine($"QuizManagerApi: {builder.Configuration["ServiceUrls:QuizManagerApi"]}");
Console.WriteLine("============================");


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Scoring Service API", Version = "v1" });
});

// Database
builder.Services.AddScoped<IDbConnection>(provider => 
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    return new NpgsqlConnection(connectionString);
});

builder.Services.AddSingleton<DapperDbContext>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// HttpClient для связи с QuizManager.Api
builder.Services.AddHttpClient<IQuizManagerClient, QuizManagerClient>(client =>
{
    var quizManagerUrl = builder.Configuration["ServiceUrls:QuizManagerApi"] 
                         ?? "http://quiz-manager-api:8080";
    client.BaseAddress = new Uri(quizManagerUrl);
});

// Application Services
builder.Services.AddScoped<IScoringService, ScoringService>();

// AutoMapper
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// RabbitMQ Consumer
//builder.Services.AddHostedService<QuizSubmissionConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Scoring API v1"));
}

app.MapControllers();

app.Run();