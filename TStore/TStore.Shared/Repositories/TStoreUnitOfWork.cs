using System.Threading.Tasks;
using TStore.Shared.Persistence;

namespace TStore.Shared.Repositories
{
    public interface ITStoreUnitOfWork : IUnitOfWork
    {
    }

    public class TStoreUnitOfWork : ITStoreUnitOfWork
    {
        protected readonly TStoreContext _storeContext;

        public TStoreUnitOfWork(TStoreContext storeContext)
        {
            _storeContext = storeContext;
        }

        public Task<int> SaveChangesAsync()
        {
            return _storeContext.SaveChangesAsync();
        }
    }
}
