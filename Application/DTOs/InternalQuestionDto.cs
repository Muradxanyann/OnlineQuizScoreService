namespace Application.DTOs;

public class InternalQuestionDto
{
    public int Id { get; set; }
    public ICollection<InternalOptionDto> Options { get; set; } = new List<InternalOptionDto>();
}