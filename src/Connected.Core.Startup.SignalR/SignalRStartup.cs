﻿using Connected.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Connected.Startup.SignalR;

[Priority(52)]
public sealed class SignalRStartup : Runtime.Startup
{
	public static IServiceProvider Services { get; private set; } = default!;
	protected override void OnConfigureServices(IServiceCollection services)
	{
		services.AddSignalR(o =>
		{
			o.EnableDetailedErrors = true;
			o.DisableImplicitFromServicesParameters = false;
		});
	}

	protected override void OnConfigure(IApplicationBuilder app, IWebHostEnvironment env)
	{
	}
}