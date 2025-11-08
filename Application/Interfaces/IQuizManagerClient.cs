using Application.DTOs;

namespace Application.Interfaces;

public interface IQuizManagerClient
{
    Task<InternalQuizDetailsDto?> GetQuizDetailsAsync(int quizId);
}