using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Text;
using System.Text.Json;
using System.Net.Sockets;
using Application.DTOs;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure
{
    public class QuizSubmissionConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<QuizSubmissionConsumer> _logger;
        private readonly IConfiguration _configuration;
        private IConnection? _connection;
        private IModel? _channel;
        private const string QueueName = "quiz_submissions";

        public QuizSubmissionConsumer(
            IServiceProvider serviceProvider,
            ILogger<QuizSubmissionConsumer> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Запуск RabbitMQ Consumer...");

            // Увеличиваем начальную задержку
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            
            var success = await InitializeRabbitMqWithRetry(stoppingToken);
            
            if (!success)
            {
                _logger.LogError("Не удалось инициализировать RabbitMQ канал после всех попыток");
                return;
            }

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                
                try
                {
                    await ProcessMessage(message);
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка обработки сообщения: {Message}", message);
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);
            
            _logger.LogInformation("RabbitMQ Consumer запущен и слушает очередь: {QueueName}", QueueName);

            // Бесконечное ожидание
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        private async Task<bool> InitializeRabbitMqWithRetry(CancellationToken stoppingToken)
        {
            const int maxRetries = 15; // Увеличиваем количество попыток
            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    _logger.LogInformation("Попытка подключения к RabbitMQ {Retry}/{MaxRetries}", 
                        retry + 1, maxRetries);
                    
                    InitializeRabbitMq();
                    _logger.LogInformation("Успешное подключение к RabbitMQ");
                    return true;
                }
                catch (BrokerUnreachableException ex)
                {
                    _logger.LogWarning("Попытка {Retry}/{MaxRetries} подключения к RabbitMQ не удалась: {Message}", 
                        retry + 1, maxRetries, ex.InnerException?.Message ?? ex.Message);
                    
                    if (retry == maxRetries - 1)
                    {
                        _logger.LogError("Не удалось подключиться к RabbitMQ после {MaxRetries} попыток", maxRetries);
                        return false;
                    }
                    
                    // Увеличиваем задержку с каждой попыткой
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, retry));
                    _logger.LogInformation("Следующая попытка через {Delay} секунд", delay.TotalSeconds);
                    await Task.Delay(delay, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Неожиданная ошибка при подключении к RabbitMQ");
                    if (retry == maxRetries - 1) return false;
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
            return false;
        }

        private void InitializeRabbitMq()
        {
            var host = _configuration["RabbitMQ:Host"] ?? "quiz-rabbitmq";
            var port = int.Parse(_configuration["RabbitMQ:Port"] ?? "5673");
            var user = _configuration["RabbitMQ:User"] ?? "guest";
            var password = _configuration["RabbitMQ:Password"] ?? "guest";

            _logger.LogInformation("Подключение к RabbitMQ: {Host}:{Port}, User: {User}", 
                host, port, user);

            var factory = new ConnectionFactory()
            {
                HostName = host,
                Port = port,
                UserName = user,
                Password = password,
                VirtualHost = "/",
                DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                RequestedHeartbeat = TimeSpan.FromSeconds(60)
            };

            _connection = factory.CreateConnection("ScoringService");
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(exchange: "quiz_exchange", type: ExchangeType.Direct, durable: true);
            _channel.QueueDeclare(queue: QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            _channel.QueueBind(queue: QueueName, exchange: "quiz_exchange", routingKey: "quiz_submitted");
            
            _channel.QueueDeclare(queue: QueueName,
                                durable: true,
                                exclusive: false,
                                autoDelete: false,
                                arguments: null);
                                
            _logger.LogInformation("Очередь {QueueName} объявлена", QueueName);
        }

        private async Task ProcessMessage(string message)
        {
            using var scope = _serviceProvider.CreateScope();
            var scoringService = scope.ServiceProvider.GetRequiredService<IScoringService>();
            
            try
            {
                var submission = JsonSerializer.Deserialize<QuizSubmittedEvent>(message);
                if (submission != null)
                {
                    await scoringService.ProcessSubmissionAsync(submission);
                    _logger.LogInformation("Сообщение обработано: QuizId {QuizId}, UserId {UserId}", 
                        submission.QuizId, submission.UserId);
                }
                else
                {
                    _logger.LogWarning("Не удалось десериализовать сообщение: {Message}", message);
                }
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "Ошибка десериализации JSON сообщения: {Message}", message);
            }
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}