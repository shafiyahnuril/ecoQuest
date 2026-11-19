using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Contracts.Repositories;

public interface ILevelRepository
{
    Task<IReadOnlyList<Level>> GetAllAsync(CancellationToken ct);
}
