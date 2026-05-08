export interface FolderDeviceResponse {
  folderId: string;
  deviceId: string;
  deviceName: string;
  platform: 'Windows' | 'Linux' | 'MacOS' | 'Web';
  isOnline: boolean;
  claimedAt: string;
}
