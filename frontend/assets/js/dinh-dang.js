/**
 * Tiện ích định dạng tiền tệ VND và thời gian GMT+7 (Asia/Ho_Chi_Minh).
 * Chuyển đổi và chuẩn hóa camelCase từ legacy/frontend/src/utils/dinh_dang.ts (Phân loại: CHỈNH ở mục 07).
 */

export function dinhDangTienVnd(soTien) {
  const soNguyen = Math.round(Number(soTien) || 0);
  return `${soNguyen.toLocaleString('vi-VN')} ₫`;
}

function chuanHoaDateUtc(giaTri) {
  if (giaTri instanceof Date) return giaTri;
  if (typeof giaTri !== 'string') return null;
  const s = giaTri.trim();
  if (!s) return null;
  // SQL Server datetime2 trả về UTC không kèm hậu tố Z -> tự gắn Z để hiển thị chuẩn GMT+7
  const coMuiGio = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(s);
  return new Date(coMuiGio ? s : `${s}Z`);
}

export function dinhDangNgayGioVn(giaTri) {
  const ngay = chuanHoaDateUtc(giaTri);
  if (!(ngay instanceof Date) || Number.isNaN(ngay.getTime())) {
    return '';
  }
  return new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    hourCycle: 'h23',
  }).format(ngay);
}

export function layNgayVn(giaTri) {
  const ngay = chuanHoaDateUtc(giaTri);
  if (!(ngay instanceof Date) || Number.isNaN(ngay.getTime())) {
    return '';
  }
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(ngay);
}

export function dinhDangNgayVn(giaTri) {
  const ngay = chuanHoaDateUtc(giaTri);
  if (!(ngay instanceof Date) || Number.isNaN(ngay.getTime())) {
    return '';
  }
  return new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    weekday: 'short',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
  }).format(ngay);
}

export function dinhDangGioVn(giaTri) {
  const ngay = chuanHoaDateUtc(giaTri);
  if (!(ngay instanceof Date) || Number.isNaN(ngay.getTime())) {
    return '';
  }
  return new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
    hourCycle: 'h23',
  }).format(ngay);
}

export function dinhDangKhoangGioVn(batDau, ketThuc) {
  return `${dinhDangNgayVn(batDau)} · ${dinhDangGioVn(batDau)} – ${dinhDangGioVn(ketThuc)} (GMT+7)`;
}

export function tachKhungGioCon(dsKhung, thoiLuongPhut, mocToiThieuMs, mocToiDaMs) {
  if (thoiLuongPhut <= 0 || !Array.isArray(dsKhung)) {
    return [];
  }
  const buocMs = 30 * 60 * 1000;
  const thoiLuongMs = thoiLuongPhut * 60 * 1000;
  const daCo = new Set();
  const ketQua = [];

  for (const khung of dsKhung) {
    const bdKhung = new Date(khung.batDauLuc).getTime();
    const ktKhung = new Date(khung.ketThucLuc).getTime();
    if (Number.isNaN(bdKhung) || Number.isNaN(ktKhung)) {
      continue;
    }

    for (let t = bdKhung; t + thoiLuongMs <= ktKhung; t += buocMs) {
      if (t < mocToiThieuMs) {
        continue;
      }
      if (mocToiDaMs !== undefined && t > mocToiDaMs) {
        continue;
      }
      const bdNgay = new Date(t);
      const ktNgay = new Date(t + thoiLuongMs);
      const ngayBdVn = layNgayVn(bdNgay);
      const ngayKtVn = layNgayVn(ktNgay);
      if (!ngayBdVn || ngayBdVn !== ngayKtVn) {
        continue;
      }

      const isoBd = bdNgay.toISOString();
      if (daCo.has(isoBd)) {
        continue;
      }
      daCo.add(isoBd);

      ketQua.push({
        batDauLuc: isoBd,
        ketThucLuc: ktNgay.toISOString(),
        nhanGio: `${dinhDangGioVn(bdNgay)} – ${dinhDangGioVn(ktNgay)}`,
        ngayVn: ngayBdVn,
        nhanNgayVn: dinhDangNgayVn(bdNgay),
      });
    }
  }

  ketQua.sort((a, b) => new Date(a.batDauLuc).getTime() - new Date(b.batDauLuc).getTime());
  return ketQua;
}
