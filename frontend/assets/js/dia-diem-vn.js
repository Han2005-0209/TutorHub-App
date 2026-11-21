/**
 * dia-diem-vn.js — Dữ liệu và bộ máy gợi ý tìm kiếm địa điểm (Tỉnh/Thành phố, Quận/Huyện)
 * Hỗ trợ tìm kiếm gần đúng, gõ không dấu, viết tắt (CT, HN, SG, TPHCM, ĐN...)
 */

export const DS_TINH_THANH_VIET_NAM = [
  // 5 Thành phố trực thuộc Trung ương & các quận trọng điểm
  { ten: 'Cần Thơ', loai: 'Thành phố trực thuộc TW', vietTat: ['ct', 'can tho'] },
  { ten: 'Ninh Kiều, Cần Thơ', loai: 'Quận trung tâm', vietTat: ['ninh kieu', 'ninh kieu can tho'] },
  { ten: 'Bình Thủy, Cần Thơ', loai: 'Quận', vietTat: ['binh thuy', 'binh thuy can tho'] },
  { ten: 'Cái Răng, Cần Thơ', loai: 'Quận', vietTat: ['cai rang', 'cai rang can tho'] },
  { ten: 'Ô Môn, Cần Thơ', loai: 'Quận', vietTat: ['o mon', 'o mon can tho'] },
  { ten: 'Thốt Nốt, Cần Thơ', loai: 'Quận', vietTat: ['thot not', 'thot not can tho'] },
  { ten: 'Phong Điền, Cần Thơ', loai: 'Huyện', vietTat: ['phong dien', 'phong dien can tho'] },

  { ten: 'TP. Hồ Chí Minh', loai: 'Thành phố trực thuộc TW', vietTat: ['hcm', 'tphcm', 'tp hcm', 'sg', 'sai gon'] },
  { ten: 'Quận 1, TP. Hồ Chí Minh', loai: 'Quận trung tâm', vietTat: ['q1', 'quan 1', 'quan 1 hcm'] },
  { ten: 'Quận 3, TP. Hồ Chí Minh', loai: 'Quận', vietTat: ['q3', 'quan 3'] },
  { ten: 'Quận 7, TP. Hồ Chí Minh', loai: 'Quận', vietTat: ['q7', 'quan 7'] },
  { ten: 'Quận 10, TP. Hồ Chí Minh', loai: 'Quận', vietTat: ['q10', 'quan 10'] },
  { ten: 'Bình Thạnh, TP. Hồ Chí Minh', loai: 'Quận', vietTat: ['binh thanh', 'binh thanh hcm'] },
  { ten: 'Gò Vấp, TP. Hồ Chí Minh', loai: 'Quận', vietTat: ['go vap', 'go vap hcm'] },
  { ten: 'Tân Bình, TP. Hồ Chí Minh', loai: 'Quận', vietTat: ['tan binh', 'tan binh hcm'] },
  { ten: 'TP. Thủ Đức, TP. Hồ Chí Minh', loai: 'Thành phố', vietTat: ['thu duc', 'tp thu duc'] },
  { ten: 'Phú Nhuận, TP. Hồ Chí Minh', loai: 'Quận', vietTat: ['phu nhuan'] },

  { ten: 'Hà Nội', loai: 'Thủ đô trực thuộc TW', vietTat: ['hn', 'ha noi'] },
  { ten: 'Cầu Giấy, Hà Nội', loai: 'Quận trung tâm', vietTat: ['cau giay', 'cau giay hn'] },
  { ten: 'Đống Đa, Hà Nội', loai: 'Quận', vietTat: ['dong da', 'dong da hn'] },
  { ten: 'Ba Đình, Hà Nội', loai: 'Quận', vietTat: ['ba dinh', 'ba dinh hn'] },
  { ten: 'Hoàn Kiếm, Hà Nội', loai: 'Quận', vietTat: ['hoan kiem', 'hoan kiem hn'] },
  { ten: 'Hai Bà Trưng, Hà Nội', loai: 'Quận', vietTat: ['hai ba trung'] },
  { ten: 'Thanh Xuân, Hà Nội', loai: 'Quận', vietTat: ['thanh xuan', 'thanh xuan hn'] },
  { ten: 'Hà Đông, Hà Nội', loai: 'Quận', vietTat: ['ha dong', 'ha dong hn'] },
  { ten: 'Nam Từ Liêm, Hà Nội', loai: 'Quận', vietTat: ['nam tu liem'] },
  { ten: 'Bắc Từ Liêm, Hà Nội', loai: 'Quận', vietTat: ['bac tu liem'] },
  { ten: 'Tây Hồ, Hà Nội', loai: 'Quận', vietTat: ['tay ho'] },
  { ten: 'Hoàng Mai, Hà Nội', loai: 'Quận', vietTat: ['hoang mai'] },

  { ten: 'Đà Nẵng', loai: 'Thành phố trực thuộc TW', vietTat: ['dn', 'da nang'] },
  { ten: 'Hải Châu, Đà Nẵng', loai: 'Quận trung tâm', vietTat: ['hai chau', 'hai chau da nang'] },
  { ten: 'Thanh Khê, Đà Nẵng', loai: 'Quận', vietTat: ['thanh khe'] },
  { ten: 'Sơn Trà, Đà Nẵng', loai: 'Quận', vietTat: ['son tra'] },
  { ten: 'Ngũ Hành Sơn, Đà Nẵng', loai: 'Quận', vietTat: ['ngu hanh son'] },
  { ten: 'Cẩm Lệ, Đà Nẵng', loai: 'Quận', vietTat: ['cam le'] },
  { ten: 'Liên Chiểu, Đà Nẵng', loai: 'Quận', vietTat: ['lien chieu'] },
  { ten: 'Huyện Hoàng Sa, Đà Nẵng', loai: 'Huyện đảo chủ quyền Việt Nam', vietTat: ['hoang sa', 'quan dao hoang sa'] },

  { ten: 'Hải Phòng', loai: 'Thành phố trực thuộc TW', vietTat: ['hp', 'hai phong'] },
  { ten: 'Hồng Bàng, Hải Phòng', loai: 'Quận', vietTat: ['hong bang'] },
  { ten: 'Ngô Quyền, Hải Phòng', loai: 'Quận', vietTat: ['ngo quyen'] },
  { ten: 'Lê Chân, Hải Phòng', loai: 'Quận', vietTat: ['le chan'] },

  // Các tỉnh/thành phố lớn & đô thị trọng điểm
  { ten: 'Bình Dương', loai: 'Tỉnh', vietTat: ['bd', 'binh duong'] },
  { ten: 'Thủ Dầu Một, Bình Dương', loai: 'Thành phố', vietTat: ['thu dau mot'] },
  { ten: 'Thuận An, Bình Dương', loai: 'Thành phố', vietTat: ['thuan an'] },
  { ten: 'Dĩ An, Bình Dương', loai: 'Thành phố', vietTat: ['di an'] },

  { ten: 'Đồng Nai', loai: 'Tỉnh', vietTat: ['dong nai'] },
  { ten: 'Biên Hòa, Đồng Nai', loai: 'Thành phố', vietTat: ['bien hoa'] },

  { ten: 'Bà Rịa - Vũng Tàu', loai: 'Tỉnh', vietTat: ['vung tau', 'ba ria', 'brvt'] },
  { ten: 'Vũng Tàu, Bà Rịa - Vũng Tàu', loai: 'Thành phố', vietTat: ['vung tau'] },

  { ten: 'Thừa Thiên Huế', loai: 'Tỉnh', vietTat: ['hue', 'thua thien hue'] },
  { ten: 'Nha Trang, Khánh Hòa', loai: 'Thành phố', vietTat: ['nha trang', 'khanh hoa'] },
  { ten: 'Huyện Trường Sa, Khánh Hòa', loai: 'Huyện đảo chủ quyền Việt Nam', vietTat: ['truong sa', 'quan dao truong sa'] },
  { ten: 'Đà Lạt, Lâm Đồng', loai: 'Thành phố', vietTat: ['da lat', 'lam dong'] },
  { ten: 'Hạ Long, Quảng Ninh', loai: 'Thành phố', vietTat: ['ha long', 'quang ninh'] },
  { ten: 'Bắc Ninh', loai: 'Tỉnh', vietTat: ['bac ninh'] },
  { ten: 'Vinh, Nghệ An', loai: 'Thành phố', vietTat: ['vinh', 'nghe an'] },
  { ten: 'Thanh Hóa', loai: 'Tỉnh', vietTat: ['thanh hoa'] },
  { ten: 'Vĩnh Long', loai: 'Tỉnh', vietTat: ['vinh long'] },
  { ten: 'Tiền Giang', loai: 'Tỉnh', vietTat: ['tien giang', 'my tho'] },
  { ten: 'Bến Tre', loai: 'Tỉnh', vietTat: ['ben tre'] },
  { ten: 'An Giang', loai: 'Tỉnh', vietTat: ['an giang', 'long xuyen'] },
  { ten: 'Kiên Giang', loai: 'Tỉnh', vietTat: ['kien giang', 'rach gia', 'phu quoc'] },
  { ten: 'Đồng Tháp', loai: 'Tỉnh', vietTat: ['dong thap', 'cao lanh'] },
  { ten: 'Hậu Giang', loai: 'Tỉnh', vietTat: ['hau giang', 'vi thanh'] },
  { ten: 'Sóc Trăng', loai: 'Tỉnh', vietTat: ['soc trang'] },
  { ten: 'Bạc Liêu', loai: 'Tỉnh', vietTat: ['bac lieu'] },
  { ten: 'Cà Mau', loai: 'Tỉnh', vietTat: ['ca mau'] },
  { ten: 'Trà Vinh', loai: 'Tỉnh', vietTat: ['tra vinh'] },
  { ten: 'Bình Định', loai: 'Tỉnh', vietTat: ['binh dinh', 'quy nhon'] },
  { ten: 'Quảng Nam', loai: 'Tỉnh', vietTat: ['quang nam', 'hoi an', 'tam ky'] },
  { ten: 'Quảng Ngãi', loai: 'Tỉnh', vietTat: ['quang ngai'] },
  { ten: 'Phú Yên', loai: 'Tỉnh', vietTat: ['phu yen', 'tuy hoa'] },
  { ten: 'Đắk Lắk', loai: 'Tỉnh', vietTat: ['dak lak', 'buon ma thuot'] },
  { ten: 'Gia Lai', loai: 'Tỉnh', vietTat: ['gia lai', 'pleiku'] },
  { ten: 'Thái Nguyên', loai: 'Tỉnh', vietTat: ['thai nguyen'] },
  { ten: 'Nam Định', loai: 'Tỉnh', vietTat: ['nam dinh'] },
  { ten: 'Ninh Bình', loai: 'Tỉnh', vietTat: ['ninh binh'] },
  { ten: 'Thái Bình', loai: 'Tỉnh', vietTat: ['thai binh'] },
  { ten: 'Hải Dương', loai: 'Tỉnh', vietTat: ['hai duong'] },
  { ten: 'Hưng Yên', loai: 'Tỉnh', vietTat: ['hung yen'] },
  { ten: 'Hà Nam', loai: 'Tỉnh', vietTat: ['ha nam', 'phu ly'] },
  { ten: 'Vĩnh Phúc', loai: 'Tỉnh', vietTat: ['vinh phuc'] },
  { ten: 'Phú Thọ', loai: 'Tỉnh', vietTat: ['phu tho', 'viet tri'] },
  { ten: 'Bắc Giang', loai: 'Tỉnh', vietTat: ['bac giang'] },
  { ten: 'Lạng Sơn', loai: 'Tỉnh', vietTat: ['lang son'] },
  { ten: 'Lào Cai', loai: 'Tỉnh', vietTat: ['lao cai', 'sa pa'] },
  { ten: 'Hòa Bình', loai: 'Tỉnh', vietTat: ['hoa binh'] },
  { ten: 'Sơn La', loai: 'Tỉnh', vietTat: ['son la'] },
  { ten: 'Hà Tĩnh', loai: 'Tỉnh', vietTat: ['ha tinh'] },
  { ten: 'Quảng Bình', loai: 'Tỉnh', vietTat: ['quang binh', 'dong hoi'] },
  { ten: 'Quảng Trị', loai: 'Tỉnh', vietTat: ['quang tri'] },
  { ten: 'Bình Phước', loai: 'Tỉnh', vietTat: ['binh phuoc'] },
  { ten: 'Tây Ninh', loai: 'Tỉnh', vietTat: ['tay ninh'] },
  { ten: 'Bình Thuận', loai: 'Tỉnh', vietTat: ['binh thuan', 'phan thiet'] },
  { ten: 'Ninh Thuận', loai: 'Tỉnh', vietTat: ['ninh thuan', 'phan rang'] },
  { ten: 'Kon Tum', loai: 'Tỉnh', vietTat: ['kon tum'] },
  { ten: 'Đắk Nông', loai: 'Tỉnh', vietTat: ['dak nong'] },
  { ten: 'Yên Bái', loai: 'Tỉnh', vietTat: ['yen bai'] },
  { ten: 'Tuyên Quang', loai: 'Tỉnh', vietTat: ['tuyen quang'] },
  { ten: 'Hà Giang', loai: 'Tỉnh', vietTat: ['ha giang'] },
  { ten: 'Cao Bằng', loai: 'Tỉnh', vietTat: ['cao bang'] },
  { ten: 'Bắc Kạn', loai: 'Tỉnh', vietTat: ['bac kan'] },
  { ten: 'Điện Biên', loai: 'Tỉnh', vietTat: ['dien bien'] },
  { ten: 'Lai Châu', loai: 'Tỉnh', vietTat: ['lai chau'] },
];

/**
 * Loại bỏ dấu tiếng Việt để so khớp gần đúng
 */
export function boDauTiengViet(str) {
  if (!str) return '';
  return str
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[đĐ]/g, (m) => (m === 'đ' ? 'd' : 'D'))
    .toLowerCase()
    .trim();
}

/**
 * Danh sách địa điểm phổ biến hiển thị khi ô trống
 */
export function layDiaDiemPhoBien() {
  return [
    { ten: 'Cần Thơ', loai: 'Thành phố trung tâm', icon: 'pin' },
    { ten: 'TP. Hồ Chí Minh', loai: 'Đô thị đặc biệt', icon: 'pin' },
    { ten: 'Hà Nội', loai: 'Thủ đô Hà Nội', icon: 'pin' },
    { ten: 'Đà Nẵng', loai: 'Thành phố du lịch', icon: 'pin' },
    { ten: 'Hải Phòng', loai: 'Thành phố cảng', icon: 'pin' },
    { ten: 'Bình Dương', loai: 'Khu vực công nghiệp', icon: 'pin' },
  ];
}

/**
 * Tìm kiếm danh sách địa điểm theo từ khóa gần đúng
 * @param {string} tuKhoa - Chuỗi người dùng gõ (ví dụ "cần t", "can tho", "hcm", "ninh kieu")
 * @param {number} soLuongToiDa - Giới hạn số lượng gợi ý
 */
export function timKiemDiaDiem(tuKhoa, soLuongToiDa = 7) {
  const raw = (tuKhoa || '').trim();
  if (!raw) return layDiaDiemPhoBien();

  const normalized = boDauTiengViet(raw);

  // Tính điểm phù hợp:
  // 1. Khớp chính xác tên (100)
  // 2. Viết tắt khớp chính xác (90)
  // 3. Tên bắt đầu bằng từ khóa (80)
  // 4. Có từ con bắt đầu bằng từ khóa (ví dụ "cần t" -> "Cần Thơ", "ninh kiều" -> "Ninh Kiều, Cần Thơ") (70)
  // 5. Chứa từ khóa bên trong (50)
  const ketQua = [];

  for (const item of DS_TINH_THANH_VIET_NAM) {
    const itemNorm = boDauTiengViet(item.ten);
    let diem = 0;

    if (itemNorm === normalized) {
      diem = 100;
    } else if (item.vietTat && item.vietTat.some((v) => v === normalized || normalized.startsWith(v))) {
      diem = 90;
    } else if (itemNorm.startsWith(normalized)) {
      diem = 80;
    } else {
      // Kiểm tra xem các từ đầu có khớp không (ví dụ "cần t" -> "cần thơ" hoặc "ninh kieu" -> "ninh kiều, cần thơ")
      const cacTuItem = itemNorm.split(/[\s,]+/);
      const cacTuKhoa = normalized.split(/\s+/);

      let khopTungPhan = true;
      for (let i = 0; i < cacTuKhoa.length; i++) {
        if (!cacTuItem.some((w) => w.startsWith(cacTuKhoa[i]))) {
          khopTungPhan = false;
          break;
        }
      }

      if (khopTungPhan) {
        diem = 70;
      } else if (itemNorm.includes(normalized)) {
        diem = 50;
      }
    }

    if (diem > 0) {
      ketQua.push({
        ...item,
        icon: 'pin',
        diem,
      });
    }
  }

  // Sắp xếp theo điểm số giảm dần
  ketQua.sort((a, b) => b.diem - a.diem);

  return ketQua.slice(0, soLuongToiDa);
}
