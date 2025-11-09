using System.Net.Http.Json;
using Application.DTOs;
using Application.Interfaces;

namespace Infrastructure.HttpClients
{
    public class QuizManagerClient : IQuizManagerClient
    {
        private readonly HttpClient _httpClient;

        public QuizManagerClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<InternalQuizDetailsDto?> GetQuizDetailsAsync(int quizId)
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<InternalQuizDetailsDto>(
                    $"api/quizzes/internal/{quizId}/details");
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }
    }
}