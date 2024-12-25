﻿using Connected.Entities;

namespace Connected.Membership.Claims;

public enum ClaimStatus
{
	Pending = 1,
	Approved = 2,
	Denied = 3
}

public interface IClaim : IEntity<long>
{
	string Claim { get; init; }
	string? Schema { get; init; }
	string? Identity { get; init; }
	string? Type { get; init; }
	string? PrimaryKey { get; init; }
	ClaimStatus Status { get; init; }
}