using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraNAS.Metadata.Models.DTO;

namespace GraNAS.Metadata.Services.Interfaces;

public enum DeleteFolderError
{
  None,
  NotFound,
  Forbidden
}

public record DeleteFolderResult(DeleteFolderError Error);

public interface IFolderService
{
  Task<IEnumerable<FolderResponse>> GetUserFoldersAsync(Guid userId);
  Task<FolderResponse> CreateFolderAsync(Guid userId, CreateFolderRequest request);
  Task<DeleteFolderResult> DeleteFolderAsync(Guid userId, Guid folderId);
}
