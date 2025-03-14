﻿using Connected.Annotations;

namespace Connected.Caching;
/// <summary>
/// Represents the service providing caching capabillities.
/// <summary>
[Service]
public interface ICachingService : ICache
{
	/// <summary>
	/// Merges the entries from the passed cache into the current cache.
	/// </summary>
	/// <remarks>
	/// This method is called from the context cache once the commit is performed.
	/// </remarks>
	void Merge(ICache cache);

	ICacheContext CreateContext();
}