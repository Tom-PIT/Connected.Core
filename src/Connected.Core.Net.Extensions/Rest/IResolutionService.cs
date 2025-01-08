using Connected.Annotations;
using Microsoft.AspNetCore.Http;
using System.Collections.Immutable;
using System.Reflection;

namespace Connected.Net.Rest;

[Service(ServiceRegistrationScope.Singleton)]
internal interface IResolutionService
{
	Task<InvokeDescriptor?> SelectMethod(HttpContext context);
	Task<Type?> SelectDto(ParameterInfo parameter);
	Task<ImmutableList<Tuple<string, ServiceOperationVerbs>>> QueryRoutes();
}
