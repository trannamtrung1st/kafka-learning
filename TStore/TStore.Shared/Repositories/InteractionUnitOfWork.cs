using System.Threading.Tasks;
using TStore.Shared.Persistence;

namespace TStore.Shared.Repositories
{
    public interface IInteractionUnitOfWork : IUnitOfWork
    {
    }

    public class InteractionUnitOfWork : IInteractionUnitOfWork
    {
        protected readonly InteractionContext _interactionContext;

        public InteractionUnitOfWork(InteractionContext interactionContext)
        {
            _interactionContext = interactionContext;
        }

        public Task<int> SaveChangesAsync()
        {
            return _interactionContext.SaveChangesAsync();
        }
    }
}
