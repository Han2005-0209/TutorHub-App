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

export function layAnhGiaSu(gs) {
  if (gs?.anhDaiDienUrl) return gs.anhDaiDienUrl;
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
    return null;
  }
  try {
    const res = await api.get('/nguoi-dung/toi');
    if (res?.duLieu) {
      luuNguoiDungHienTai(res.duLieu);
      return res.duLieu;
    }
  } catch {
    // Nếu 401 đã được api.js xóa token
  }
  return layNguoiDungHienTai();
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
              ? `<span class="sp-verified-pill">${icons.checkCircle(12)} Đã xác minh</span>`
              : ''
          }
          <div class="sp-tutor-overlay">
            <h3 class="sp-tutor-name">${escapeHtml(gs.hoTen)}</h3>
            <div class="sp-tutor-loc">${escapeHtml(dongViTriCheDo)}</div>
          </div>
        </div>

        <div class="sp-tutor-body">
          <div class="sp-tutor-meta-row">
            <span class="sp-tutor-rating">
              ${
                gs.soLuotDanhGia > 0
                  ? `<span style="color: var(--th-amber); display: inline-flex">${icons.star(14)}</span>
                     <span>${gs.diemDanhGiaTrungBinh.toFixed(1)}</span>
                     <span style="font-weight: 500; color: var(--th-text-muted)">(${gs.soLuotDanhGia} đánh giá)</span>`
                  : `<span style="font-weight: 600; color: var(--th-text-muted)">${gs.soNamKinhNghiem} năm kinh nghiệm</span>`
              }
            </span>
            <div style="display: flex; gap: 0.3rem; flex-shrink: 0">
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
