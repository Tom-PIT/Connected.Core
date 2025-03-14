﻿using System.Collections;
using System.Collections.Immutable;

namespace Connected.Caching;

public abstract class CacheContainer<TEntry, TKey> : ICacheContainer<TEntry, TKey> where TEntry : class
{
	protected CacheContainer(ICachingService cachingService, string key)
	{
		if (cachingService is null)
			ArgumentNullException.ThrowIfNull(cachingService);

		CachingService = cachingService;
		Key = key;

		Context = cachingService.CreateContext();
	}

	private ICacheContext Context { get; }
	protected bool IsDisposed { get; set; }
	protected ICachingService CachingService { get; }
	protected virtual ICollection<string>? Keys => CachingService.Ids(Key);

	public int Count => CachingService.Count(Key);
	public string Key { get; }

	public async Task Remove(TKey id)
	{
		if (id is null)
			throw new ArgumentNullException(nameof(id));

		await CachingService.Remove(Key, id);
	}

	public async Task Remove(Func<TEntry, bool> predicate)
	{
		await CachingService.Remove(Key, predicate);
	}

	public virtual Task<ImmutableList<TEntry>?> All()
	{
		return Task.FromResult(CachingService.All<TEntry>(Key));
	}

	public virtual async Task<TEntry?> Get(TKey id, Func<IEntryOptions, Task<TEntry?>>? retrieve)
	{
		if (id is null)
			throw new ArgumentNullException(nameof(id));

		return await CachingService.Get(Key, id, retrieve);
	}

	public virtual Task<TEntry?> Get(TKey id)
	{
		if (id is null)
			throw new ArgumentNullException(nameof(id));

		return Task.FromResult(CachingService.Get<TEntry>(Key, id));
	}

	public virtual Task<TEntry?> First()
	{
		return Task.FromResult(CachingService.First<TEntry>(Key));
	}

	public virtual async Task<TEntry?> Get(Func<TEntry, bool> predicate, Func<IEntryOptions, Task<TEntry?>>? retrieve)
	{
		return await CachingService.Get(Key, predicate, retrieve);
	}

	public virtual async Task<TEntry?> Get(Func<TEntry, bool> predicate)
	{
		return await CachingService.Get(Key, predicate, null);
	}

	public virtual void Set(TKey id, TEntry instance)
	{
		if (id is null)
			throw new ArgumentNullException(nameof(id));

		CachingService.Set(Key, id, instance);
	}

	public virtual void Set(TKey id, TEntry instance, TimeSpan duration)
	{
		if (id is null)
			throw new ArgumentNullException(nameof(id));

		CachingService.Set(Key, id, instance, duration);
	}

	public virtual void Set(TKey id, TEntry instance, TimeSpan duration, bool slidingExpiration)
	{
		if (id is null)
			throw new ArgumentNullException(nameof(id));

		CachingService.Set(Key, id, instance, duration, slidingExpiration);
	}

	private void Dispose(bool disposing)
	{
		if (!IsDisposed)
		{
			if (disposing)
				OnDisposing();

			IsDisposed = true;
		}
	}

	protected virtual void OnDisposing()
	{

	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	public virtual IEnumerator<TEntry> GetEnumerator()
	{
		return (CachingService?.GetEnumerator<TEntry>(Key)) ?? throw new NullReferenceException("Cannot retrieve cache enumerator.");
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
