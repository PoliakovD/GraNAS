export const MsgType = {
  EcdhOffer: 'ecdh_offer',
  EcdhAnswer: 'ecdh_answer',
  ListRequest: 'list_request',
  ListResponse: 'list_response',
  FileRequest: 'file_request',
  FileHeader: 'file_header',
  FileComplete: 'file_complete',
  Error: 'error',
} as const;

export interface EcdhOfferMsg { type: 'ecdh_offer'; publicKey: string }
export interface EcdhAnswerMsg { type: 'ecdh_answer'; publicKey: string }
export interface ListRequestMsg { type: 'list_request' }
export interface ListResponseMsg { type: 'list_response'; files: RemoteFileEntry[] }
export interface FileRequestMsg { type: 'file_request'; path: string }
export interface FileHeaderMsg {
  type: 'file_header';
  path: string;
  size: number;
  sha256: string;
  iv: string;
}
export interface FileCompleteMsg { type: 'file_complete'; path: string }
export interface ErrorMsg { type: 'error'; code: string; message: string }

export interface RemoteFileEntry {
  path: string;
  size: number;
  modifiedAt: string;
}

export type ProtocolMsg =
  | EcdhOfferMsg | EcdhAnswerMsg
  | ListRequestMsg | ListResponseMsg
  | FileRequestMsg | FileHeaderMsg | FileCompleteMsg
  | ErrorMsg;

export function parseMsg(json: string): ProtocolMsg | null {
  try { return JSON.parse(json) as ProtocolMsg; }
  catch { return null; }
}
