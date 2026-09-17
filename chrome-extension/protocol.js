export const BROWSER_PORT = 17893;
export const AUDIO_MAGIC = 0xa1;
export const VIDEO_HEADER = 14;
export const AUDIO_HEADER = 16;
export const JPEG = 3;

export function envelope(type, payload = null) {
  return {
    version: 1,
    type,
    requestId: crypto.randomUUID(),
    timestamp: Date.now(),
    payload,
    error: null
  };
}

export function isAudio(bytes) {
  return bytes.length >= AUDIO_HEADER && bytes[0] === AUDIO_MAGIC;
}

function readLong(bytes, offset) {
  let value = 0;
  for (let i = 0; i < 8; i += 1) {
    value = value * 256 + bytes[offset + i];
  }
  return value;
}

function readShort(bytes, offset) {
  return (bytes[offset] << 8) | bytes[offset + 1];
}

function readInt(bytes, offset) {
  return (
    ((bytes[offset] << 24) |
      (bytes[offset + 1] << 16) |
      (bytes[offset + 2] << 8) |
      bytes[offset + 3]) >>>
    0
  );
}

export function decodeVideo(bytes) {
  if (bytes.length < VIDEO_HEADER) return null;
  return {
    flags: bytes[0],
    timestamp: readLong(bytes, 1),
    width: readShort(bytes, 9),
    height: readShort(bytes, 11),
    codec: bytes[13],
    payload: bytes.subarray(VIDEO_HEADER)
  };
}

export function decodeAudio(bytes) {
  if (bytes.length < AUDIO_HEADER) return null;
  return {
    timestamp: readLong(bytes, 1),
    sampleRate: readInt(bytes, 9),
    channels: bytes[13],
    bitsPerSample: bytes[14],
    payload: bytes.subarray(AUDIO_HEADER)
  };
}
