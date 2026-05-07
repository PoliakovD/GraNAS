import { http, HttpResponse } from 'msw';

const BASE = 'http://localhost:8080';

export const handlers = [
  // auth
  http.post(`${BASE}/api/auth/register`, () =>
    HttpResponse.json({ userId: 'user-1', message: 'ok' })),

  http.post(`${BASE}/api/auth/login`, () =>
    HttpResponse.json({
      access_token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ1c2VyLTEiLCJlbWFpbCI6InRlc3RAdGVzdC5jb20iLCJleHAiOjk5OTk5OTk5OTl9.signature',
      refresh_token: 'rt',
      expires_in: 900,
      token_type: 'bearer',
    })),

  http.post(`${BASE}/api/auth/refresh`, () =>
    HttpResponse.json({
      access_token: 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ1c2VyLTEiLCJlbWFpbCI6InRlc3RAdGVzdC5jb20iLCJleHAiOjk5OTk5OTk5OTl9.signature',
      refresh_token: 'rt2',
      expires_in: 900,
      token_type: 'bearer',
    })),

  http.post(`${BASE}/api/auth/logout`, () =>
    HttpResponse.json({ message: 'ok' })),

  // folders
  http.get(`${BASE}/api/metadata/folders`, () =>
    HttpResponse.json([
      { id: 'folder-1', name: 'Root', parentFolderId: null, ownerId: 'user-1', accessLevel: 'Full', path: null, createdAt: '2026-01-01T00:00:00Z', updatedAt: null },
      { id: 'folder-2', name: 'Sub', parentFolderId: 'folder-1', ownerId: 'user-1', accessLevel: 'Full', path: null, createdAt: '2026-01-01T00:00:00Z', updatedAt: null },
    ])),

  http.post(`${BASE}/api/metadata/folders`, () =>
    HttpResponse.json(
      { id: 'folder-3', name: 'New', parentFolderId: null, ownerId: 'user-1', accessLevel: 'Full', path: null, createdAt: '2026-01-01T00:00:00Z', updatedAt: null },
      { status: 201 },
    )),

  http.delete(`${BASE}/api/metadata/folders/:id`, () =>
    new HttpResponse(null, { status: 204 })),

  // permissions
  http.post(`${BASE}/api/metadata/folders/:folderId/permissions`, () =>
    HttpResponse.json(
      { userId: 'user-2', accessLevel: 'View', path: null, createdAt: '2026-01-01T00:00:00Z' },
      { status: 201 },
    )),

  http.delete(`${BASE}/api/metadata/folders/:folderId/permissions/:userId`, () =>
    new HttpResponse(null, { status: 204 })),

  // shares
  http.post(`${BASE}/api/sharing/folders/:folderId/share`, () =>
    HttpResponse.json(
      { id: 'link-1', folderId: 'folder-1', token: 'tok123', path: null, expiresAt: '2027-01-01T00:00:00Z', createdAt: '2026-01-01T00:00:00Z' },
      { status: 201 },
    )),

  http.get(`${BASE}/api/sharing/folders/:folderId/shares`, () =>
    HttpResponse.json([
      { id: 'link-1', folderId: 'folder-1', path: null, shareUrl: 'http://localhost:8080/s/tok123', expiresAt: '2027-01-01T00:00:00Z', revoked: false, createdAt: '2026-01-01T00:00:00Z' },
    ])),

  http.delete(`${BASE}/api/sharing/share-links/:id`, () =>
    new HttpResponse(null, { status: 204 })),

  // global shares listing (backend plan: docs/sharing-service-global-listing.md)
  http.get(`${BASE}/api/share-links`, () =>
    HttpResponse.json([
      { id: 'link-1', folderId: 'folder-1', folderName: 'Root', path: null, shareUrl: 'http://localhost:8080/s/tok123', expiresAt: '2027-01-01T00:00:00Z', revoked: false, createdAt: '2026-01-01T00:00:00Z', openCount: 0 },
    ])),

  // signaling (stub — WebSocket not supported in jsdom tests)
  http.post(`${BASE}/hubs/signaling/negotiate`, () =>
    new HttpResponse(null, { status: 400 })),
  http.get(`${BASE}/api/signaling/turn/credentials`, () =>
    HttpResponse.json({ username: 'test', credential: 'test', uris: [], ttl: 600 })),

  // public share
  http.get(`${BASE}/api/sharing/share/:token`, ({ params }) => {
    if (params.token === 'revoked') return new HttpResponse(null, { status: 410 });
    if (params.token === 'notfound') return new HttpResponse(null, { status: 404 });
    return HttpResponse.json({
      folderId: 'folder-1',
      folderName: 'Root',
      ownerId: 'user-1',
      path: null,
      expiresAt: '2027-01-01T00:00:00Z',
    });
  }),
];
