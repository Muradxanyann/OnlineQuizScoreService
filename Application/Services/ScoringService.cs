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
        _logger.LogInformation("Начат подсчет очков для QuizId: {QuizId}, UserId: {UserId}",
            submission.QuizId, submission.UserId);

        try
        {
            // 1. Получаем правильные ответы от QuizManager.Api
            _logger.LogInformation("Запрос деталей квиза {QuizId} у QuizManager API", submission.QuizId);
            var correctQuiz = await _quizManagerClient.GetQuizDetailsAsync(submission.QuizId);

            if (correctQuiz == null)
            {
                _logger.LogError("Квиз с Id {QuizId} не найден в QuizManager.Api", submission.QuizId);
                return;
            }

            _logger.LogInformation("Получены детали квиза {QuizId}, вопросов: {QuestionCount}",
                submission.QuizId, correctQuiz.Questions.Count);

            // 2. Создаем словарь правильных ответов
            var correctAnswers = correctQuiz.Questions
                .SelectMany(q => q.Options.Where(o => o.IsCorrect))
                .ToDictionary(o => o.Id, o => o.Id);

            // 3. Считаем очки
            int score = 0;
            var userAnswers = new List<UserAnswer>();

            foreach (var submittedAnswer in submission.Answers)
            {
                if (correctAnswers.ContainsKey(submittedAnswer.SelectedOptionId))
                {
                    score++;
                }

                userAnswers.Add(new UserAnswer
                {
                    QuestionId = submittedAnswer.QuestionId,
                    OptionId = submittedAnswer.SelectedOptionId
                });
            }

            _logger.LogInformation("Подсчет завершен: UserId {UserId}, Score: {Score}/{Total}",
                submission.UserId, score, submission.Answers.Count);

            var userResult = new UserResult
            {
                UserId = submission.UserId,
                QuizId = submission.QuizId,
                Score = score,
                CompletedAt = submission.SubmittedAt,
                UserAnswers = userAnswers
            };

            // 4. Сохраняем результат
            _unitOfWork.BeginTransaction();
            try
            {
                await _unitOfWork.UserResults.AddResultAsync(userResult);
                await _unitOfWork.CommitAsync();

                _logger.LogInformation("Результат сохранен в БД: UserId {UserId}, QuizId {QuizId}, Score {Score}",
                    submission.UserId, submission.QuizId, score);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка сохранения результата в БД для UserId {UserId}", submission.UserId);
                _unitOfWork.Rollback();
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка при обработке submission: QuizId {QuizId}, UserId {UserId}",
                submission.QuizId, submission.UserId);
            throw;
        }
    }
}