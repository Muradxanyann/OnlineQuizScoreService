namespace Domain;

public class UserResult
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int QuizId { get; set; }
    public int Score { get; set; } 
    public int TimeSpent { get; set; } 
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    // Навигационное свойство
    public ICollection<UserAnswer> UserAnswers { get; set; } = new List<UserAnswer>();
}
