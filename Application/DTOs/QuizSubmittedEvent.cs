namespace Application.DTOs;

public class QuizSubmittedEvent
{
    public int QuizId { get; set; }
    public int UserId { get; set; }
    public DateTime SubmittedAt { get; set; }
    public List<UserAnswerDto> Answers { get; set; } = new List<UserAnswerDto>();
}