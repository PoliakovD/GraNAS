using System;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Auth.Models;

namespace GraNAS.Auth.Services.Interfaces;

/// <summary>Управление аватарами пользователей (хранение в BYTEA).</summary>
public interface IAvatarService
{
    /// <summary>Сохраняет аватар пользователя. Если <paramref name="bytes"/> равен null — удаляет аватар.</summary>
    Task SetAsync(Guid userId, byte[]? bytes, string? contentType, CancellationToken ct);

    /// <summary>
    /// Возвращает аватар пользователя или <c>null</c>, если аватар не установлен.
    /// Включает <see cref="AvatarData.ContentType"/> и <see cref="AvatarData.UpdatedAt"/> для кэш-заголовков.
    /// </summary>
    Task<AvatarData?> GetAsync(Guid userId, CancellationToken ct);
}

/// <summary>Бинарные данные аватара с метаданными для HTTP-ответа.</summary>
public record AvatarData(byte[] Bytes, string ContentType, DateTime UpdatedAt);
