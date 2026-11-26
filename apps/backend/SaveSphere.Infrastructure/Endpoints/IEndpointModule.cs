using Microsoft.AspNetCore.Routing;

namespace SaveSphere.Infrastructure.Endpoints;

/// <summary>
/// A vertical slice's HTTP surface. The API maps every registered module under the
/// <c>/api/v1</c> group (ADR-0002); modules never choose their own root prefix.
/// </summary>
public interface IEndpointModule
{
    void MapEndpoints(IEndpointRouteBuilder routes);
}
