using Connected.Annotations;
using Connected.Reflection;
using Connected.Runtime;
using Connected.Services;
using Microsoft.AspNetCore.Http;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;

namespace Connected.Net.Rest;

internal class ResolutionService(IRuntimeService runtimeService) : IResolutionService
{
	private bool IsInitialized { get; set; }
	private Dictionary<string, List<InvokeDescriptor>> Methods { get; } = new(StringComparer.OrdinalIgnoreCase);
	private Dictionary<string, List<DtoRestDescriptor>> Dtos { get; } = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	/// This method tries to resolve argument implementation type based on the parameter's type interface.
	/// </summary>
	/// <param name="parameter">The implementation parameter of the method which declares the argument</param>
	/// <returns><see cref="Type"/> that implements <paramref name="parameter"/>'s type interface.</returns>
	public async Task<Type?> SelectDto(ParameterInfo parameter)
	{
		await Initialize();

		if (!Dtos.ContainsKey(DtoName(parameter.ParameterType)))
			return null;

		var items = Dtos[DtoName(parameter.ParameterType)];
		/*,
		 * If the interafce has only one implementation the air is clear.
		 */
		if (items.Count == 1)
			return WrapDto(items[0].Type, parameter);
		/*
		 * We have more than one implementation. We'll try to find the implementation that match
		 * the assembly of the invoking method. This is the most probable scenario.
		 */
		foreach (var dto in items)
		{
			if (dto.Type.Assembly == parameter.ParameterType.Assembly)
				return WrapDto(dto.Type, parameter);
		}
		/*
		 * Method's assembly doesn't have an implementation, let's try to look in the
		 * interface's assembly.
		 */
		foreach (var dto in items)
		{
			if (dto.Type.Assembly == parameter.ParameterType.Assembly)
				return WrapDto(dto.Type, parameter);
		}
		/*
		 * Nope, there must be some intermediate assembly implementing the argument and it surely must
		 * be referenced by the method's assembly.
		 */
		return WrapDto(items[0].Type, parameter);
	}

	private static Type WrapDto(Type dto, ParameterInfo parameter)
	{
		if (!dto.IsGenericType)
			return dto;

		return dto.MakeGenericType(parameter.ParameterType.GetGenericArguments());
	}

	public async Task<InvokeDescriptor?> SelectMethod(HttpContext context)
	{
		await Initialize();

		var route = context.Request.Path.Value;

		if (route is null)
			return null;

		var routeValue = route.ToString();

		if (!Methods.TryGetValue(routeValue, out List<InvokeDescriptor>? descriptor))
		{
			if (!routeValue.EndsWith("/dto"))
				return null;

			if (!string.Equals(context.Request.Method, "GET", StringComparison.OrdinalIgnoreCase))
				return null;

			if (Methods.TryGetValue(routeValue[..^4], out List<InvokeDescriptor>? dtoDescriptor) && dtoDescriptor is not null)
				return dtoDescriptor[0];

			return null;
		}

		//TODO: map overloads from arguments
		return descriptor[0];
	}

	private async Task Initialize()
	{
		if (IsInitialized)
			return;

		IsInitialized = true;

		var startups = await runtimeService.QueryStartups();

		foreach (var startup in startups)
		{
			foreach (var type in startup.GetType().Assembly.GetTypes())
			{
				if (type.IsAbstract)
					continue;

				if (type.ImplementsInterface<IDto>())
					InitializeDto(type);

				foreach (var itf in type.GetInterfaces())
				{
					if (itf.GetCustomAttribute<ServiceAttribute>() is not null)
					{
						InitializeService(type);
						break;
					}
				}
			}
		}
	}

	private void InitializeDto(Type type)
	{
		if (type.GetImplementedDtos() is not List<Type> dtos || dtos.Count == 0)
			return;

		foreach (var dto in dtos)
		{
			var name = DtoName(dto);

			if (Dtos.TryGetValue(name, out _))
				Dtos[name].Add(new DtoRestDescriptor(type));
			else
				Dtos.Add(name, [new DtoRestDescriptor(type)]);
		}
	}

	private void InitializeService(Type type)
	{
		if (type.GetImplementedServices() is not List<Type> services || services.Count == 0)
			return;

		foreach (var service in services)
		{
			var serviceUrl = ResolveServiceUrl(service);
			var methods = service.GetMethods(BindingFlags.Public | BindingFlags.Instance);

			foreach (var method in methods)
			{
				if (method.GetCustomAttribute<ServiceOperationAttribute>() is not ServiceOperationAttribute attribute || attribute.Verbs == ServiceOperationVerbs.None)
					continue;

				InitializeServiceMethod(serviceUrl, service, method, attribute.Verbs);
			}
		}
	}

	private void InitializeServiceMethod(string serviceUrl, Type serviceType, MethodInfo method, ServiceOperationVerbs verbs)
	{
		var parameterTypes = new List<Type>();

		foreach (var parameter in method.GetParameters())
			parameterTypes.Add(parameter.ParameterType);

		var targetMethod = serviceType.GetMethod(method.Name, [.. parameterTypes]) ?? throw new NullReferenceException($"Method not found ({method.Name})");
		var methodUrl = $"{serviceUrl}/{ResolveMethodUrl(targetMethod)}";
		var descriptor = new InvokeDescriptor { Service = serviceType, Method = targetMethod, Parameters = [.. parameterTypes], Verbs = verbs };

		if (!methodUrl.StartsWith('/'))
			methodUrl = $"/{methodUrl}";

		if (Methods.TryGetValue(methodUrl, out List<InvokeDescriptor>? items))
			items.Add(descriptor);
		else
			Methods.Add(methodUrl, [descriptor]);
	}

	public async Task<ImmutableList<Tuple<string, ServiceOperationVerbs>>> QueryRoutes()
	{
		await Initialize();

		var result = new List<Tuple<string, ServiceOperationVerbs>>();

		foreach (var method in Methods)
		{
			var verbs = ServiceOperationVerbs.None;

			foreach (var descriptor in method.Value)
				verbs |= descriptor.Verbs;

			result.Add(Tuple.Create(method.Key, verbs));
		}

		return [.. result];
	}

	private static string ResolveServiceUrl(Type type)
	{
		if (type.GetCustomAttribute<ServiceUrlAttribute>() is ServiceUrlAttribute attribute)
			return attribute.Url;

		return $"{PascalNamespace(type.Namespace)}/{type.Name.ToPascalCase()}".Replace('.', '/');
	}

	private static string ResolveMethodUrl(MethodInfo method)
	{
		if (method.GetCustomAttribute<ServiceUrlAttribute>() is ServiceUrlAttribute attribute)
			return attribute.Url;

		return method.Name.ToCamelCase();
	}

	private static string DtoName(Type dto)
	{
		return $"{dto.Namespace}.{dto.Name}, {dto.Assembly.FullName}";
	}

	private static string? PascalNamespace(string? @namespace)
	{
		if (string.IsNullOrEmpty(@namespace))
			return null;

		var tokens = @namespace.Split('.');
		var result = new StringBuilder();

		foreach (var token in tokens)
			result.Append($"{token.ToPascalCase()}.");

		return result.ToString().TrimEnd('.');
	}
}
