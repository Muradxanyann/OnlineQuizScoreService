namespace Domain.Interfaces;

public interface IUserResultRepository
{
    
    Task<int> AddResultAsync(UserResult result);
}