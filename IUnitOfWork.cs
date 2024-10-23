namespace UnitOfWork
{
    public interface IUnitOfWork
    {
        public Task Begin();
        public Task Commit();
        public Task Rollback();
    }
}
