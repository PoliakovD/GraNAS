export interface FolderDeviceResponse {
  folderId: string;
  deviceId: string;
  deviceName: string;
  platform: 'Windows' | 'Linux' | 'MacOS' | 'Web';
  isOnline: boolean;
  claimedAt: string;
}

export interface DeviceResponse {
  deviceId: string;
  deviceName: string;
  platform: 'Windows' | 'Linux' | 'MacOS' | 'Web';
  createdAt: string;
  lastSeenAt: string;
  isOnline: boolean;
}

export interface ActiveSessionResponse {
  deviceId: string;
  deviceName: string;
  platform: 'Windows' | 'Linux' | 'MacOS' | 'Web';
  ip: string;
  connectedAt: string;
}

export interface DeviceFolderBinding {
  folderId: string;
  claimedAt: string;
}
