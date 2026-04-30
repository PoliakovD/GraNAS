const NONCE_SIZE = 12;
const TAG_SIZE = 16;

export async function generateEcdhKeyPair(): Promise<CryptoKeyPair> {
  return crypto.subtle.generateKey(
    { name: 'ECDH', namedCurve: 'P-256' },
    false,
    ['deriveBits'],
  );
}

export async function exportPublicKeyBase64(publicKey: CryptoKey): Promise<string> {
  const spki = await crypto.subtle.exportKey('spki', publicKey);
  return btoa(String.fromCharCode(...new Uint8Array(spki)));
}

export async function importPublicKey(base64: string): Promise<CryptoKey> {
  const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
  return crypto.subtle.importKey('spki', bytes, { name: 'ECDH', namedCurve: 'P-256' }, false, []);
}

export async function deriveAesKey(privateKey: CryptoKey, peerPublicKey: CryptoKey): Promise<CryptoKey> {
  const sharedBits = await crypto.subtle.deriveBits(
    { name: 'ECDH', public: peerPublicKey },
    privateKey,
    256,
  );

  // HKDF: derive 256-bit AES-GCM key
  const ikm = await crypto.subtle.importKey('raw', sharedBits, 'HKDF', false, ['deriveKey']);
  return crypto.subtle.deriveKey(
    { name: 'HKDF', hash: 'SHA-256', salt: new Uint8Array(0), info: new Uint8Array(0) },
    ikm,
    { name: 'AES-GCM', length: 256 },
    false,
    ['decrypt'],
  );
}

// Decrypt a chunk packed as nonce(12) + ciphertext + tag(16)
export async function decryptChunk(aesKey: CryptoKey, packed: Uint8Array): Promise<Uint8Array> {
  const nonce = packed.slice(0, NONCE_SIZE);
  // SubtleCrypto AES-GCM expects ciphertext+tag concatenated (tag is the last 16 bytes)
  const ciphertextWithTag = packed.slice(NONCE_SIZE);
  const plaintext = await crypto.subtle.decrypt(
    { name: 'AES-GCM', iv: nonce, tagLength: TAG_SIZE * 8 },
    aesKey,
    ciphertextWithTag,
  );
  return new Uint8Array(plaintext);
}

export async function sha256Hex(data: Uint8Array): Promise<string> {
  // Create a fresh ArrayBuffer copy so TypeScript knows it's not SharedArrayBuffer
  const fresh = new Uint8Array(data).buffer;
  const hash = await crypto.subtle.digest('SHA-256', fresh);
  return Array.from(new Uint8Array(hash))
    .map(b => b.toString(16).padStart(2, '0'))
    .join('');
}
