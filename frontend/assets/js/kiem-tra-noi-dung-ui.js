/**
 * Bộ kiểm tra nội dung hiển thị trên giao diện web TutorHub (Runtime DOM Auditor & Client Validator).
 * Chặn 5 nhóm nội dung không phù hợp hiển thị với người dùng cuối:
 *  1. LO_CONG_NGHE_NOI_BO (.NET, EF Core, SQL Server, SSMS, Clean Architecture, BCNF/4NF, Class v4, JWT, Signed URL...)
 *  2. LO_MA_TAI_LIEU_DEV (BR-TUT-01, UC_*, Module 1..9, Seed Data, TODO, FIXME...)
 *  3. LO_TEN_THUC_THE_CSDL (GiaSuMonHoc, TaiKhoanNganHang, LichLapHangTuan, /daXacMinh, kho Private...)
 *  4. LO_ENUM_THO_UI (HOC_SINH, GIA_SU, CHO_DUYET, DA_XOA, DANG_GIU, BANG_CAP, NGHI_PHEP, TRUC_TUYEN...)
 *  5. NOI_DUNG_KHONG_PHU_HOP (ngôn từ thô tục/nhạy cảm, hoặc chèn SĐT/Zalo/Facebook/URL ngoài vào hồ sơ công khai)
 */

export const QUY_TAC_HIEN_THI_WEB = [
  {
    nhomQuyTac: 'LO_CONG_NGHE_NOI_BO',
    maQuyTac: 'CONG_NGHE_BACKEND',
    bieuThuc: /(?:\.NET\s*\d+|\b(?:ASP\.NET|EF\s*Core|Entity\s*Framework|SQL\s*Server|SSMS|PostgreSQL|Prisma|Express\.js|Node\.js)\b)/gi,
    lyDo: 'Không được hiển thị tên công nghệ backend/CSDL (.NET, EF Core, SQL Server, SSMS...) lên giao diện người dùng.',
  },
  {
    nhomQuyTac: 'LO_CONG_NGHE_NOI_BO',
    maQuyTac: 'KIEN_TRUC_CHUAN_HOA',
    bieuThuc: /\b(Clean\s*Architecture|BCNF|4NF|3NF|2NF|1NF|Class\s*v4|UML|draw\.?io|Table-per-Type|TPT|TPH|ERD|OCL)\b/gi,
    lyDo: 'Không được hiển thị thuật ngữ kiến trúc phần mềm / chuẩn hóa CSDL (Clean Architecture, BCNF/4NF, Class v4...) lên giao diện web.',
  },
  {
    nhomQuyTac: 'LO_CONG_NGHE_NOI_BO',
    maQuyTac: 'THUAT_NGU_KY_THUAT_API',
    bieuThuc: /(?:\/api\/health|\b(?:Web\s*API|REST\s*API|JWT\s*Bearer|Bearer\s*Token|Signed\s*URL|Storage\s*Key|Endpoint\s*API|GET\s*\/api\/|POST\s*\/api\/|PUT\s*\/api\/|DELETE\s*\/api\/|localhost:\d+|Leaflet\s*\d+\.\d+|OpenStreetMap)\b)/gi,
    lyDo: 'Không được hiển thị thuật ngữ kỹ thuật giao thức/API/thư viện (Web API, JWT Bearer, Signed URL, Storage Key, /api/...) lên giao diện người dùng.',
  },
  {
    nhomQuyTac: 'LO_MA_TAI_LIEU_DEV',
    maQuyTac: 'MA_LUAT_NGHIEP_VU',
    bieuThuc: /\b(BR-(?:TUT|BKG|FIN|PAY|REV|CMP|MSG|ADM)-\d+|UC_[A-Za-z0-9_]+)\b/gi,
    lyDo: 'Không được hiển thị mã tài liệu nghiệp vụ/Use Case nội bộ (BR-*, UC_*) trên web.',
  },
  {
    nhomQuyTac: 'LO_MA_TAI_LIEU_DEV',
    maQuyTac: 'TIEN_DO_PHAT_TRIEN',
    bieuThuc: /(?:\(Dev\)|\b(?:Module\s*\d+|Seed\s*Data|TODO|FIXME|Lorem\s+ipsum|xóa\s*mềm)\b)/gi,
    lyDo: 'Không được hiển thị tiến độ phát triển hoặc thuật ngữ dữ liệu thử nghiệm kỹ thuật (Module 1..9, Seed Data, TODO, xóa mềm) lên web.',
  },
  {
    nhomQuyTac: 'LO_TEN_THUC_THE_CSDL',
    maQuyTac: 'TEN_LOP_THUC_THE',
    bieuThuc: /\b(GiaSuMonHoc|TaiKhoanNganHang|LichLapHangTuan|LichNgayCuThe|MucYeuThich|DonDatLich|GiayToGiaSu|NguoiDungThamGia|TaiKhoanVi|GiaoDichVi|YeuCauRutTien|QuanTriVien|AppDbContext)\b/g,
    lyDo: 'Không được hiển thị tên lớp/bảng cơ sở dữ liệu dạng PascalCase lên giao diện người dùng.',
  },
  {
    nhomQuyTac: 'LO_TEN_THUC_THE_CSDL',
    maQuyTac: 'THUOC_TINH_UML_KHO_PRIVATE',
    bieuThuc: /(\/daXacMinh|\/vaiTro|\/tongTien|kho\s+Private|\?id=\.\.\.)/gi,
    lyDo: 'Không được hiển thị thuộc tính dẫn xuất UML hoặc tên lưu trữ kỹ thuật (kho Private) lên giao diện người dùng.',
  },
  {
    nhomQuyTac: 'LO_ENUM_THO_UI',
    maQuyTac: 'MA_ENUM_CHUA_VIET_HOA',
    bieuThuc: /\b(HOC_SINH|GIA_SU|QUAN_TRI|CHO_DUYET|DA_DUYET|TU_CHOI|NHAP|DA_XOA|BI_KHOA|HOAT_DONG|DANG_GIU|DA_GIAI_NGAN|DA_HOAN|CHO_XAC_NHAN|DA_XAC_NHAN|HOAN_THANH|TRANH_CHAP|BANG_CAP|CHUNG_CHI|CAN_CUOC|NGHI_PHEP|BO_SUNG|TRUC_TUYEN|TRUC_TIEP|TIEU_HOC|DAI_HOC|LUYEN_THI|VI_NOI_BO|GIA_LAP)\b/g,
    lyDo: 'Không được hiển thị mã Enum thô dạng UPPER_SNAKE_CASE trên giao diện người dùng; hãy dùng nhãn tiếng Việt tự nhiên.',
  },
];

const REGEX_TU_NGU_KHONG_PHU_HOP =
  /\b(đm|đkm|vcl|vl|cc|đéo|đụ|lồn|cặc|buồi|chó\s*đẻ|mất\s*dạy|lừa\s*đảo|cờ\s*bạc|cá\s*độ|tài\s*xỉu|lô\s*đề|sex|porn|xxx|fuck|shit|bitch)\b/gi;

const QUY_TAC_LIEN_HE_NGOAI_LUONG = [
  {
    maQuyTac: 'LO_SO_DIEN_THOAI',
    bieuThuc: /(?<!\d)(?:\+?84|0)(?:[\s.\-]*\d){8,9}(?!\d)/g,
    lyDo: 'Không được chèn số điện thoại cá nhân vào nội dung hiển thị công khai.',
  },
  {
    maQuyTac: 'LO_EMAIL',
    bieuThuc: /[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}/gi,
    lyDo: 'Không được chèn địa chỉ email cá nhân vào nội dung hiển thị công khai.',
  },
  {
    maQuyTac: 'LO_ZALO_FACEBOOK',
    bieuThuc: /\b(zalo|zl\s*[:.]|fb\.com|fb\.me|facebook\.com|telegram|t\.me|whatsapp|viber)\b/gi,
    lyDo: 'Không được chèn liên kết mạng xã hội ngoài nền tảng (Zalo, Facebook, Telegram...) vào nội dung công khai.',
  },
  {
    maQuyTac: 'LO_URL_NGOAI',
    bieuThuc: /\b(?:https?:\/\/|www\.)[^\s<>'"]+/gi,
    lyDo: 'Không được chèn đường dẫn website bên ngoài vào nội dung hồ sơ công khai.',
  },
];

export function kiemTraVanBanHienThiWeb(vanBan, nguon = 'dom') {
  const dsViPham = [];
  if (!vanBan || typeof vanBan !== 'string') {
    return { hopLe: true, dsViPham };
  }

  for (const qt of QUY_TAC_HIEN_THI_WEB) {
    qt.bieuThuc.lastIndex = 0;
    let m;
    while ((m = qt.bieuThuc.exec(vanBan)) !== null) {
      dsViPham.push({
        nhomQuyTac: qt.nhomQuyTac,
        maQuyTac: qt.maQuyTac,
        nguon,
        doanViPham: m[0],
        lyDo: qt.lyDo,
      });
    }
  }

  REGEX_TU_NGU_KHONG_PHU_HOP.lastIndex = 0;
  let mTu;
  while ((mTu = REGEX_TU_NGU_KHONG_PHU_HOP.exec(vanBan)) !== null) {
    dsViPham.push({
      nhomQuyTac: 'NOI_DUNG_KHONG_PHU_HOP',
      maQuyTac: 'NGON_TU_KHONG_PHU_HOP',
      nguon,
      doanViPham: mTu[0],
      lyDo: 'Nội dung chứa ngôn từ không phù hợp với môi trường giáo dục.',
    });
  }

  return { hopLe: dsViPham.length === 0, dsViPham };
}

export function kiemTraNoiDungNguoiDungNhap(vanBan, { kiemTraLienHeNgoaiLuong = true } = {}) {
  const dsViPham = [];
  if (!vanBan || typeof vanBan !== 'string') {
    return { hopLe: true, dsViPham };
  }

  REGEX_TU_NGU_KHONG_PHU_HOP.lastIndex = 0;
  let m;
  while ((m = REGEX_TU_NGU_KHONG_PHU_HOP.exec(vanBan)) !== null) {
    dsViPham.push({
      nhomQuyTac: 'NOI_DUNG_KHONG_PHU_HOP',
      maQuyTac: 'NGON_TU_KHONG_PHU_HOP',
      doanViPham: m[0],
      lyDo: 'Nội dung chứa từ ngữ không phù hợp với môi trường giáo dục.',
    });
  }

  if (kiemTraLienHeNgoaiLuong) {
    for (const qt of QUY_TAC_LIEN_HE_NGOAI_LUONG) {
      qt.bieuThuc.lastIndex = 0;
      let mLh;
      while ((mLh = qt.bieuThuc.exec(vanBan)) !== null) {
        dsViPham.push({
          nhomQuyTac: 'NOI_DUNG_KHONG_PHU_HOP',
          maQuyTac: qt.maQuyTac,
          doanViPham: mLh[0],
          lyDo: qt.lyDo,
        });
      }
    }
  }

  return { hopLe: dsViPham.length === 0, dsViPham };
}

/**
 * Quét toàn bộ DOM thực tế sau khi render động bằng JS để đảm bảo 0 nội dung kỹ thuật/không phù hợp.
 * Bỏ qua hộp bản quyền bản đồ `.leaflet-control-attribution` của thư viện bản đồ bên thứ 3.
 */
export function kiemTraNoiDungHienThiDom(root = typeof document !== 'undefined' ? document.body : null) {
  if (!root || typeof document === 'undefined') {
    return { hopLe: true, tongViPham: 0, dsViPham: [] };
  }

  const tatCaViPham = [];

  // 1. Tiêu đề trang (<title>)
  const kqTitle = kiemTraVanBanHienThiWeb(document.title || '', 'document.title');
  tatCaViPham.push(...kqTitle.dsViPham);

  // 2. Toàn bộ văn bản hiển thị thực tế trong DOM (loại trừ attribution của bản đồ Leaflet)
  const clone = root.cloneNode(true);
  clone.querySelectorAll('script, style, noscript, .leaflet-control-attribution').forEach((el) => el.remove());
  const vanBanHienThi = clone.textContent || '';
  const kqBody = kiemTraVanBanHienThiWeb(vanBanHienThi, 'document.body');
  tatCaViPham.push(...kqBody.dsViPham);

  // 3. Thuộc tính hiển thị với người dùng (placeholder, title, alt)
  const attrTexts = [];
  root.querySelectorAll('[placeholder], [title], [alt]').forEach((el) => {
    if (el.closest('.leaflet-control-attribution')) return;
    ['placeholder', 'title', 'alt'].forEach((attr) => {
      const val = el.getAttribute(attr);
      if (val) attrTexts.push(val);
    });
  });

  if (attrTexts.length > 0) {
    const kqAttr = kiemTraVanBanHienThiWeb(attrTexts.join(' \n '), 'attributes');
    tatCaViPham.push(...kqAttr.dsViPham);
  }

  return {
    trang: window.location.pathname,
    hopLe: tatCaViPham.length === 0,
    tongViPham: tatCaViPham.length,
    dsViPham: tatCaViPham,
  };
}

if (typeof window !== 'undefined') {
  window.__tutorhubUiAudit = () => kiemTraNoiDungHienThiDom(document.body);
}
