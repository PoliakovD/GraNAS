import type { CreateShareRequest, CreateShareResponse, ShareDetailsResponse, ShareLinkOwnerResponse, ShareLinkResponse } from '../types/share';
import { api } from './client';

export const sharesApi = {
  create: (folderId: string, data: CreateShareRequest) =>
    api.post<CreateShareResponse>(`/api/sharing/folders/${folderId}/share`, data),

  list: (folderId: string) =>
    api.get<ShareLinkResponse[]>(`/api/sharing/folders/${folderId}/shares`),

  revokeById: (id: string) =>
    api.delete(`/api/sharing/share-links/${id}`),

  revokeByToken: (token: string) =>
    api.delete(`/api/sharing/share/${token}`),

  getByToken: (token: string) =>
    api.get<ShareDetailsResponse>(`/api/sharing/share/${token}`),

  listAll: () =>
    api.get<ShareLinkOwnerResponse[]>('/api/sharing/share-links'),
};
