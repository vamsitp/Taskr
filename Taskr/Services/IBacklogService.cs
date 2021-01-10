namespace Taskr
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IBacklogService
    {
        public AccountType AccountType { get; }

        Task<List<WorkItem>> GetWorkItems(Account account, CancellationToken cancellationToken);
    }
}