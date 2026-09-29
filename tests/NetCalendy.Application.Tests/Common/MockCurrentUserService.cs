using NetCalendy.Application.Common;

namespace NetCalendy.Application.Tests.Common;

public sealed class MockCurrentUserService(Guid userId) : ICurrentUserService
{
    public Guid? UserId => userId;
    public bool IsAuthenticated => true;
}
