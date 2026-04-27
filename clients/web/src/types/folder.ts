export type AccessLevel = 'View' | 'Full';

export interface FolderResponse {
  id: string;
  name: string;
  parentFolderId: string | null;
  ownerId: string;
  accessLevel: AccessLevel;
  path: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface CreateFolderRequest {
  name: string;
  parentFolderId?: string | null;
}
