using Domain;
using Domain.Interfaces;

namespace Infrastructure.Repositories
{
    using Dapper;
    using System.Data;

    public class DapperUserResultRepository : IUserResultRepository
    {
        private readonly IDbConnection _connection;
        private IDbTransaction? _transaction;

        public DapperUserResultRepository(IDbConnection connection, IDbTransaction? transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        public void SetTransaction(IDbTransaction? transaction)
        {
            _transaction = transaction;
        }

        public async Task<int> AddResultAsync(UserResult result)
        {

            const string sqlResult = """

                                         INSERT INTO UserResults (UserId, QuizId, Score, TimeSpent, CompletedAt)
                                         VALUES (@UserId, @QuizId, @Score, @TimeSpent, @CompletedAt)
                                         RETURNING id;
                                     """;

            var resultId = await _connection.QuerySingleAsync<int>(sqlResult, result, _transaction);
            result.Id = resultId;

            if (result.UserAnswers.Count == 0) return resultId;
            // 2. Вставляем "детей" (UserAnswers)
            foreach (var answer in result.UserAnswers)
            {
                answer.UserResultId = resultId;
            }

            var sqlAnswer = """
                                    INSERT INTO UserAnswers (UserResultId, QuestionId, OptionId)
                                    VALUES (@UserResultId, @QuestionId, @OptionId)
                            """;

            await _connection.ExecuteAsync(sqlAnswer, result.UserAnswers, _transaction);

            return resultId;
        }
    }
}