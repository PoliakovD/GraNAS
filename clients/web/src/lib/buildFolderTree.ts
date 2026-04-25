import type { DataNode } from 'antd/es/tree';
import type { FolderResponse } from '../types/folder';

export interface FolderNode extends DataNode {
  folderId: string;
  children: FolderNode[];
}

export function buildFolderTree(folders: FolderResponse[], ownerId: string): FolderNode[] {
  const owned = folders.filter(f => f.ownerId === ownerId);
  const map = new Map<string, FolderNode>(
    owned.map(f => [f.id, { key: f.id, folderId: f.id, title: f.name, children: [] }])
  );
  const roots: FolderNode[] = [];
  for (const f of owned) {
    const node = map.get(f.id)!;
    if (f.parentFolderId && map.has(f.parentFolderId)) {
      map.get(f.parentFolderId)!.children.push(node);
    } else {
      roots.push(node);
    }
  }
  return roots;
}
