namespace Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IUserResultRepository UserResults { get; }

    void BeginTransaction();
    Task CommitAsync();
    void Rollback();
}
