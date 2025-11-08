using System.Text;
using System.Text.Json;
using Application.DTOs;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Infrastructure 
{
    public class QuizSubmissionConsumer : BackgroundService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<QuizSubmissionConsumer> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private IConnection _connection = null!;
        private IModel _channel = null!;

        public QuizSubmissionConsumer(IConfiguration configuration, ILogger<QuizSubmissionConsumer> logger, IServiceScopeFactory scopeFactory)
        {
            _configuration = configuration;
            _logger = logger;
            _scopeFactory = scopeFactory;
            InitializeRabbitMq();
        }

        private void InitializeRabbitMq()
        {
            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = _configuration["RabbitMQ:Host"],
                    Port = int.Parse(_configuration["RabbitMQ:Port"]!)
                };
                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _channel.ExchangeDeclare("quiz_exchange", ExchangeType.Direct);
                
                var queueName = "scoring_queue";
                _channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);
                
                _channel.QueueBind(queueName, "quiz_exchange", "quiz.submitted");
                _logger.LogInformation("RabbitMQ Consumer инициализирован.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Не удалось инициализировать RabbitMQ Consumer.");
            }
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_channel == null)
            {
                _logger.LogError("RabbitMQ канал не инициализирован. Consumer не запускается.");
                return Task.CompletedTask;
            }

            var consumer = new EventingBasicConsumer(_channel);

            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                _logger.LogInformation("Получено сообщение: {Message}", message);

                try
                {
                    // Де сериализация с учетом регистра
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    var submissionEvent = JsonSerializer.Deserialize<QuizSubmittedEvent>(message, options);

                    if (submissionEvent != null)
                    {
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var scoringService = scope.ServiceProvider.GetRequiredService<IScoringService>();
                            await scoringService.ProcessSubmissionAsync(submissionEvent);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Не удалось деморализовать сообщение.");
                    }
                    
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "Ошибка дематериализации JSON.");
                    _channel.BasicNack(ea.DeliveryTag, false, false); // false - не возвращать в очередь
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка обработки сообщения RabbitMQ.");
                    _channel.BasicNack(ea.DeliveryTag, false, true); 
                }
            };

            _channel.BasicConsume("scoring_queue", false, consumer);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}