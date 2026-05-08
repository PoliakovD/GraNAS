import type { CreateFolderRequest, FolderResponse } from '../types/folder';
import { api } from './client';

export const foldersApi = {
  list: () =>
    api.get<FolderResponse[]>('/api/metadata/folders'),

  create: (data: CreateFolderRequest) =>
    api.post<FolderResponse>('/api/metadata/folders', data),

  delete: (id: string) =>
    api.delete(`/api/metadata/folders/${id}`),

  touch: (id: string) =>
    api.patch(`/api/metadata/folders/${id}/touch`),
};
