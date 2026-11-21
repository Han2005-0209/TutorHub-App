/**
 * Ánh xạ trạng thái và nhãn hiển thị tiếng Việt tự nhiên trên giao diện TutorHub.
 */

export function layNhanVaiTro(vaiTro) {
  const bang = {
    HOC_SINH: 'Học sinh',
    GIA_SU: 'Gia sư',
    QUAN_TRI: 'Quản trị viên',
  };
  return bang[vaiTro] || 'Thành viên';
}

export function layNhanHinhThucHoc(hinhThuc) {
  const bang = {
    TRUC_TUYEN: 'Học trực tuyến (Online)',
    TRUC_TIEP: 'Học trực tiếp tại nhà',
  };
  return bang[hinhThuc] || 'Chưa xác định';
}

export function layNhanLoaiGiayTo(loai) {
  const bang = {
    BANG_CAP: 'Bằng cấp chuyên môn',
    CHUNG_CHI: 'Chứng chỉ giảng dạy / ngoại ngữ',
    CAN_CUOC: 'Căn cước công dân',
    KHAC: 'Giấy tờ bổ sung',
  };
  return bang[loai] || 'Giấy tờ xác minh';
}

export function layNhanTrangThaiGiayTo(trangThai) {
  const bang = {
    CHO_DUYET: { nhan: 'Đang chờ duyệt', lopBadge: 'badge-warning' },
    DA_DUYET: { nhan: 'Đã duyệt', lopBadge: 'badge-success' },
    TU_CHOI: { nhan: 'Cần bổ sung lại', lopBadge: 'badge-danger' },
  };
  return bang[trangThai] || { nhan: 'Đang xử lý', lopBadge: 'badge-neutral' };
}

export function layNhanLoaiLichNgayCuThe(loai) {
  const bang = {
    NGHI_PHEP: { nhan: 'Báo bận / Tạm nghỉ', lopBadge: 'badge-danger' },
    BO_SUNG: { nhan: 'Mở ca dạy thêm', lopBadge: 'badge-success' },
    MO_THEM_GIO: { nhan: 'Mở ca dạy thêm', lopBadge: 'badge-success' },
  };
  return bang[loai] || { nhan: 'Lịch ngoại lệ', lopBadge: 'badge-neutral' };
}

export function layNhanTrangThaiDonDatLich(trangThai) {
  const bang = {
    CHO_XAC_NHAN: { nhan: 'Chờ xác nhận', lopBadge: 'badge-warning' },
    DA_XAC_NHAN: { nhan: 'Đã xác nhận', lopBadge: 'badge-info' },
    BI_TU_CHOI: { nhan: 'Gia sư từ chối', lopBadge: 'badge-danger' },
    HET_HAN: { nhan: 'Hết hạn phản hồi', lopBadge: 'badge-neutral' },
    DA_HUY: { nhan: 'Đã hủy', lopBadge: 'badge-neutral' },
    HOAN_THANH: { nhan: 'Đã hoàn thành', lopBadge: 'badge-success' },
    TRANH_CHAP: { nhan: 'Đang giải quyết khiếu nại', lopBadge: 'badge-danger' },
  };
  return bang[trangThai] || { nhan: 'Đang cập nhật', lopBadge: 'badge-neutral' };
}

export function layNhanTrangThaiThanhToan(trangThai) {
  const bang = {
    CHO_THANH_TOAN: { nhan: 'Chờ thanh toán', lopBadge: 'badge-warning' },
    DANG_GIU: { nhan: 'Đang tạm giữ an toàn', lopBadge: 'badge-success' },
    DA_GIAI_NGAN: { nhan: 'Đã giải ngân cho gia sư', lopBadge: 'badge-info' },
    DA_HOAN: { nhan: 'Đã hoàn tiền', lopBadge: 'badge-neutral' },
    THAT_BAI: { nhan: 'Thanh toán không thành công', lopBadge: 'badge-danger' },
  };
  return bang[trangThai] || { nhan: 'Đang cập nhật', lopBadge: 'badge-neutral' };
}

export function layNhanPhuongThucThanhToan(phuongThuc) {
  const bang = {
    VNPAY: 'Cổng thanh toán VNPay',
    VI_NOI_BO: 'Ví TutorHub',
    GIA_LAP: 'Thanh toán thử nghiệm',
  };
  return bang[phuongThuc] || 'Phương thức khác';
}

export function layNhanTrangThaiHoSoGiaSu(trangThaiHoSo) {
  const bang = {
    NHAP: { nhan: 'Bản nháp', lopBadge: 'badge-neutral' },
    CHO_DUYET: { nhan: 'Đang chờ duyệt', lopBadge: 'badge-warning' },
    DA_DUYET: { nhan: 'Đã duyệt công khai', lopBadge: 'badge-success' },
    TU_CHOI: { nhan: 'Cần bổ sung hồ sơ', lopBadge: 'badge-danger' },
  };
  return bang[trangThaiHoSo] || { nhan: 'Bản nháp', lopBadge: 'badge-neutral' };
}

export function layNhanDanhMucKhieuNai(danhMuc) {
  const bang = {
    GIA_SU_VANG_MAT: 'Gia sư vắng mặt',
    CHAT_LUONG: 'Chất lượng giảng dạy',
    THANH_TOAN: 'Thanh toán & học phí',
    KHAC: 'Vấn đề khác',
  };
  return bang[danhMuc] || 'Vấn đề khác';
}

export function layNhanKetQuaKhieuNai({ trangThai, tienDaHoan = 0, soTienThanhToan = 0 }) {
  if (trangThai === 'MO') return 'Đang chờ quản trị xử lý';
  if (trangThai === 'TU_CHOI') return 'Bác khiếu nại (giải ngân cho gia sư)';
  if (trangThai === 'DA_GIAI_QUYET') {
    if (soTienThanhToan > 0 && tienDaHoan >= soTienThanhToan) {
      return 'Hoàn tiền toàn bộ về ví học viên';
    }
    return 'Hoàn tiền một phần về ví học viên';
  }
  return 'Đang cập nhật';
}

export function layNhanLoaiSoCaiGiaSu(loai) {
  const bang = {
    THU_NHAP: { nhan: 'Thu nhập buổi dạy', lop: 'badge-success' },
    RUT_TIEN: { nhan: 'Rút tiền', lop: 'badge-neutral' },
  };
  return bang[loai] || { nhan: 'Giao dịch ví', lop: 'badge-neutral' };
}

export function layNhanTrangThaiRutTien(trangThai) {
  const bang = {
    CHO_XU_LY: { nhan: 'Đang xử lý', lop: 'badge-warning' },
    DA_CHI_TRA: { nhan: 'Đã chi trả', lop: 'badge-success' },
    TU_CHOI: { nhan: 'Từ chối', lop: 'badge-danger' },
  };
  return bang[trangThai] || { nhan: 'Đang xử lý', lop: 'badge-neutral' };
}

export function layNhanTrangThaiKhieuNai(trangThai) {
  const bang = {
    MO: { nhan: 'Đang mở', lop: 'badge-danger' },
    DA_GIAI_QUYET: { nhan: 'Đã giải quyết', lop: 'badge-success' },
    TU_CHOI: { nhan: 'Đã bác khiếu nại', lop: 'badge-neutral' },
  };
  return bang[trangThai] || { nhan: 'Đang xử lý', lop: 'badge-neutral' };
}

/**
 * Ánh xạ chuẩn duy nhất cho nhãn tiền trên thẻ buổi dạy Cổng Gia sư (không còn nhánh chết).
 */
export function layNhanTienDonGiaSu(don) {
  const ttDon = don?.trangThai;
  const ttTien = don?.trangThaiThanhToan;
  const tongTien = Number(don?.tongTien || 0);

  if (ttTien === 'DA_HOAN') {
    return { hien: true, hienSoTien: false, nhan: 'Đã hoàn về ví học viên', lop: 'badge-neutral', linkSoCai: false };
  }

  if (
    tongTien <= 0 ||
    ((ttDon === 'BI_TU_CHOI' || ttDon === 'DA_HUY' || ttDon === 'HET_HAN') && ttTien !== 'DA_HOAN')
  ) {
    return { hien: false, hienSoTien: false, nhan: '', lop: '', linkSoCai: false };
  }

  if (ttDon === 'TRANH_CHAP') {
    return { hien: true, hienSoTien: true, nhan: 'Đang tạm giữ (khiếu nại)', lop: 'badge-danger', linkSoCai: false };
  }

  if (!ttTien || ttTien === 'CHO_THANH_TOAN' || ttTien === 'THAT_BAI') {
    return { hien: true, hienSoTien: true, nhan: 'Chờ thanh toán', lop: 'badge-warning', linkSoCai: false };
  }

  if (ttTien === 'DANG_GIU') {
    return { hien: true, hienSoTien: true, nhan: 'Sắp nhận (dự kiến)', lop: 'badge-info', linkSoCai: false };
  }

  if (ttTien === 'DA_GIAI_NGAN') {
    return { hien: true, hienSoTien: true, nhan: 'Đã nhận vào ví →', lop: 'badge-success', linkSoCai: true };
  }

  return { hien: false, hienSoTien: false, nhan: '', lop: '', linkSoCai: false };
}

/**
 * Rút gọn câu cảnh báo di chuyển thành 1 câu súc tích, giữ nguyên chi tiết đầy đủ cho tooltip.
 */
export function rutGonCanhBaoDiChuyen(uocTinh) {
  if (!uocTinh) return { tomTat: '', chiTiet: '' };
  const goc = String(uocTinh.thongBaoKhaThi || '').trim();
  const kmMatch = goc.match(/([\d.,]+)\s*km/i);
  const canMatch =
    goc.match(/(?:cần|tối thiểu|ít nhất)\s*(?:≥\s*)?(\d+)\s*phút/i) || goc.match(/(\d+)\s*phút\s*di chuyển/i);
  const trongMatch = goc.match(/trống\s*(\d+)\s*phút/i) || goc.match(/chỉ có\s*(\d+)\s*phút/i);

  const km =
    uocTinh.khoangCachKm != null
      ? Number(uocTinh.khoangCachKm).toFixed(1).replace('.', ',')
      : kmMatch
        ? kmMatch[1].replace('.', ',')
        : null;
  const phutCan =
    uocTinh.thoiGianDiChuyenPhut != null ? uocTinh.thoiGianDiChuyenPhut : canMatch ? canMatch[1] : null;
  const phutTrong = uocTinh.thoiGianTrongPhut != null ? uocTinh.thoiGianTrongPhut : trongMatch ? trongMatch[1] : 0;

  if (uocTinh.mucDoKhaThi === 'KHONG_KHA_THI' && (km || phutCan)) {
    return {
      tomTat: `Không kịp di chuyển: cách lớp trước ${km || '—'} km, cần ≥${phutCan || '—'} phút, đang trống ${phutTrong ?? 0} phút.`,
      chiTiet: goc || 'Khoảng thời gian trống giữa 2 ca dạy trực tiếp không đủ để di chuyển an toàn.',
    };
  }

  if (goc.length > 92) {
    return {
      tomTat: `${goc.slice(0, 89)}...`,
      chiTiet: goc,
    };
  }
  return { tomTat: goc, chiTiet: goc };
}

/**
 * Xuất sổ cái giao dịch ví gia sư từ dữ liệu thật ra tệp CSV (UTF-8 BOM cho Excel).
 */
export function xuatCsvSoCaiGiaSu(dsGiaoDich = []) {
  const tieuDe = [
    'STT',
    'Thời gian',
    'Loại giao dịch',
    'Số tiền (VND)',
    'Số dư trước (VND)',
    'Số dư sau (VND)',
    'Học viên trả (VND)',
    'Phí nền tảng (VND)',
    'Tỷ lệ phí (%)',
    'Mã buổi học',
    'Học viên',
    'Môn học',
    'Ngân hàng rút',
    'Ghi chú',
  ];
  const dong = dsGiaoDich.map((g) => [
    g.soThuTu,
    g.taoLuc,
    layNhanLoaiSoCaiGiaSu(g.loai).nhan,
    g.soTien,
    g.soDuTruoc,
    g.soDuSau,
    g.soHocVienTra ?? '',
    g.phiNenTang ?? '',
    g.tyLePhi ?? '',
    g.maDon || '',
    g.tenHocVien || '',
    g.tenMonHoc || '',
    g.tenNganHangRut ? `${g.tenNganHangRut} ${g.soTaiKhoanRutChe || ''}`.trim() : '',
    g.ghiChu || '',
  ]);
  const csvContent =
    '\uFEFF' +
    [tieuDe, ...dong]
      .map((row) => row.map((cell) => `"${String(cell ?? '').replace(/"/g, '""')}"`).join(','))
      .join('\r\n');

  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `so-cai-vi-gia-su-${new Date().toISOString().slice(0, 10)}.csv`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}
