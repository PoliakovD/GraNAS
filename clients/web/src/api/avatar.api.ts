import { api } from './client';

export const avatarApi = {
  upload: (file: File) => {
    const fd = new FormData();
    fd.append('file', file);
    return api.post('/api/auth/me/avatar', fd);
  },

  remove: () => api.delete('/api/auth/me/avatar'),
};
