import { API_BASE_URL } from './config.js';

const KHOA_ACCESS_TOKEN = 'tutorhub_access_token';
const KHOA_REFRESH_TOKEN = 'tutorhub_refresh_token';

let tokenTrongBoNho = null;

export function layAccessToken() {
  if (tokenTrongBoNho) {
    return tokenTrongBoNho;
  }
  try {
    tokenTrongBoNho =
      window.localStorage.getItem(KHOA_ACCESS_TOKEN) ||
      window.sessionStorage.getItem(KHOA_ACCESS_TOKEN);
    return tokenTrongBoNho;
  } catch {
    return null;
  }
}

export function luuPhienToken({ accessToken, refreshToken = null }) {
  tokenTrongBoNho = accessToken || null;
  try {
    const dsKho = [window.localStorage, window.sessionStorage];
    for (const kho of dsKho) {
      if (accessToken) {
        kho.setItem(KHOA_ACCESS_TOKEN, accessToken);
      } else {
        kho.removeItem(KHOA_ACCESS_TOKEN);
      }
      if (refreshToken) {
        kho.setItem(KHOA_REFRESH_TOKEN, refreshToken);
      } else if (refreshToken === '') {
        kho.removeItem(KHOA_REFRESH_TOKEN);
      }
    }
  } catch {
    // Bỏ qua khi trình duyệt chặn lưu trữ cục bộ
  }
}

export function xoaPhienToken() {
  tokenTrongBoNho = null;
  try {
    window.localStorage.removeItem(KHOA_ACCESS_TOKEN);
    window.localStorage.removeItem(KHOA_REFRESH_TOKEN);
    window.sessionStorage.removeItem(KHOA_ACCESS_TOKEN);
    window.sessionStorage.removeItem(KHOA_REFRESH_TOKEN);
  } catch {
    // Bỏ qua
  }
}

/**
 * Lỗi chuẩn từ TutorHub API ({ loi: { ma, thongBao, chiTiet, maYeuCau } }).
 */
export class LoiApi extends Error {
  constructor({ ma, thongBao, maHttp, chiTiet = [], maYeuCau = '' }) {
    super(thongBao || 'Có lỗi xảy ra, vui lòng thử lại.');
    this.name = 'LoiApi';
    this.ma = ma || 'LOI_HE_THONG';
    this.maHttp = maHttp || 500;
    this.chiTiet = chiTiet;
    this.maYeuCau = maYeuCau;
  }
}

let soRequestDangChoThucDay = 0;

function capNhatThanhKhoiDongMayChu(hien) {
  if (typeof document === 'undefined' || !document.body) return;
  const id = 'tutorhub-wakeup-banner';
  let el = document.getElementById(id);
  if (hien) {
    if (!el) {
      el = document.createElement('div');
      el.id = id;
      el.style.cssText =
        'position:fixed;top:14px;left:50%;transform:translateX(-50%);z-index:99999;background:#0f172a;color:#f8fafc;border:1px solid #334155;padding:10px 18px;border-radius:9999px;font-size:13px;font-weight:700;box-shadow:0 12px 32px rgba(15,23,42,.35);display:flex;align-items:center;gap:8px;font-family:system-ui,sans-serif;';
      el.textContent = 'Đang khởi động máy chủ TutorHub, vui lòng chờ giây lát...';
      document.body.appendChild(el);
    }
    el.style.display = 'flex';
  } else if (el) {
    el.style.display = 'none';
  }
}

/**
 * Wrapper fetch tập trung:
 * - Tự động ghép API_BASE_URL
 * - Tự động gắn Header `Authorization: Bearer <token>` (KHÔNG dùng cookie)
 * - Tự động đánh thức & thử lại mượt mà khi Backend Render Free vừa thức dậy (cold start)
 * - Tự động giải mã JSON camelCase và chuẩn hóa lỗi / 401
 */
export async function goiApi(duongDan, tuyChon = {}) {
  const url = duongDan.startsWith('http')
    ? duongDan
    : `${API_BASE_URL}/${duongDan.replace(/^\/+/, '')}`;

  const headers = new Headers(tuyChon.headers || {});
  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json');
  }

  const token = layAccessToken();
  if (token && !headers.has('Authorization')) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  let body = tuyChon.body;
  const laFormData = typeof FormData !== 'undefined' && body instanceof FormData;
  if (body && typeof body === 'object' && !laFormData && !(body instanceof Blob)) {
    if (!headers.has('Content-Type')) {
      headers.set('Content-Type', 'application/json');
    }
    body = JSON.stringify(body);
  }

  // Khi chạy trên host miễn phí (Render Free), máy chủ có thể cần 20-45s để tự thức dậy ở lần mở đầu tiên
  const laLocal =
    typeof window !== 'undefined' &&
    (window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1');
  const method = String(tuyChon.method || 'GET').toUpperCase();
  const coTheThuLai = method === 'GET' || method === 'HEAD' || duongDan.includes('/xac-thuc/');
  const soLanToiDa = coTheThuLai ? (laLocal ? 2 : 12) : 1;
  let phanHoi = null;
  let loiMangCuoi = null;
  let daBatBanner = false;

  try {
    for (let lan = 1; lan <= soLanToiDa; lan++) {
      try {
        phanHoi = await fetch(url, {
          ...tuyChon,
          headers,
          body,
        });
        if (lan < soLanToiDa && (phanHoi.status === 502 || phanHoi.status === 503 || phanHoi.status === 504)) {
          if (!laLocal && !daBatBanner) {
            daBatBanner = true;
            soRequestDangChoThucDay += 1;
            capNhatThanhKhoiDongMayChu(true);
          }
          await new Promise((r) => setTimeout(r, Math.min(5000, lan * 2000)));
          continue;
        }
        loiMangCuoi = null;
        break;
      } catch (err) {
        loiMangCuoi = err;
        if (lan < soLanToiDa) {
          if (!laLocal && !daBatBanner) {
            daBatBanner = true;
            soRequestDangChoThucDay += 1;
            capNhatThanhKhoiDongMayChu(true);
          }
          await new Promise((r) => setTimeout(r, Math.min(5000, lan * 2000)));
          continue;
        }
      }
    }
  } finally {
    if (daBatBanner) {
      soRequestDangChoThucDay = Math.max(0, soRequestDangChoThucDay - 1);
      if (soRequestDangChoThucDay === 0) {
        capNhatThanhKhoiDongMayChu(false);
      }
    }
  }

  if (loiMangCuoi || !phanHoi) {
    throw new LoiApi({
      ma: 'LOI_KET_NOI_MAY_CHU',
      thongBao: 'Không thể kết nối máy chủ API. Hệ thống có thể đang khởi động lại, vui lòng thử lại sau vài giây.',
      maHttp: 503,
    });
  }

  if (phanHoi.status === 401) {
    xoaPhienToken();
    window.dispatchEvent(new CustomEvent('tutorhub:het-phien-dang-nhap', { detail: { url } }));
  }

  const loaiNoiDung = phanHoi.headers.get('content-type') || '';
  const duLieuJson = loaiNoiDung.includes('application/json')
    ? await phanHoi.json().catch(() => null)
    : null;

  if (!phanHoi.ok) {
    const thongTinLoi = duLieuJson?.loi || {};
    throw new LoiApi({
      ma: thongTinLoi.ma || (phanHoi.status === 401 ? 'CHUA_DANG_NHAP' : 'LOI_HE_THONG'),
      thongBao: thongTinLoi.thongBao || `Yêu cầu thất bại (HTTP ${phanHoi.status}).`,
      maHttp: phanHoi.status,
      chiTiet: thongTinLoi.chiTiet || [],
      maYeuCau: thongTinLoi.maYeuCau || phanHoi.headers.get('x-request-id') || '',
    });
  }

  return duLieuJson;
}

export const api = {
  get: (duongDan, tuyChon = {}) => goiApi(duongDan, { ...tuyChon, method: 'GET' }),
  post: (duongDan, body, tuyChon = {}) => goiApi(duongDan, { ...tuyChon, method: 'POST', body }),
  put: (duongDan, body, tuyChon = {}) => goiApi(duongDan, { ...tuyChon, method: 'PUT', body }),
  patch: (duongDan, body, tuyChon = {}) => goiApi(duongDan, { ...tuyChon, method: 'PATCH', body }),
  delete: (duongDan, tuyChon = {}) => goiApi(duongDan, { ...tuyChon, method: 'DELETE' }),
};
