namespace Application.DTOs;

public class InternalQuizDetailsDto
{
    public int Id { get; set; }
    public ICollection<InternalQuestionDto> Questions { get; set; } = new List<InternalQuestionDto>();
}