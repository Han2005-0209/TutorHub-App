/**
 * Hệ thống thông báo Toast góc trên phải (tự ẩn sau 5 giây, tạm dừng khi di chuột)
 * và hộp thoại Xác nhận / Nhập lý do chuẩn giao diện TutorHub (thay thế hoàn toàn alert/confirm/prompt).
 */

const ID_CONTAINER_TOAST = 'th-toast-stack';
const ID_STYLE_FALLBACK = 'th-ui-feedback-style';

function damBaoStyleDuPhong() {
  if (document.getElementById(ID_STYLE_FALLBACK)) return;
  const style = document.createElement('style');
  style.id = ID_STYLE_FALLBACK;
  style.textContent = `
    .th-toast-stack {
      position: fixed;
      top: 18px;
      right: 18px;
      z-index: 10050;
      display: flex;
      flex-direction: column;
      gap: 0.6rem;
      width: min(390px, calc(100vw - 24px));
      pointer-events: none;
    }
    .th-toast-item {
      pointer-events: auto;
      position: relative;
      overflow: hidden;
      display: flex;
      align-items: flex-start;
      gap: 0.7rem;
      padding: 0.85rem 0.95rem 0.95rem;
      border-radius: 14px;
      background: #ffffff;
      border: 1px solid #e2e8f0;
      box-shadow: 0 14px 34px rgba(15, 23, 42, 0.16), 0 2px 6px rgba(15, 23, 42, 0.06);
      color: #0f172a;
      font-family: inherit;
      font-size: 0.85rem;
      line-height: 1.48;
      transform: translateX(28px);
      opacity: 0;
      transition: transform 0.22s cubic-bezier(0.22, 1, 0.36, 1), opacity 0.22s ease;
    }
    .th-toast-item.show {
      transform: translateX(0);
      opacity: 1;
    }
    .th-toast-item.hiding {
      transform: translateX(24px);
      opacity: 0;
    }
    .th-toast-item.tone-success { border-left: 4px solid #10b981; }
    .th-toast-item.tone-danger  { border-left: 4px solid #ef4444; }
    .th-toast-item.tone-warning { border-left: 4px solid #f59e0b; }
    .th-toast-item.tone-info    { border-left: 4px solid #3b82f6; }
    .th-toast-icon {
      flex-shrink: 0;
      width: 24px;
      height: 24px;
      border-radius: 999px;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      font-size: 0.8rem;
      font-weight: 800;
      margin-top: 1px;
    }
    .tone-success .th-toast-icon { background: #dcfce7; color: #15803d; }
    .tone-danger  .th-toast-icon { background: #fee2e2; color: #b91c1c; }
    .tone-warning .th-toast-icon { background: #fef3c7; color: #b45309; }
    .tone-info    .th-toast-icon { background: #dbeafe; color: #1d4ed8; }
    .th-toast-body {
      flex: 1;
      min-width: 0;
      word-break: break-word;
    }
    .th-toast-title {
      font-weight: 800;
      font-size: 0.82rem;
      margin-bottom: 2px;
      color: #0f172a;
    }
    .th-toast-msg {
      color: #334155;
      font-weight: 500;
    }
    .th-toast-close {
      flex-shrink: 0;
      border: none;
      background: transparent;
      color: #94a3b8;
      font-size: 1.15rem;
      line-height: 1;
      padding: 0.1rem 0.25rem;
      cursor: pointer;
      border-radius: 6px;
    }
    .th-toast-close:hover {
      background: #f1f5f9;
      color: #0f172a;
    }
    .th-toast-progress {
      position: absolute;
      left: 0;
      bottom: 0;
      height: 3px;
      width: 100%;
      transform-origin: left center;
      animation-name: thToastShrink;
      animation-timing-function: linear;
      animation-fill-mode: forwards;
    }
    .tone-success .th-toast-progress { background: #10b981; }
    .tone-danger  .th-toast-progress { background: #ef4444; }
    .tone-warning .th-toast-progress { background: #f59e0b; }
    .tone-info    .th-toast-progress { background: #3b82f6; }
    .th-toast-item:hover .th-toast-progress {
      animation-play-state: paused;
    }
    @keyframes thToastShrink {
      from { transform: scaleX(1); }
      to   { transform: scaleX(0); }
    }
    .th-dialog-backdrop {
      position: fixed;
      inset: 0;
      z-index: 10060;
      background: rgba(15, 23, 42, 0.56);
      backdrop-filter: blur(4px);
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 1rem;
      animation: thDialogFadeIn 0.16s ease;
    }
    @keyframes thDialogFadeIn {
      from { opacity: 0; }
      to   { opacity: 1; }
    }
    .th-dialog-card {
      width: 100%;
      max-width: 450px;
      background: #ffffff;
      border-radius: 18px;
      border: 1px solid #e2e8f0;
      box-shadow: 0 24px 60px rgba(15, 23, 42, 0.26);
      padding: 1.35rem 1.45rem;
      color: #0f172a;
      font-family: inherit;
    }
    .th-dialog-head {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 0.75rem;
      margin-bottom: 0.65rem;
    }
    .th-dialog-title {
      margin: 0;
      font-size: 1.04rem;
      font-weight: 800;
      color: #0f172a;
    }
    .th-dialog-desc {
      margin: 0 0 0.9rem;
      font-size: 0.86rem;
      line-height: 1.55;
      color: #475569;
      white-space: pre-line;
    }
    .th-dialog-chips {
      display: flex;
      flex-wrap: wrap;
      gap: 0.35rem;
      margin-bottom: 0.65rem;
    }
    .th-dialog-chip {
      border: 1px solid #dbeafe;
      background: #f8fbff;
      color: #1e40af;
      border-radius: 999px;
      padding: 0.24rem 0.65rem;
      font-size: 0.75rem;
      font-weight: 600;
      cursor: pointer;
      font-family: inherit;
    }
    .th-dialog-chip:hover {
      background: #dbeafe;
    }
    .th-dialog-input {
      width: 100%;
      box-sizing: border-box;
      border: 1.5px solid #cbd5e1;
      border-radius: 11px;
      padding: 0.65rem 0.8rem;
      font-family: inherit;
      font-size: 0.86rem;
      color: #0f172a;
      resize: vertical;
      min-height: 78px;
      outline: none;
    }
    .th-dialog-input:focus {
      border-color: #2563eb;
      box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.14);
    }
    .th-dialog-err {
      margin-top: 0.35rem;
      font-size: 0.78rem;
      font-weight: 600;
      color: #dc2626;
      min-height: 1em;
    }
    .th-dialog-actions {
      display: flex;
      justify-content: flex-end;
      gap: 0.5rem;
      margin-top: 1.05rem;
    }
    .th-dialog-btn {
      border-radius: 10px;
      padding: 0.52rem 1rem;
      font-size: 0.84rem;
      font-weight: 700;
      cursor: pointer;
      font-family: inherit;
      border: 1px solid transparent;
      transition: background-color 0.14s ease, border-color 0.14s ease, opacity 0.14s ease;
    }
    .th-dialog-btn.cancel {
      background: #f1f5f9;
      color: #334155;
      border-color: #e2e8f0;
    }
    .th-dialog-btn.cancel:hover {
      background: #e2e8f0;
    }
    .th-dialog-btn.primary {
      background: #2563eb;
      color: #ffffff;
    }
    .th-dialog-btn.primary:hover {
      background: #1d4ed8;
    }
    .th-dialog-btn.danger {
      background: #dc2626;
      color: #ffffff;
    }
    .th-dialog-btn.danger:hover {
      background: #b91c1c;
    }
    @media (max-width: 640px) {
      .th-toast-stack {
        top: 12px;
        right: 12px;
        left: 12px;
        width: auto;
      }
    }
  `;
  document.head.appendChild(style);
}

function layKhungToast() {
  damBaoStyleDuPhong();
  let stack = document.getElementById(ID_CONTAINER_TOAST);
  if (!stack) {
    stack = document.createElement('div');
    stack.id = ID_CONTAINER_TOAST;
    stack.className = 'th-toast-stack';
    stack.setAttribute('aria-live', 'polite');
    stack.setAttribute('aria-atomic', 'false');
    document.body.appendChild(stack);
  }
  return stack;
}

function chuanHoaLoaiToast(loai) {
  const s = String(loai || 'info').toLowerCase();
  if (s === 'success' || s === 'ok') return 'success';
  if (s === 'danger' || s === 'error' || s === 'loi') return 'danger';
  if (s === 'warning' || s === 'warn') return 'warning';
  return 'info';
}

/**
 * Hiển thị thông báo Toast góc trên phải, tự động ẩn sau 5 giây (tạm dừng khi di chuột vào).
 * @param {string} noiDung
 * @param {'success'|'danger'|'warning'|'info'} [loai='info']
 * @param {number} [thoiGianMs=5000]
 */
export function hienToast(noiDung, loai = 'info', thoiGianMs = 5000) {
  const text = String(noiDung ?? '').trim();
  if (!text) return;

  const tone = chuanHoaLoaiToast(loai);
  const stack = layKhungToast();

  // Giới hạn tối đa 4 toast cùng lúc để không che màn hình
  while (stack.children.length >= 4) {
    stack.firstElementChild?.remove();
  }

  const metaTheoLoai = {
    success: { icon: '✓', title: 'Thành công' },
    danger: { icon: '!', title: 'Có lỗi xảy ra' },
    warning: { icon: '!', title: 'Lưu ý' },
    info: { icon: 'i', title: 'Thông báo' },
  };
  const meta = metaTheoLoai[tone];

  const el = document.createElement('div');
  el.className = `th-toast-item tone-${tone}`;
  el.setAttribute('role', tone === 'danger' ? 'alert' : 'status');

  const iconEl = document.createElement('span');
  iconEl.className = 'th-toast-icon';
  iconEl.setAttribute('aria-hidden', 'true');
  iconEl.textContent = meta.icon;

  const bodyEl = document.createElement('div');
  bodyEl.className = 'th-toast-body';
  const titleEl = document.createElement('div');
  titleEl.className = 'th-toast-title';
  titleEl.textContent = meta.title;
  const msgEl = document.createElement('div');
  msgEl.className = 'th-toast-msg';
  msgEl.textContent = text;
  bodyEl.appendChild(titleEl);
  bodyEl.appendChild(msgEl);

  const btnClose = document.createElement('button');
  btnClose.type = 'button';
  btnClose.className = 'th-toast-close';
  btnClose.setAttribute('aria-label', 'Đóng thông báo');
  btnClose.textContent = '×';

  const progressEl = document.createElement('div');
  progressEl.className = 'th-toast-progress';
  progressEl.style.animationDuration = `${thoiGianMs}ms`;

  el.appendChild(iconEl);
  el.appendChild(bodyEl);
  el.appendChild(btnClose);
  el.appendChild(progressEl);
  stack.appendChild(el);

  requestAnimationFrame(() => el.classList.add('show'));

  let conLaiMs = thoiGianMs;
  let batDauLuc = Date.now();
  let timer = null;
  let daDong = false;

  const dong = () => {
    if (daDong) return;
    daDong = true;
    clearTimeout(timer);
    el.classList.remove('show');
    el.classList.add('hiding');
    setTimeout(() => el.remove(), 240);
  };

  const batDauDem = () => {
    batDauLuc = Date.now();
    timer = setTimeout(dong, Math.max(400, conLaiMs));
  };

  el.addEventListener('mouseenter', () => {
    clearTimeout(timer);
    conLaiMs = Math.max(400, conLaiMs - (Date.now() - batDauLuc));
  });
  el.addEventListener('mouseleave', () => {
    if (!daDong) batDauDem();
  });
  btnClose.addEventListener('click', dong);

  batDauDem();
}

/**
 * Hộp thoại xác nhận chuẩn giao diện TutorHub (thay thế window.confirm).
 * @returns {Promise<boolean>}
 */
export function xacNhanHanhDong({
  tieuDe = 'Xác nhận thao tác',
  noiDung = '',
  nutXacNhan = 'Xác nhận',
  nutHuy = 'Hủy',
  nguyHiem = false,
} = {}) {
  damBaoStyleDuPhong();
  return new Promise((resolve) => {
    const backdrop = document.createElement('div');
    backdrop.className = 'th-dialog-backdrop';
    backdrop.setAttribute('role', 'dialog');
    backdrop.setAttribute('aria-modal', 'true');

    const card = document.createElement('div');
    card.className = 'th-dialog-card';

    const head = document.createElement('div');
    head.className = 'th-dialog-head';
    const h3 = document.createElement('h3');
    h3.className = 'th-dialog-title';
    h3.textContent = tieuDe;
    const btnX = document.createElement('button');
    btnX.type = 'button';
    btnX.className = 'th-toast-close';
    btnX.setAttribute('aria-label', 'Đóng');
    btnX.textContent = '×';
    head.appendChild(h3);
    head.appendChild(btnX);

    const p = document.createElement('p');
    p.className = 'th-dialog-desc';
    p.textContent = noiDung;

    const actions = document.createElement('div');
    actions.className = 'th-dialog-actions';
    const btnCancel = document.createElement('button');
    btnCancel.type = 'button';
    btnCancel.className = 'th-dialog-btn cancel';
    btnCancel.textContent = nutHuy;

    const btnOk = document.createElement('button');
    btnOk.type = 'button';
    btnOk.className = `th-dialog-btn ${nguyHiem ? 'danger' : 'primary'}`;
    btnOk.textContent = nutXacNhan;

    actions.appendChild(btnCancel);
    actions.appendChild(btnOk);

    card.appendChild(head);
    if (noiDung) card.appendChild(p);
    card.appendChild(actions);
    backdrop.appendChild(card);
    document.body.appendChild(backdrop);

    let xong = false;
    const ketThuc = (val) => {
      if (xong) return;
      xong = true;
      document.removeEventListener('keydown', onKey);
      backdrop.remove();
      resolve(val);
    };
    const onKey = (e) => {
      if (e.key === 'Escape') ketThuc(false);
    };
    document.addEventListener('keydown', onKey);
    backdrop.addEventListener('click', (e) => {
      if (e.target === backdrop) ketThuc(false);
    });
    btnX.addEventListener('click', () => ketThuc(false));
    btnCancel.addEventListener('click', () => ketThuc(false));
    btnOk.addEventListener('click', () => ketThuc(true));
    btnOk.focus();
  });
}

/**
 * Hộp thoại nhập lý do / văn bản chuẩn giao diện TutorHub (thay thế window.prompt).
 * @returns {Promise<string | null>}
 */
export function nhapLyDoModal({
  tieuDe = 'Nhập thông tin',
  noiDung = '',
  placeholder = 'Nhập nội dung...',
  macDinh = '',
  batBuoc = true,
  doDaiToiThieu = 0,
  goiY = [],
  nutXacNhan = 'Xác nhận',
  nutHuy = 'Hủy',
  nguyHiem = false,
} = {}) {
  damBaoStyleDuPhong();
  return new Promise((resolve) => {
    const backdrop = document.createElement('div');
    backdrop.className = 'th-dialog-backdrop';
    backdrop.setAttribute('role', 'dialog');
    backdrop.setAttribute('aria-modal', 'true');

    const card = document.createElement('div');
    card.className = 'th-dialog-card';

    const head = document.createElement('div');
    head.className = 'th-dialog-head';
    const h3 = document.createElement('h3');
    h3.className = 'th-dialog-title';
    h3.textContent = tieuDe;
    const btnX = document.createElement('button');
    btnX.type = 'button';
    btnX.className = 'th-toast-close';
    btnX.setAttribute('aria-label', 'Đóng');
    btnX.textContent = '×';
    head.appendChild(h3);
    head.appendChild(btnX);
    card.appendChild(head);

    if (noiDung) {
      const p = document.createElement('p');
      p.className = 'th-dialog-desc';
      p.textContent = noiDung;
      card.appendChild(p);
    }

    const form = document.createElement('form');

    const textarea = document.createElement('textarea');
    textarea.className = 'th-dialog-input';
    textarea.placeholder = placeholder;
    textarea.value = macDinh || '';

    if (Array.isArray(goiY) && goiY.length > 0) {
      const chipsWrap = document.createElement('div');
      chipsWrap.className = 'th-dialog-chips';
      goiY.forEach((g) => {
        const chip = document.createElement('button');
        chip.type = 'button';
        chip.className = 'th-dialog-chip';
        chip.textContent = g;
        chip.addEventListener('click', () => {
          textarea.value = g;
          errEl.textContent = '';
          textarea.focus();
        });
        chipsWrap.appendChild(chip);
      });
      form.appendChild(chipsWrap);
    }

    const errEl = document.createElement('div');
    errEl.className = 'th-dialog-err';

    const actions = document.createElement('div');
    actions.className = 'th-dialog-actions';
    const btnCancel = document.createElement('button');
    btnCancel.type = 'button';
    btnCancel.className = 'th-dialog-btn cancel';
    btnCancel.textContent = nutHuy;

    const btnOk = document.createElement('button');
    btnOk.type = 'submit';
    btnOk.className = `th-dialog-btn ${nguyHiem ? 'danger' : 'primary'}`;
    btnOk.textContent = nutXacNhan;

    actions.appendChild(btnCancel);
    actions.appendChild(btnOk);

    form.appendChild(textarea);
    form.appendChild(errEl);
    form.appendChild(actions);
    card.appendChild(form);
    backdrop.appendChild(card);
    document.body.appendChild(backdrop);

    let xong = false;
    const ketThuc = (val) => {
      if (xong) return;
      xong = true;
      document.removeEventListener('keydown', onKey);
      backdrop.remove();
      resolve(val);
    };
    const onKey = (e) => {
      if (e.key === 'Escape') ketThuc(null);
    };
    document.addEventListener('keydown', onKey);
    backdrop.addEventListener('click', (e) => {
      if (e.target === backdrop) ketThuc(null);
    });
    btnX.addEventListener('click', () => ketThuc(null));
    btnCancel.addEventListener('click', () => ketThuc(null));
    textarea.addEventListener('input', () => {
      errEl.textContent = '';
    });

    form.addEventListener('submit', (e) => {
      e.preventDefault();
      const val = textarea.value.trim();
      if (batBuoc && !val) {
        errEl.textContent = 'Vui lòng nhập nội dung trước khi xác nhận.';
        textarea.focus();
        return;
      }
      if (doDaiToiThieu > 0 && val.length < doDaiToiThieu) {
        errEl.textContent = `Vui lòng nhập tối thiểu ${doDaiToiThieu} ký tự.`;
        textarea.focus();
        return;
      }
      ketThuc(val);
    });

    textarea.focus();
    textarea.setSelectionRange(textarea.value.length, textarea.value.length);
  });
}
