namespace Domain;

public class UserAnswer
{
    public int Id { get; set; }
    public int UserResultId { get; set; }
    public UserResult UserResult { get; set; } = null!;
        
    public int QuestionId { get; set; }
    public int OptionId { get; set; }
   
}