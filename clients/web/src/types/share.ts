export interface ShareLinkResponse {
  id: string;
  folderId: string;
  path: string | null;
  expiresAt: string | null;
  revoked: boolean;
  createdAt: string;
}

export interface CreateShareRequest {
  expiresAt: string;
  path?: string | null;
}

export interface CreateShareResponse {
  id: string;
  folderId: string;
  token: string;
  path: string | null;
  expiresAt: string | null;
  createdAt: string;
}

export interface ShareDetailsResponse {
  folderId: string;
  folderName: string;
  ownerId: string;
  path: string | null;
  expiresAt: string | null;
}

// TODO: returned by GET /api/share-links (cross-folder listing, backend not yet implemented)
export interface ShareLinkOwnerResponse extends ShareLinkResponse {
  folderName: string;
  openCount?: number;
}
