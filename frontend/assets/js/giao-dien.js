/**
 * Thành phần Giao diện dùng chung theo phong cách tối giản lấy cảm hứng từ Superprof,
 * vẫn tách biệt rõ ràng 4 cổng vai trò (Khách công khai, Cổng Học sinh, Cổng Gia sư, Cổng Quản trị).
 */
import { api, layAccessToken, luuPhienToken, xoaPhienToken } from './api.js';
import { dinhDangTienVnd } from './dinh-dang.js';
import { icons } from './icons.js';
import { layNhanVaiTro } from './trang-thai.js';
import './kiem-tra-noi-dung-ui.js';

const KHOA_NGUOI_DUNG = 'tutorhub_nguoi_dung';

const ANH_CHAN_DUNG_MAC_DINH = {
  '30000000-0000-0000-0000-000000000004': '/assets/img/tutors/tutor-hoang-minh.jpg',
  '30000000-0000-0000-0000-000000000005': '/assets/img/tutors/tutor-thu-ha.jpg',
  '30000000-0000-0000-0000-000000000006': '/assets/img/tutors/tutor-thanh-tung.jpg',
  '30000000-0000-0000-0000-000000000007': '/assets/img/tutors/tutor-quoc-bao.jpg',
  '30000000-0000-0000-0000-000000000008': '/assets/img/tutors/tutor-ngoc-mai.jpg',
};

export function toiUuUrlCloudinary(url) {
  if (!url || typeof url !== 'string') return url;
  if (url.includes('res.cloudinary.com/') && url.includes('/image/upload/') && !url.includes('/image/upload/f_auto')) {
    return url.replace('/image/upload/', '/image/upload/f_auto,q_auto,w_520,h_520,c_fill,g_face/');
  }
  return url;
}

export function layAnhGiaSu(gs) {
  if (gs?.anhDaiDienUrl) return toiUuUrlCloudinary(gs.anhDaiDienUrl);
  const id = String(gs?.id || '').toLowerCase();
  if (ANH_CHAN_DUNG_MAC_DINH[id]) return ANH_CHAN_DUNG_MAC_DINH[id];
  const ten = (gs?.hoTen || '').toLowerCase();
  if (ten.includes('minh')) return '/assets/img/tutors/tutor-hoang-minh.jpg';
  if (ten.includes('thu hà') || ten.includes('thu ha')) return '/assets/img/tutors/tutor-thu-ha.jpg';
  if (ten.includes('bảo')) return '/assets/img/tutors/tutor-quoc-bao.jpg';
  if (ten.includes('tùng')) return '/assets/img/tutors/tutor-thanh-tung.jpg';
  if (ten.includes('mai')) return '/assets/img/tutors/tutor-ngoc-mai.jpg';
  return '/assets/img/tutors/tutor-mac-dinh.svg';
}

export function layIconMonHoc(tenMon = '') {
  const s = tenMon.toLowerCase();
  if (s.includes('toán')) return icons.mathRoot(22);
  if (s.includes('anh') || s.includes('ngữ') || s.includes('ielts')) return icons.languages(22);
  if (s.includes('lý') || s.includes('vật')) return icons.atom(22);
  if (s.includes('hóa') || s.includes('sinh')) return icons.flask(22);
  if (s.includes('tin') || s.includes('lập trình')) return icons.code(22);
  if (s.includes('văn')) return icons.feather(22);
  return icons.bookOpen(22);
}

export function layTrangChinhTheoVaiTro(vaiTro) {
  if (vaiTro === 'HOC_SINH') return '/pages/hoc-sinh-tong-quan.html';
  if (vaiTro === 'GIA_SU') return '/pages/quan-ly-gia-su.html';
  if (vaiTro === 'QUAN_TRI') return '/pages/quan-tri.html';
  return '/index.html';
}

export function layNguoiDungHienTai() {
  try {
    const raw =
      window.localStorage.getItem(KHOA_NGUOI_DUNG) ||
      window.sessionStorage.getItem(KHOA_NGUOI_DUNG);
    return raw ? JSON.parse(raw) : null;
  } catch {
    return null;
  }
}

export function luuNguoiDungHienTai(nguoiDung) {
  try {
    const dsKho = [window.localStorage, window.sessionStorage];
    for (const kho of dsKho) {
      if (nguoiDung) {
        kho.setItem(KHOA_NGUOI_DUNG, JSON.stringify(nguoiDung));
      } else {
        kho.removeItem(KHOA_NGUOI_DUNG);
      }
    }
  } catch {
    // Bỏ qua
  }
}

export function luuPhienDangNhapDayDu(duLieuDangNhap) {
  if (!duLieuDangNhap) return;
  luuPhienToken({
    accessToken: duLieuDangNhap.accessToken,
    refreshToken: duLieuDangNhap.refreshToken || '',
  });
  luuNguoiDungHienTai(duLieuDangNhap.nguoiDung || null);
}

export async function dangXuatHeThong() {
  try {
    await api.post('/xac-thuc/dang-xuat', {});
  } catch {
    // Bỏ qua lỗi mạng khi đăng xuất
  }
  xoaPhienToken();
  luuNguoiDungHienTai(null);
  window.location.href = '/pages/dang-nhap.html';
}

export async function dongBoNguoiDungTuApi() {
  const token = layAccessToken();
  if (!token) {
    luuNguoiDungHienTai(null);
    ganThanhDieuHuongDayMobile(null);
    return null;
  }
  try {
    const res = await api.get('/nguoi-dung/toi');
    if (res?.duLieu) {
      luuNguoiDungHienTai(res.duLieu);
      ganThanhDieuHuongDayMobile(res.duLieu);
      khoiTaoThanhTabNgangMobilePortal();
      return res.duLieu;
    }
  } catch {
    // Nếu 401 đã được api.js xóa token
  }
  const u = layNguoiDungHienTai();
  ganThanhDieuHuongDayMobile(u);
  khoiTaoThanhTabNgangMobilePortal();
  return u;
}

export function layChuCaiDau(hoTen) {
  if (!hoTen) return 'TH';
  const parts = hoTen.trim().split(/\s+/);
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

export function layNhanThuTrongTuan(thu) {
  const bang = {
    1: 'Chủ nhật',
    2: 'Thứ 2',
    3: 'Thứ 3',
    4: 'Thứ 4',
    5: 'Thứ 5',
    6: 'Thứ 6',
    7: 'Thứ 7',
  };
  return bang[thu] || `Thứ ${thu}`;
}

export function renderHeader(selector = '#khung-header') {
  const el = document.querySelector(selector);
  if (!el) return;

  const user = layNguoiDungHienTai();
  const path = window.location.pathname;
  const laTrangChu = path === '/' || path.endsWith('/index.html');
  const chuKyHienTai = JSON.stringify({
    path,
    id: user?.id || null,
    vaiTro: user?.vaiTro || null,
    hoTen: user?.hoTen || null,
    soDuVi: user?.soDuVi ?? null,
  });
  if (el.dataset.renderedSig === chuKyHienTai && el.children.length > 0) {
    return;
  }
  el.dataset.renderedSig = chuKyHienTai;
  const laTrang = (p) => {
    if (p === '/index.html') return laTrangChu ? 'active' : '';
    return path.endsWith(p) || path.endsWith(p.replace(/\.html$/, '')) ? 'active' : '';
  };

  let headerClass = laTrangChu ? 'header-chung header-on-hero' : 'header-chung';
  let logoHref = '/index.html';
  let portalChipHtml = '';
  let centerHtml = '';
  let rightHtml = '';

  const formTimHeaderHtml = (anBanDau = false) => `
    <div class="sp-header-search-wrapper" style="position: relative;">
      <form class="sp-header-search ${anBanDau ? 'is-hidden' : ''}" id="form-tim-header" aria-label="Tìm kiếm nhanh gia sư">
        <input id="inp-tim-header" type="text" placeholder="Bạn muốn học gì?" aria-label="Bạn muốn học gì?" autocomplete="off" />
        <button type="submit" class="sp-header-search-btn" aria-label="Tìm kiếm">
          ${icons.search(15)}
        </button>
      </form>
      <div class="sp-search-dropdown" id="dropdown-tim-kiem-header" style="width: 320px; top: calc(100% + 8px); left: 0;">
        <button type="button" class="sp-ai-dropdown-btn" id="btn-mo-ai-header">
          <span>✦ Tìm kiếm với AI</span>
          <span>→</span>
        </button>
        <div class="sp-dropdown-list" style="margin-top: 0.5rem; max-height: 260px; overflow-y: auto;">
          <div class="sp-dropdown-item" data-mon="Toán học">
            <span class="sp-dropdown-icon">${icons.search(14)}</span>
            <span>Toán học</span>
          </div>
          <div class="sp-dropdown-item" data-mon="Tiếng Anh">
            <span class="sp-dropdown-icon">${icons.search(14)}</span>
            <span>Tiếng Anh</span>
          </div>
          <div class="sp-dropdown-item" data-mon="Luyện thi IELTS">
            <span class="sp-dropdown-icon">${icons.search(14)}</span>
            <span>Luyện thi IELTS</span>
          </div>
          <div class="sp-dropdown-item" data-mon="Hóa học">
            <span class="sp-dropdown-icon">${icons.search(14)}</span>
            <span>Hóa học</span>
          </div>
          <div class="sp-dropdown-item" data-mon="Vật lý">
            <span class="sp-dropdown-icon">${icons.search(14)}</span>
            <span>Vật lý</span>
          </div>
          <div class="sp-dropdown-item" data-mon="Ngữ văn">
            <span class="sp-dropdown-icon">${icons.search(14)}</span>
            <span>Ngữ văn</span>
          </div>
        </div>
      </div>
    </div>
  `;

  const userDropdownHtml = (u) => {
    const avatarBg = u.vaiTro === 'GIA_SU' ? '#3b82f6' : u.vaiTro === 'QUAN_TRI' ? '#6366f1' : '#f0656d';
    return `
      <div class="sp-user-dropdown-wrapper">
        <button type="button" class="sp-avatar-circle-btn" id="btn-user-avatar-menu" style="background: ${avatarBg}" aria-label="Menu tài khoản" aria-expanded="false" title="${escapeHtml(u.hoTen)}">
          ${escapeHtml(layChuCaiDau(u.hoTen))}
        </button>
        <div class="sp-user-dropdown-menu" id="menu-user-dropdown" style="display: none;">
          <div class="sp-user-dropdown-header">
            <div class="sp-dropdown-user-name">${escapeHtml(u.hoTen)}</div>
            <div class="sp-dropdown-user-role">${escapeHtml(layNhanVaiTro(u.vaiTro))}</div>
          </div>
          <div class="sp-dropdown-divider"></div>
          <a class="sp-dropdown-menu-item" href="${layTrangChinhTheoVaiTro(u.vaiTro)}">
            <span class="sp-menu-item-icon">${icons.layoutDashboard(16)}</span>
            <span>Bảng điều khiển</span>
          </a>
          ${
            u.vaiTro === 'HOC_SINH'
              ? `
            <a class="sp-dropdown-menu-item" href="/pages/hoc-sinh-tong-quan.html?tab=tab-tin-nhan">
              <span class="sp-menu-item-icon">${icons.messageCircle(16)}</span>
              <span>Tin nhắn</span>
            </a>
            <a class="sp-dropdown-menu-item" href="/pages/hoc-sinh-tong-quan.html?tab=tab-lich-hoc">
              <span class="sp-menu-item-icon">${icons.calendar(16)}</span>
              <span>Lịch học của tôi</span>
            </a>
            <a class="sp-dropdown-menu-item" href="/pages/hoc-sinh-tong-quan.html?tab=tab-yeu-thich">
              <span class="sp-menu-item-icon">${icons.heart(16)}</span>
              <span>Gia sư đã lưu</span>
            </a>
            <a class="sp-dropdown-menu-item" href="/pages/tro-thanh-gia-su.html">
              <span class="sp-menu-item-icon">${icons.briefcase(16)}</span>
              <span>Đăng tin dạy học</span>
            </a>
          `
              : u.vaiTro === 'GIA_SU'
                ? `
            <a class="sp-dropdown-menu-item" href="/pages/quan-ly-gia-su.html?tab=tab-tin-nhan">
              <span class="sp-menu-item-icon">${icons.messageCircle(16)}</span>
              <span>Tin nhắn học viên</span>
            </a>
            <a class="sp-dropdown-menu-item" href="/pages/quan-ly-gia-su.html?tab=tab-lich-ranh">
              <span class="sp-menu-item-icon">${icons.calendar(16)}</span>
              <span>Thời gian biểu &amp; Lịch dạy</span>
            </a>
            <a class="sp-dropdown-menu-item" href="/pages/quan-ly-gia-su.html">
              <span class="sp-menu-item-icon">${icons.briefcase(16)}</span>
              <span>Không gian Gia sư</span>
            </a>
          `
                : `
            <a class="sp-dropdown-menu-item" href="/pages/quan-tri.html">
              <span class="sp-menu-item-icon">${icons.shieldCheck(16)}</span>
              <span>Quản trị hệ thống</span>
            </a>
          `
          }
          <div class="sp-dropdown-divider"></div>
          <a class="sp-dropdown-menu-item" href="${
            u.vaiTro === 'HOC_SINH'
              ? '/pages/hoc-sinh-tong-quan.html?tab=tab-tai-khoan'
              : u.vaiTro === 'GIA_SU'
                ? '/pages/quan-ly-gia-su.html?tab=tab-tai-khoan'
                : '/pages/ho-so-tai-khoan.html'
          }">
            <span class="sp-menu-item-icon">${icons.user(16)}</span>
            <span>Tài khoản</span>
          </a>
          <button type="button" class="sp-dropdown-menu-item danger" id="btn-dang-xuat-dropdown">
            <span class="sp-menu-item-icon">${icons.logout(16)}</span>
            <span>Đăng xuất</span>
          </button>
        </div>
      </div>
    `;
  };

  if (!user) {
    // 1. KHÁCH CÔNG KHAI — Tối giản tinh tế kiểu Superprof: Logo + thanh tìm gọn (hiện khi cuộn / ở trang trong) + 3 nút tròn nhẹ
    centerHtml = formTimHeaderHtml(laTrangChu);
    rightHtml = `
      <a class="sp-icon-circle" href="/index.html#so-sanh-hinh-thuc" title="So sánh Học trực tuyến &amp; Tại nhà" aria-label="Cách học trực tuyến và tại nhà">
        ${icons.helpCircle(18)}
      </a>
      <a class="sp-pill-action" href="/pages/tro-thanh-gia-su.html">Dạy học</a>
      <a class="sp-pill-action primary" href="/pages/dang-nhap.html">Đăng nhập</a>
    `;
  } else {
    // 2. NGƯỜI DÙNG ĐÃ ĐĂNG NHẬP — Tinh gọn tối đa chuẩn Superprof (Không hiện tiền ví ở header, không nhét menu dàn ngang)
    centerHtml = formTimHeaderHtml(laTrangChu);
    rightHtml = `
      <a class="sp-icon-circle" href="/index.html#so-sanh-hinh-thuc" title="So sánh Học trực tuyến &amp; Tại nhà" aria-label="Cách học trực tuyến và tại nhà">
        ${icons.helpCircle(18)}
      </a>
      <a class="sp-icon-circle" href="${
        user.vaiTro === 'HOC_SINH' ? '/pages/hoc-sinh-tong-quan.html?tab=tab-yeu-thich' : '/pages/muc-yeu-thich.html'
      }" title="Gia sư đã lưu" aria-label="Gia sư yêu thích">
        ${icons.heart(18, false)}
      </a>
      ${userDropdownHtml(user)}
    `;
  }

  el.innerHTML = `
    <header class="${headerClass}" id="header-chinh">
      <div class="container header-noi-dung">
        <div class="header-trai">
          <div style="display: flex; align-items: center; gap: 0.5rem; flex-shrink: 0">
            <a class="thuong-hieu" href="${logoHref}" style="font-size: 1.32rem">
              <img src="/assets/img/icon-192.svg" alt="TutorHub" width="28" height="28" />
              <span>TutorHub</span>
            </a>
            ${portalChipHtml}
          </div>
          ${centerHtml}
        </div>
        <div class="header-phai">
          ${rightHtml}
        </div>
      </div>
    </header>
  `;

  const formTimHeader = el.querySelector('#form-tim-header');
  const inpTimHeader = el.querySelector('#inp-tim-header');
  const dropdownHeader = el.querySelector('#dropdown-tim-kiem-header');
  const btnMoAiHeader = el.querySelector('#btn-mo-ai-header');

  formTimHeader?.addEventListener('submit', (e) => {
    e.preventDefault();
    const tk = inpTimHeader?.value?.trim();
    const q = tk ? `?tuKhoa=${encodeURIComponent(tk)}` : '';
    window.location.href = `/pages/tim-kiem-gia-su.html${q}`;
  });

  if (inpTimHeader && dropdownHeader) {
    inpTimHeader.addEventListener('focus', () => {
      dropdownHeader.classList.add('open');
    });

    dropdownHeader.querySelectorAll('.sp-dropdown-item').forEach((item) => {
      item.addEventListener('click', () => {
        const mon = item.getAttribute('data-mon') || '';
        inpTimHeader.value = mon;
        dropdownHeader.classList.remove('open');
        window.location.href = `/pages/tim-kiem-gia-su.html?tuKhoa=${encodeURIComponent(mon)}`;
      });
    });

    btnMoAiHeader?.addEventListener('click', () => {
      dropdownHeader.classList.remove('open');
      const aiModalBtn = document.getElementById('btn-mo-ai-modal');
      if (aiModalBtn) {
        aiModalBtn.click();
      } else {
        window.location.href = '/index.html?openAi=true';
      }
    });

    document.addEventListener('click', (e) => {
      if (!formTimHeader?.contains(e.target) && !dropdownHeader?.contains(e.target)) {
        dropdownHeader.classList.remove('open');
      }
    });
  }

  // Hiện thanh tìm kiếm gọn trên Header khi cuộn qua Hero (chuẩn Superprof: chỉ khi cuộn qua hết tìm kiếm mới trôi hiện lên)
  if (laTrangChu) {
    const heroSearch = document.getElementById('form-tim-nhanh');
    const headerEl = el.querySelector('#header-chinh');
    const pillSearch = el.querySelector('#form-tim-header');
    if (headerEl && pillSearch) {
      const capNhatTrangThaiCuon = () => {
        const rect = heroSearch?.getBoundingClientRect();
        const daCuonQua = rect ? rect.bottom <= 68 : window.scrollY > 320;
        pillSearch.classList.toggle('is-hidden', !daCuonQua);
        headerEl.classList.toggle('header-on-hero', !daCuonQua);
        headerEl.classList.toggle('header-scrolled', daCuonQua);
      };
      window.addEventListener('scroll', () => requestAnimationFrame(capNhatTrangThaiCuon), { passive: true });
      capNhatTrangThaiCuon();
    }
  }

  const btnAvatar = el.querySelector('#btn-user-avatar-menu');
  const menuDropdown = el.querySelector('#menu-user-dropdown');
  const btnDangXuatDrop = el.querySelector('#btn-dang-xuat-dropdown');

  if (btnAvatar && menuDropdown) {
    btnAvatar.addEventListener('click', (e) => {
      e.stopPropagation();
      const dangMo = menuDropdown.style.display === 'block';
      menuDropdown.style.display = dangMo ? 'none' : 'block';
      btnAvatar.setAttribute('aria-expanded', String(!dangMo));
    });

    document.addEventListener('click', (e) => {
      if (!menuDropdown.contains(e.target) && !btnAvatar.contains(e.target)) {
        menuDropdown.style.display = 'none';
        btnAvatar.setAttribute('aria-expanded', 'false');
      }
    });
  }

  btnDangXuatDrop?.addEventListener('click', dangXuatHeThong);
  const btnDangXuat = el.querySelector('#btn-dang-xuat-header');
  btnDangXuat?.addEventListener('click', dangXuatHeThong);
}

export function renderFooter(selector = '#khung-footer') {
  const el = document.querySelector(selector);
  if (!el) return;

  const user = layNguoiDungHienTai();
  const linkTaiKhoan = !user
    ? '<a class="footer-link" href="/pages/dang-nhap.html">Đăng nhập / Đăng ký</a>'
    : `<a class="footer-link" href="${layTrangChinhTheoVaiTro(user.vaiTro)}">Không gian ${escapeHtml(layNhanVaiTro(user.vaiTro))}</a>`;

  el.innerHTML = `
    <footer class="footer-chung" aria-label="Giới thiệu và hỗ trợ TutorHub">
      <div class="container">
        <div class="footer-grid">
          <!-- Cột 1: Giới thiệu thương hiệu & Liên hệ hỗ trợ -->
          <div class="footer-brand-col">
            <a href="/index.html" class="thuong-hieu" style="display: inline-flex">
              <span class="thuong-hieu-bieu-tuong">${icons.graduationCap(17)}</span>
              <span>TutorHub</span>
            </a>
            <p class="footer-desc">
              Nền tảng kết nối học sinh, phụ huynh với đội ngũ gia sư 1-1 đã xác minh bằng cấp trên toàn quốc. Hỗ trợ học thử buổi đầu Online miễn phí và học kèm trực tiếp tại nhà linh hoạt, minh bạch.
            </p>
            <div class="footer-contact-list">
              <span class="footer-contact-pill">Tổng đài hỗ trợ: <strong>Đang cập nhật</strong> (08:00 – 21:30)</span>
              <a class="footer-contact-pill" href="mailto:s236894@nctu.edu.vn" style="text-decoration: none">
                Email hỗ trợ: <strong>s236894@nctu.edu.vn</strong>
              </a>
            </div>
          </div>

          <!-- Cột 2: Môn học phổ biến -->
          <div class="footer-nav-col">
            <h3 class="footer-col-title">Môn học phổ biến</h3>
            <ul class="footer-link-list">
              <li><a class="footer-link" href="/pages/tim-kiem-gia-su.html?tuKhoa=To%C3%A1n">Toán học &amp; Luyện thi THPT</a></li>
              <li><a class="footer-link" href="/pages/tim-kiem-gia-su.html?tuKhoa=Ti%E1%BA%BFng+Anh">Tiếng Anh &amp; Luyện thi IELTS</a></li>
              <li><a class="footer-link" href="/pages/tim-kiem-gia-su.html?tuKhoa=H%C3%B3a+h%E1%BB%8Dc">Hóa học &amp; Vật lý</a></li>
              <li><a class="footer-link" href="/pages/tim-kiem-gia-su.html?tuKhoa=Ng%E1%BB%AF+v%C4%83n">Ngữ văn &amp; Nghị luận xã hội</a></li>
              <li><a class="footer-link" href="/pages/tim-kiem-gia-su.html?tuKhoa=Tin+h%E1%BB%8Dc">Tin học &amp; Lập trình</a></li>
            </ul>
          </div>

          <!-- Cột 3: Khám phá & Dành cho Gia sư -->
          <div class="footer-nav-col">
            <h3 class="footer-col-title">Khám phá</h3>
            <ul class="footer-link-list">
              <li><a class="footer-link" href="/pages/tim-kiem-gia-su.html">Tìm kiếm gia sư 1-1</a></li>
              <li><a class="footer-link" href="/pages/tim-kiem-gia-su.html?coHocThu=true">Gia sư miễn phí buổi đầu</a></li>
              <li><a class="footer-link" href="/index.html#so-sanh-hinh-thuc">Học Online &amp; Học tại nhà</a></li>
              <li><a class="footer-link" href="/pages/tro-thanh-gia-su.html">Trở thành Gia sư TutorHub</a></li>
              <li>${linkTaiKhoan}</li>
            </ul>
          </div>

          <!-- Cột 4: Hỗ trợ & Cam kết -->
          <div class="footer-nav-col">
            <h3 class="footer-col-title">Trung tâm hỗ trợ</h3>
            <ul class="footer-link-list">
              <li><a class="footer-link" href="/index.html#ho-tro-faq">Câu hỏi cho Người học</a></li>
              <li><a class="footer-link" href="/index.html#ho-tro-faq">Câu hỏi cho Gia sư</a></li>
              <li><a class="footer-link" href="/index.html#quy-trinh-hoc">Hướng dẫn đặt lịch 3 bước</a></li>
              <li><a class="footer-link" href="/index.html#ho-tro-faq">Chính sách đổi &amp; hoàn hủy lịch</a></li>
              <li><a class="footer-link" href="mailto:s236894@nctu.edu.vn">Gửi email: s236894@nctu.edu.vn</a></li>
            </ul>
          </div>
        </div>

        <div class="footer-bottom">
          <div>© 2026 TutorHub Vietnam · Nền tảng kết nối gia sư 1-1 trực tuyến &amp; trực tiếp tại nhà</div>
          <div class="footer-trust-tags">
            <span>✓ Bằng cấp xác minh</span>
            <span>·</span>
            <span>✓ Học thử Online 0 ₫</span>
            <span>·</span>
            <span>✓ Bảo vệ học phí an toàn</span>
          </div>
        </div>
      </div>
    </footer>
  `;
  ganThanhDieuHuongDayMobile(user);
}

export function taoThanhDieuHuongDayMobileHtml(user = layNguoiDungHienTai()) {
  const path = window.location.pathname || '';
  const search = window.location.search || '';
  const params = new URLSearchParams(search);
  // Trên trang Chi tiết Hồ sơ Gia sư và Phòng học trực tuyến đã có thanh công cụ đáy chuyên biệt
  if (path.includes('ho-so-gia-su') || path.includes('phong-hoc')) {
    return '';
  }

  const tabDangMo =
    document.querySelector('.studio-nav-btn[data-tab].active, .admin-nav-btn[data-tab].active')?.dataset?.tab ||
    params.get('tab') ||
    '';
  const cheDoLichDangMo =
    document.body?.dataset?.cheDoLich || params.get('cheDo') || 'lich';

  const laActive = (matchPath) => {
    if (matchPath === '/index.html') {
      return path === '/' || path.endsWith('/index.html') ? 'active' : '';
    }
    return path.includes(matchPath.replace(/\.html$/, '')) ? 'active' : '';
  };

  let items = [];
  let meMenu = null;

  if (!user) {
    // 4 mục gọn gàng, gộp Tìm gia sư & Bản đồ gần tôi thành 1
    items = [
      { href: '/index.html', icon: icons.home(19), label: 'Trang chủ', active: laActive('/index.html') },
      { href: '/pages/tim-kiem-gia-su.html', icon: icons.search(19), label: 'Tìm gia sư', active: laActive('tim-kiem-gia-su') },
      { href: '/pages/tro-thanh-gia-su.html', icon: icons.graduationCap(19), label: 'Dạy học', active: laActive('tro-thanh-gia-su') },
      { href: '/pages/dang-nhap.html', icon: icons.user(19), label: 'Đăng nhập', active: laActive('dang-nhap') },
    ];
  } else if (user.vaiTro === 'HOC_SINH') {
    const oCongHs = path.includes('hoc-sinh-tong-quan');
    const dangOLichHoc = oCongHs && tabDangMo === 'tab-lich-hoc' && cheDoLichDangMo !== 'don-hang';
    const dangOTinNhan = oCongHs && tabDangMo === 'tab-tin-nhan';
    const dangOCuaToi = oCongHs && !dangOLichHoc && !dangOTinNhan;

    items = [
      { href: '/index.html', icon: icons.home(19), label: 'Trang chủ', active: laActive('/index.html') },
      { href: '/pages/tim-kiem-gia-su.html', icon: icons.search(19), label: 'Tìm gia sư', active: laActive('tim-kiem-gia-su') },
      {
        href: '/pages/hoc-sinh-tong-quan.html?tab=tab-lich-hoc&cheDo=lich',
        tab: 'tab-lich-hoc',
        cheDo: 'lich',
        icon: icons.calendar(19),
        label: 'Lịch học',
        active: dangOLichHoc ? 'active' : '',
      },
      {
        href: '/pages/hoc-sinh-tong-quan.html?tab=tab-tin-nhan',
        tab: 'tab-tin-nhan',
        icon: icons.messageCircle(19),
        label: 'Tin nhắn',
        active: dangOTinNhan ? 'active' : '',
      },
    ];

    meMenu = {
      label: 'Của tôi',
      active: dangOCuaToi ? 'active' : '',
      name: user.hoTen || 'Học sinh',
      role: 'Tài khoản Học sinh',
      links: [
        {
          href: '/pages/hoc-sinh-tong-quan.html?tab=tab-tong-quan',
          tab: 'tab-tong-quan',
          icon: icons.layoutDashboard(16),
          label: 'Tổng quan học tập',
          active: oCongHs && (tabDangMo === 'tab-tong-quan' || !tabDangMo),
        },
        {
          href: '/pages/hoc-sinh-tong-quan.html?tab=tab-yeu-thich',
          tab: 'tab-yeu-thich',
          icon: icons.heart(16, false),
          label: 'Gia sư đã lưu',
          active: oCongHs && tabDangMo === 'tab-yeu-thich',
        },
        {
          href: '/pages/hoc-sinh-tong-quan.html?tab=tab-lich-hoc&cheDo=don-hang',
          tab: 'tab-lich-hoc',
          cheDo: 'don-hang',
          icon: icons.fileText ? icons.fileText(16) : icons.list(16),
          label: 'Lịch sử đơn & Học phí',
          active: oCongHs && tabDangMo === 'tab-lich-hoc' && cheDoLichDangMo === 'don-hang',
        },
        {
          href: '/pages/hoc-sinh-tong-quan.html?tab=tab-tai-khoan',
          tab: 'tab-tai-khoan',
          icon: icons.wallet(16),
          label: 'Ví học tập & Cài đặt',
          active: oCongHs && tabDangMo === 'tab-tai-khoan',
        },
        { divider: true },
        {
          href: '/pages/tro-thanh-gia-su.html',
          icon: icons.graduationCap(16),
          label: 'Đăng ký làm Gia sư',
        },
        {
          action: 'logout',
          icon: icons.logout(16),
          label: 'Đăng xuất',
          danger: true,
        },
      ],
    };
  } else if (user.vaiTro === 'GIA_SU') {
    const oCongGs = path.includes('quan-ly-gia-su');
    const tabGs = tabDangMo || 'tab-tong-quan';
    const dangOCuaToiGs = oCongGs && !['tab-tong-quan', 'tab-lich-ranh', 'tab-tin-nhan'].includes(tabGs);

    items = [
      {
        href: '/pages/quan-ly-gia-su.html?tab=tab-tong-quan',
        tab: 'tab-tong-quan',
        icon: icons.layoutDashboard(19),
        label: 'Tổng quan',
        active: oCongGs && tabGs === 'tab-tong-quan' ? 'active' : '',
      },
      {
        href: '/pages/quan-ly-gia-su.html?tab=tab-lich-ranh',
        tab: 'tab-lich-ranh',
        icon: icons.calendar(19),
        label: 'Lịch dạy',
        active: oCongGs && tabGs === 'tab-lich-ranh' ? 'active' : '',
      },
      {
        href: '/pages/quan-ly-gia-su.html?tab=tab-tin-nhan',
        tab: 'tab-tin-nhan',
        icon: icons.messageCircle(19),
        label: 'Tin nhắn',
        active: oCongGs && tabGs === 'tab-tin-nhan' ? 'active' : '',
      },
    ];

    meMenu = {
      label: 'Của tôi',
      active: dangOCuaToiGs ? 'active' : '',
      name: user.hoTen || 'Gia sư',
      role: 'Cổng Gia sư',
      links: [
        {
          href: '/pages/quan-ly-gia-su.html?tab=tab-vi-giao-dich',
          tab: 'tab-vi-giao-dich',
          icon: icons.wallet(16),
          label: 'Ví & Thu nhập',
          active: oCongGs && tabGs === 'tab-vi-giao-dich',
        },
        {
          href: '/pages/quan-ly-gia-su.html?tab=tab-ho-so',
          tab: 'tab-ho-so',
          icon: icons.user(16),
          label: 'Hồ sơ & Hình thức dạy',
          active: oCongGs && tabGs === 'tab-ho-so',
        },
        {
          href: '/pages/quan-ly-gia-su.html?tab=tab-mon-hoc',
          tab: 'tab-mon-hoc',
          icon: icons.bookOpen(16),
          label: 'Môn dạy & Học phí',
          active: oCongGs && tabGs === 'tab-mon-hoc',
        },
        {
          href: '/pages/quan-ly-gia-su.html?tab=tab-giay-to',
          tab: 'tab-giay-to',
          icon: icons.shieldCheck(16),
          label: 'Giấy tờ & Bằng cấp',
          active: oCongGs && tabGs === 'tab-giay-to',
        },
        { divider: true },
        { href: '/index.html', icon: icons.home(16), label: 'Về Trang chủ' },
        { action: 'logout', icon: icons.logout(16), label: 'Đăng xuất', danger: true },
      ],
    };
  } else {
    const oCongQt = path.includes('quan-tri');
    items = [
      { href: '/pages/quan-tri.html', icon: icons.shieldCheck(19), label: 'Quản trị', active: laActive('quan-tri') },
      { href: '/pages/tim-kiem-gia-su.html', icon: icons.search(19), label: 'Gia sư', active: laActive('tim-kiem-gia-su') },
      { href: '/index.html', icon: icons.home(19), label: 'Trang chủ', active: laActive('/index.html') },
    ];
    meMenu = {
      label: 'Của tôi',
      active: laActive('ho-so-tai-khoan'),
      name: user.hoTen || 'Quản trị viên',
      role: 'Quản trị hệ thống',
      links: [
        ...(oCongQt
          ? [...document.querySelectorAll('.admin-nav-btn[data-tab]')].map((b) => ({
              href: '/pages/quan-tri.html',
              tab: b.dataset.tab,
              icon: icons.list(16),
              label: (b.textContent || '').replace(/\d+$/, '').trim(),
              active: b.classList.contains('active'),
            }))
          : [{ href: '/pages/quan-tri.html', icon: icons.shieldCheck(16), label: 'Bảng điều khiển quản trị' }]),
        { divider: true },
        { href: '/pages/ho-so-tai-khoan.html', icon: icons.user(16), label: 'Tài khoản của tôi' },
        { action: 'logout', icon: icons.logout(16), label: 'Đăng xuất', danger: true },
      ],
    };
  }

  const tongCot = items.length + (meMenu ? 1 : 0);

  return `
    <div class="th-mobile-me-backdrop" id="th-mobile-me-backdrop"></div>
    <nav class="th-mobile-bottom-nav" id="th-mobile-bottom-nav" style="--th-mob-cols: ${tongCot}" aria-label="Điều hướng nhanh trên điện thoại">
      ${items
        .map(
          (it) => `
        <a href="${it.href}" class="th-mobile-nav-item ${it.active}" ${it.tab ? `data-portal-tab="${it.tab}"` : ''} ${it.cheDo ? `data-portal-che-do="${it.cheDo}"` : ''}>
          <span class="th-mobile-nav-icon">${it.icon}</span>
          <span class="th-mobile-nav-label">${escapeHtml(it.label)}</span>
        </a>
      `
        )
        .join('')}
      ${
        meMenu
          ? `
        <div class="th-mobile-me-wrap" id="th-mobile-me-wrap">
          <button type="button" class="th-mobile-nav-item th-mobile-me-trigger ${meMenu.active}" id="btn-th-mobile-me" aria-haspopup="true" aria-expanded="false">
            <span class="th-mobile-nav-icon">${icons.user(19)}</span>
            <span class="th-mobile-nav-label">${escapeHtml(meMenu.label)} ▴</span>
          </button>
          <div class="th-mobile-me-menu" id="th-mobile-me-menu" role="menu" aria-label="Tùy chọn tài khoản cá nhân">
            <div class="th-mobile-me-head">
              <span class="th-mobile-me-avatar">${escapeHtml(layChuCaiDau(meMenu.name))}</span>
              <div style="min-width: 0; flex: 1">
                <div class="th-mobile-me-name">${escapeHtml(meMenu.name)}</div>
                <div class="th-mobile-me-role">${escapeHtml(meMenu.role)}</div>
              </div>
            </div>
            <div class="th-mobile-me-list">
              ${meMenu.links
                .map((lk) => {
                  if (lk.divider) return '<div class="th-mobile-me-divider"></div>';
                  if (lk.action === 'logout') {
                    return `
                      <button type="button" class="th-mobile-me-item danger" data-mobile-action="logout" role="menuitem">
                        <span class="th-mobile-me-ico">${lk.icon}</span>
                        <span>${escapeHtml(lk.label)}</span>
                      </button>
                    `;
                  }
                  return `
                    <a href="${lk.href}" class="th-mobile-me-item ${lk.active ? 'active' : ''}" ${lk.tab ? `data-portal-tab="${lk.tab}"` : ''} ${lk.cheDo ? `data-portal-che-do="${lk.cheDo}"` : ''} role="menuitem">
                      <span class="th-mobile-me-ico">${lk.icon}</span>
                      <span>${escapeHtml(lk.label)}</span>
                    </a>
                  `;
                })
                .join('')}
            </div>
          </div>
        </div>
      `
          : ''
      }
    </nav>
  `;
}

export function ganThanhDieuHuongDayMobile(user = layNguoiDungHienTai()) {
  if (typeof document === 'undefined' || !document.body) return;
  // Gỡ bỏ thanh pill cũ dưới header nếu còn sót lại
  document.getElementById('portal-mobile-subtabs')?.remove();

  const html = taoThanhDieuHuongDayMobileHtml(user);
  document.querySelectorAll('.th-mobile-bottom-nav, .th-mobile-me-backdrop').forEach((el) => el.remove());
  if (!html) return;
  document.body.insertAdjacentHTML('beforeend', html);
  const navEl = document.getElementById('th-mobile-bottom-nav');
  if (!navEl) return;

  const meWrap = document.getElementById('th-mobile-me-wrap');
  const btnMe = document.getElementById('btn-th-mobile-me');
  const backdrop = document.getElementById('th-mobile-me-backdrop');
  let hoverCloseTimer = null;

  const setMeOpen = (open) => {
    if (!meWrap) return;
    meWrap.classList.toggle('open', open);
    backdrop?.classList.toggle('open', open);
    btnMe?.setAttribute('aria-expanded', open ? 'true' : 'false');
  };

  if (meWrap && btnMe) {
    // Mở menu khi chạm/click (điện thoại cảm ứng)
    btnMe.addEventListener('click', (e) => {
      e.preventDefault();
      e.stopPropagation();
      setMeOpen(!meWrap.classList.contains('open'));
    });

    // Mở menu ngay khi đưa trỏ chuột vào (PC / DevTools / Máy tính bảng có trỏ)
    meWrap.addEventListener('mouseenter', () => {
      if (hoverCloseTimer) {
        clearTimeout(hoverCloseTimer);
        hoverCloseTimer = null;
      }
      setMeOpen(true);
    });

    meWrap.addEventListener('mouseleave', () => {
      hoverCloseTimer = setTimeout(() => setMeOpen(false), 180);
    });

    backdrop?.addEventListener('click', () => setMeOpen(false));
  }

  navEl.querySelectorAll('[data-mobile-action="logout"]').forEach((btn) => {
    btn.addEventListener('click', () => {
      setMeOpen(false);
      dangXuatHeThong();
    });
  });

  navEl.querySelectorAll('a[data-portal-tab]').forEach((a) => {
    a.addEventListener('click', (e) => {
      const targetUrl = new URL(a.href, window.location.origin);
      const samePage =
        window.location.pathname.replace(/\.html$/, '') === targetUrl.pathname.replace(/\.html$/, '');
      const tabId = a.dataset.portalTab;
      const cheDo = a.dataset.portalCheDo || '';
      const sideBtn = tabId
        ? document.querySelector(`.studio-nav-btn[data-tab="${tabId}"], .admin-nav-btn[data-tab="${tabId}"]`)
        : null;
      setMeOpen(false);
      if (samePage && sideBtn) {
        e.preventDefault();
        if (cheDo) {
          document.body.dataset.cheDoLich = cheDo;
          window.dispatchEvent(new CustomEvent('tutorhub:doi-che-do-lich', { detail: { cheDo } }));
        }
        sideBtn.click();
        window.scrollTo({ top: 0, behavior: 'smooth' });
        ganThanhDieuHuongDayMobile(user);
      }
    });
  });
}

let daKhoiTaoObserverPortalMobile = false;

export function khoiTaoThanhTabNgangMobilePortal() {
  if (typeof document === 'undefined') return;
  document.getElementById('portal-mobile-subtabs')?.remove();
  const appShell = document.querySelector('.app-shell');
  const sideBtns = [...(appShell?.querySelectorAll('.studio-nav-btn[data-tab], .admin-nav-btn[data-tab]') || [])];
  if (!appShell || sideBtns.length === 0) return;

  if (!daKhoiTaoObserverPortalMobile) {
    daKhoiTaoObserverPortalMobile = true;
    const obs = new MutationObserver(() => {
      ganThanhDieuHuongDayMobile();
    });
    sideBtns.forEach((b) => obs.observe(b, { attributes: true, attributeFilter: ['class'] }));
  }
}

if (typeof document !== 'undefined') {
  const khoiChayMobileUi = () => {
    ganThanhDieuHuongDayMobile();
    khoiTaoThanhTabNgangMobilePortal();
  };
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', khoiChayMobileUi);
  } else {
    khoiChayMobileUi();
  }
}

export function renderCardGiaSu(gs, { choPhepYeuThich = true } = {}) {
  const dsTenMon = [...new Set((gs.dsMonDay || []).map((m) => m.tenMonHoc))];
  const giaHienThi = `${dinhDangTienVnd(gs.giaThapNhat)}/giờ`;

  const coTrucTuyen = gs.coDayTrucTuyen ?? (gs.hinhThucDay || []).includes('TRUC_TUYEN');
  const coTrucTiep = gs.coDayTrucTiep ?? (gs.hinhThucDay || []).includes('TRUC_TIEP');

  let dongViTriCheDo = 'Toàn quốc (học trực tuyến)';
  if (typeof gs._khoangCachKm === 'number') {
    const denNha = gs._sanSangDenNha ? ' · ✓ Đến tận nhà' : '';
    dongViTriCheDo = `Cách bạn ${gs._khoangCachKm.toFixed(1)} km · ${gs.khuVuc || 'Tại nhà'}${denNha}`;
  } else if (coTrucTiep && coTrucTuyen) {
    dongViTriCheDo = `${gs.khuVuc || 'Tại nhà'} (trực tiếp & trực tuyến)`;
  } else if (coTrucTiep) {
    dongViTriCheDo = `${gs.khuVuc || 'Tại nhà'} (trực tiếp · ${gs.banKinhPhucVuKm || 10} km)`;
  }

  const moUuDaiBuoiDau = Boolean(gs.coHocThu) && coTrucTuyen;
  const tagHocThuHtml = moUuDaiBuoiDau
    ? '<span class="tag-trial">Miễn phí buổi đầu Online</span>'
    : `<span class="badge badge-neutral">${gs.soNamKinhNghiem} năm KN</span>`;

  const anhUrl = layAnhGiaSu(gs);

  return `
    <article class="sp-tutor-card" data-gia-su-id="${gs.id}">
      ${
        choPhepYeuThich
          ? `<button type="button" class="sp-heart-circle btn-yeu-thich" data-gia-su-id="${gs.id}" data-da-yeu-thich="${gs.daYeuThich ? 'true' : 'false'}" aria-label="${gs.daYeuThich ? 'Bỏ lưu yêu thích' : 'Lưu yêu thích'}">
              ${icons.heart(17, Boolean(gs.daYeuThich))}
            </button>`
          : ''
      }
      <a class="sp-tutor-link-cover" href="/pages/ho-so-gia-su.html?id=${gs.id}">
        <div class="sp-tutor-media">
          <img
            class="sp-tutor-photo"
            src="${escapeHtml(anhUrl)}"
            alt="${escapeHtml(gs.hoTen)}"
            width="340"
            height="340"
            loading="lazy"
          />
          <div class="sp-tutor-scrim"></div>
          ${
            gs.daXacMinh
              ? `<span class="sp-verified-pill">${icons.checkCircle(12)} <span class="sp-verified-text">Đã xác minh</span></span>`
              : ''
          }
          <div class="sp-tutor-overlay">
            <h3 class="sp-tutor-name">${escapeHtml(gs.hoTen)}</h3>
            <div class="sp-tutor-loc">${escapeHtml(dongViTriCheDo)}</div>
          </div>
        </div>

        <div class="sp-tutor-body">
          <!-- Đầu thẻ dành riêng cho giao diện Thẻ Ngang trên Điện thoại dọc (ẩn trên PC) -->
          <div class="sp-tutor-mobile-head">
            <div class="sp-tutor-mobile-name-row">
              <span class="sp-tutor-mobile-name">${escapeHtml(gs.hoTen)}</span>
              ${gs.daXacMinh ? `<span class="sp-tutor-mobile-verified" title="Đã xác minh bằng cấp">${icons.checkCircle(13)}</span>` : ''}
            </div>
            <div class="sp-tutor-mobile-loc">${escapeHtml(dongViTriCheDo)}</div>
          </div>

          <div class="sp-tutor-meta-row">
            <span class="sp-tutor-rating">
              ${
                gs.soLuotDanhGia > 0
                  ? `<span style="color: var(--th-amber); display: inline-flex">${icons.star(14)}</span>
                     <span>${gs.diemDanhGiaTrungBinh.toFixed(1)}</span>
                     <span class="sp-rating-count" style="font-weight: 500; color: var(--th-text-muted)">(${gs.soLuotDanhGia} đánh giá)</span>`
                  : `<span style="font-weight: 600; color: var(--th-text-muted)">${gs.soNamKinhNghiem} năm KN</span>`
              }
            </span>
            <div class="sp-tutor-subject-tags" style="display: flex; gap: 0.3rem; flex-shrink: 0; flex-wrap: wrap">
              ${dsTenMon.slice(0, 2).map((m) => `<span class="tag-subject">${escapeHtml(m)}</span>`).join('')}
            </div>
          </div>

          <p class="sp-tutor-pitch">${escapeHtml(gs.tieuDe)}</p>

          <div class="sp-tutor-price-row">
            <span class="sp-tutor-price">${giaHienThi}</span>
            ${tagHocThuHtml}
          </div>
        </div>
      </a>
    </article>
  `;
}

export function taoSkeletonCardGiaSu(soLuong = 3) {
  return Array.from({ length: soLuong })
    .map(
      () => `
      <div class="sk-sp-tutor-card" aria-hidden="true">
        <div class="sk-block sk-sp-tutor-media"></div>
        <div class="sk-sp-tutor-body">
          <div style="display: flex; justify-content: space-between; align-items: center; gap: 8px">
            <span class="sk-line" style="height: 15px; width: 42%"></span>
            <span class="sk-block" style="height: 24px; width: 72px"></span>
          </div>
          <div>
            <span class="sk-line" style="height: 14px; width: 94%; margin-top: 6px"></span>
            <span class="sk-line" style="height: 14px; width: 72%; margin-top: 7px"></span>
          </div>
          <div style="display: flex; justify-content: space-between; align-items: center; padding-top: 6px">
            <span class="sk-line" style="height: 18px; width: 108px"></span>
            <span class="sk-block" style="height: 24px; width: 112px"></span>
          </div>
        </div>
      </div>
    `
    )
    .join('');
}

export function taoSkeletonDongLich(soLuong = 3) {
  return Array.from({ length: soLuong })
    .map(
      () => `
      <div class="sk-lesson-row" aria-hidden="true">
        <div style="flex: 1; min-width: 0">
          <span class="sk-line" style="height: 15px; width: 58%"></span>
          <span class="sk-line" style="height: 12px; width: 76%; margin-top: 10px"></span>
        </div>
        <div style="width: 110px; display: flex; flex-direction: column; align-items: flex-end; gap: 6px">
          <span class="sk-line" style="height: 15px; width: 82px"></span>
          <span class="sk-line" style="height: 20px; width: 96px; border-radius: 9999px"></span>
        </div>
      </div>
    `
    )
    .join('');
}

export function escapeHtml(str) {
  if (str === null || str === undefined) return '';
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}
