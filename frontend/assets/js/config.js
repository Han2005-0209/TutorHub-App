/**
 * Cấu hình môi trường phía trình duyệt cho TutorHub Frontend (Site tĩnh Vercel).
 * Lưu ý: KHÔNG BAO GIỜ đặt khóa bí mật (Secret) vào tệp này.
 * Có thể ghi đè trước khi nạp module qua `window.__TUTORHUB_CONFIG__`.
 */
import { RUNTIME_API_BASE_URL } from './runtime-config.js';

const cauHinhGhiDe = typeof window !== 'undefined' && window.__TUTORHUB_CONFIG__
  ? window.__TUTORHUB_CONFIG__
  : {};

const laLocalhost =
  typeof window !== 'undefined' &&
  (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1');

export const API_BASE_URL = (
  cauHinhGhiDe.API_BASE_URL ||
  (!laLocalhost && RUNTIME_API_BASE_URL ? RUNTIME_API_BASE_URL : 'http://localhost:5000/api')
).replace(/\/+$/, '');

export const WS_BASE_URL = (
  cauHinhGhiDe.WS_BASE_URL ||
  API_BASE_URL.replace(/\/api$/i, '').replace(/^http/i, 'ws')
).replace(/\/+$/, '');

export const WEBRTC_ICE_SERVERS = cauHinhGhiDe.WEBRTC_ICE_SERVERS || [
  { urls: 'stun:stun.l.google.com:19302' },
  { urls: 'stun:stun1.l.google.com:19302' },
];

export const JITSI_BASE_URL = cauHinhGhiDe.JITSI_BASE_URL || 'https://meet.jit.si';

export const MAP_TILE_URL =
  cauHinhGhiDe.MAP_TILE_URL || 'https://tile.openstreetmap.org/{z}/{x}/{y}.png';

export const DEFAULT_MAP_CENTER = cauHinhGhiDe.DEFAULT_MAP_CENTER || [10.0341, 105.7881];
