
using Application.Interfaces;
using Application.Services;
using Domain.Interfaces;
using Infrastructure; 
using Infrastructure.Repositories; 
using Infrastructure.HttpClients;
using Microsoft.OpenApi.Models;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Scoring Service API", Version = "v1" });
});

// AutoMapper
builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

// --- Регистрация сервисов для ScoringService ---

// Infrastructure
builder.Services.AddSingleton<DapperDbContext>(); 
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>(); 

// HttpClient для связи с QuizManager.Api
builder.Services.AddHttpClient<IQuizManagerClient, QuizManagerClient>(client =>
{
    var quizManagerUrl = builder.Configuration["ServiceUrls:QuizManagerApi"];
    if (string.IsNullOrEmpty(quizManagerUrl))
    {
        throw new InvalidOperationException("ServiceUrls:QuizManagerApi не задан в конфигурации.");
    }
    client.BaseAddress = new Uri(quizManagerUrl);
});

// Application
builder.Services.AddScoped<IScoringService, ScoringService>();

// --- RabbitMQ Consumer ---
// Регистрируем наш "слушатель" RabbitMQ
builder.Services.AddHostedService<QuizSubmissionConsumer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Scoring API v1"));
}

// Убрал UseHttpsRedirection(), т.к. в Docker
// app.UseHttpsRedirection();

app.MapControllers();

app.Run();