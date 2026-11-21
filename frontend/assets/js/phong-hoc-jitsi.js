/**
 * Tiện ích kiểm tra URL phòng học trực tuyến Jitsi Meet.
 * Chuyển đổi từ legacy/frontend/src/utils/phong_hoc_jitsi.ts (Phân loại: CHỈNH ở mục 07).
 */

export function kiemTraUrlPhongHocHopLe(url) {
  const urlSach = typeof url === 'string' ? url.trim() : '';
  if (!urlSach) {
    return null;
  }
  try {
    const parsed = new URL(urlSach);
    if (parsed.protocol === 'https:' || parsed.protocol === 'http:') {
      return parsed.toString();
    }
    return null;
  } catch {
    return null;
  }
}

export function laMayChuJitsiCongCong(url) {
  const urlHopLe = kiemTraUrlPhongHocHopLe(url);
  if (!urlHopLe) {
    return false;
  }
  try {
    return new URL(urlHopLe).hostname.toLowerCase() === 'meet.jit.si';
  } catch {
    return false;
  }
}
