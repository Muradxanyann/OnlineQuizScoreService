using Application.DTOs;
using Application.Interfaces;
using AutoMapper;
using Domain;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public class ScoringService : IScoringService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IQuizManagerClient _quizManagerClient;
    private readonly IMapper _mapper;
    private readonly ILogger<ScoringService> _logger;

    public ScoringService(
        IUnitOfWork unitOfWork,
        IQuizManagerClient quizManagerClient,
        IMapper mapper,
        ILogger<ScoringService> logger)
    {
        _unitOfWork = unitOfWork;
        _quizManagerClient = quizManagerClient;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task ProcessSubmissionAsync(QuizSubmittedEvent submission)
    {
        _logger.LogInformation($"Начат подсчет очков для QuizId: {submission.QuizId}, UserId: {submission.UserId}");

        // 1. Получаем правильные ответы от QuizManager.Api
        var correctQuiz = await _quizManagerClient.GetQuizDetailsAsync(submission.QuizId);
        if (correctQuiz == null)
        {
            _logger.LogError($"Квиз с Id {submission.QuizId} не найден в QuizManager.Api");
            return; // (Или отправить в "dead-letter" очередь)
        }

        // 2. Создаем словарь правильных ответов для O(1) поиска
        var correctAnswers = correctQuiz.Questions
            .SelectMany(q => q.Options.Where(o => o.IsCorrect))
            .ToDictionary(o => o.Id, o => o.Id); // [Key: OptionId]

        // 3. Считаем очки
        int score = 0;
        var userAnswers = new List<UserAnswer>();

        foreach (var submittedAnswer in submission.Answers)
        {
            if (correctAnswers.ContainsKey(submittedAnswer.SelectedOptionId))
            {
                score++; // +1 балл за правильный ответ
            }
            
            userAnswers.Add(new UserAnswer
            {
                QuestionId = submittedAnswer.QuestionId,
                OptionId = submittedAnswer.SelectedOptionId
            });
        }

        // 4. Готовим Агрегат (Корень + Ветки)
        var userResult = new UserResult
        {
            UserId = submission.UserId,
            QuizId = submission.QuizId,
            Score = score,
            CompletedAt = submission.SubmittedAt,
            UserAnswers = userAnswers // Добавляем ответы
        };

        // 5. Сохраняем ВСЕ ОДНОЙ ТРАНЗАКЦИЕЙ
        try
        {
            // Репозиторий должен быть написан так,
            // чтобы он сохранил UserResult и UserAnswers за один вызов
            await _unitOfWork.UserResults.AddResultAsync(userResult);
            await _unitOfWork.CommitAsync(); // Коммитим UoW
            
            _logger.LogInformation($"Результат (Счет: {score}) для QuizId: {submission.QuizId} сохранен.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка сохранения результата в БД.");
            _unitOfWork.Rollback();
        }
    }
}
