using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Repositories;
using EcoQuest.Core.Domain;
using EcoQuest.Data.Errors;
using EcoQuest.Services;
using NSubstitute;
using Xunit;

namespace EcoQuest.Tests.Services;

public class ReferenceDataServiceTests
{
    private readonly IActivityTypeRepository _typesRepo = Substitute.For<IActivityTypeRepository>();
    private readonly IActivityCategoryRepository _categoriesRepo = Substitute.For<IActivityCategoryRepository>();
    private readonly ReferenceDataService _service;

    public ReferenceDataServiceTests()
    {
        _service = new ReferenceDataService(_typesRepo, _categoriesRepo);
    }

    [Fact]
    public async Task GetActivityTypesAsync_orders_by_DisplayOrder_and_maps_CategoryName()
    {
        var cat1 = new ActivityCategory(1, "Transportasi");
        var cat2 = new ActivityCategory(2, "Energi");
        _categoriesRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ActivityCategory>>(new[] { cat1, cat2 }));

        var type2 = new ActivityType(2, "Lampu", 2, InputControlType.TimeRange, "jam", 0.013m, 0.8m, 12m, 24m, "Desc 2", 2);
        var type1 = new ActivityType(1, "Sepeda", 1, InputControlType.Numeric, "km", 0.15m, 1.0m, 30m, 300m, "Desc 1", 1);
        _typesRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ActivityType>>(new[] { type2, type1 }));

        var result = await _service.GetActivityTypesAsync();

        Assert.True(result.IsSuccess);
        var items = result.Value!;
        Assert.Equal(2, items.Count);
        Assert.Equal(1, items[0].DisplayOrder);
        Assert.Equal("Sepeda", items[0].TypeName);
        Assert.Equal("Transportasi", items[0].CategoryName);
        Assert.Equal(2, items[1].DisplayOrder);
        Assert.Equal("Lampu", items[1].TypeName);
        Assert.Equal("Energi", items[1].CategoryName);
    }

    [Fact]
    public async Task GetActivityTypesAsync_returns_failure_when_data_access_exception_thrown()
    {
        _typesRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<ActivityType>>(_ => throw new DataAccessException(ErrorCode.NetworkError, "activitytypes.get"));

        var result = await _service.GetActivityTypesAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCode.NetworkError, result.Error);
        Assert.Equal(Messages.NetworkError, result.Message);
    }
}
