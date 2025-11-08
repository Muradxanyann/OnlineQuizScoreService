using Application.DTOs;

namespace Application.Interfaces;

public interface IScoringService
{
    Task ProcessSubmissionAsync(QuizSubmittedEvent submission);
}