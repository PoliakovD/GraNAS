export type AccessLevel = 'View' | 'Full';

export interface FolderResponse {
  id: string;
  name: string;
  parentFolderId: string | null;
  ownerId: string;
  accessLevel: AccessLevel;
  path: string | null;
  ownerEmail: string | null;
  createdAt: string;
  updatedAt: string | null;
  lastAccessedAt: string | null;
}

export interface CreateFolderRequest {
  name: string;
  parentFolderId?: string | null;
}
