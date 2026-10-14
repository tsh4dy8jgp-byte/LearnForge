using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LearnForge.Api;

// Marks stored times as UTC when read; local times are normalized before they are written.
public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v,
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
