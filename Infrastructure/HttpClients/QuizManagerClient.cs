using System.Net.Http.Json;
using Application.DTOs;
using Application.Interfaces;

namespace Infrastructure.HttpClients
{
    // Реализация HTTP-клиента
    public class QuizManagerClient : IQuizManagerClient
    {
        private readonly HttpClient _httpClient;

        public QuizManagerClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            // Адрес будет внедрен из Program.cs
        }

        public async Task<InternalQuizDetailsDto?> GetQuizDetailsAsync(int quizId)
        {
            try
            {
                // Вызываем *внутренний* endpoint, который мы создали в QuizManager
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