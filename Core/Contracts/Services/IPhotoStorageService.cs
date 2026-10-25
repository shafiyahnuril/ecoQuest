// Dibuat dari docs/architect/Interface-Contracts.md (kontrak v1.2). Jangan ubah tanpa review Architect.
using EcoQuest.Core.Common;
using EcoQuest.Core.Contracts.Dtos;
using EcoQuest.Core.Domain;

namespace EcoQuest.Core.Contracts.Services;

public interface IPhotoStorageService
{
    Result ValidatePhoto(string sourcePath);                         // ekstensi .jpg/.jpeg/.png, ≤ 5 MB
    string? ResolveAbsolutePath(string? relativePath);               // null bila file tidak ada di perangkat ini
}
