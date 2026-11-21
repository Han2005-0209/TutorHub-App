# TutorHub Frontend — HTML / CSS / JS Thuần (Static Site cho Vercel)

Phần Frontend của nền tảng **TutorHub**, được xây dựng bằng **HTML5, CSS3 và JavaScript ES Modules thuần** (không cần bước build bundler), chạy hoàn toàn **độc lập** với Backend và chỉ giao tiếp với `.NET 10 Web API` qua HTTP `fetch`.

---

## 1. Cấu trúc Thư mục

```text
frontend/
├── index.html                  # Trang chủ
├── manifest.webmanifest        # Cấu hình PWA
├── vercel.json                 # Cấu hình triển khai tĩnh trên Vercel
├── pages/                      # Các trang màn hình chức năng (*.html)
│   └── dang-nhap.html          # Khung trang đăng nhập
└── assets/
    ├── css/
    │   └── main.css            # Hệ thống Design Tokens (--th-*), layout, buttons, badges, safe-area
    ├── img/
    │   ├── icon-192.svg        # Biểu tượng TutorHub 192x192
    │   └── icon-512.svg        # Biểu tượng TutorHub 512x512
    └── js/
        ├── config.js           # Chứa API_BASE_URL, JITSI_BASE_URL, MAP_TILE_URL
        ├── api.js              # Wrapper fetch gắn Authorization: Bearer và xử lý lỗi/401 tập trung
        ├── dinh-dang.js        # Định dạng tiền VND, ngày giờ GMT+7, tách khung giờ 30 phút
        ├── trang-thai.js       # Nhãn trạng thái theo 18 Enum của 03-class-v4.drawio & ước tính hoàn tiền
        └── phong-hoc-jitsi.js  # Kiểm tra URL phòng học trực tuyến Jitsi Meet
```

---

## 2. Cách chạy Cục bộ (Local Development)

Vì sử dụng ES Modules (`<script type="module">`), cần mở qua một máy chủ tĩnh HTTP (không mở trực tiếp qua giao thức `file://`):

### Cách 1: Dùng `npx serve` (Node.js)
```powershell
npx serve frontend -l 5173
```
Sau đó mở trình duyệt tại `http://localhost:5173`.

### Cách 2: Dùng Python HTTP Server
```powershell
cd frontend
python -m http.server 5173
```

### Cách 3: Dùng VS Code Extension **Live Server**
Mở thư mục `frontend/` làm gốc hoặc cấu hình `liveServer.settings.root: "/frontend"` trên cổng `5500` / `5173`.

---

## 3. Đổi `API_BASE_URL` theo Môi trường & Triển khai Vercel

- Mặc định khi chạy cục bộ, `assets/js/config.js` trỏ tới `http://localhost:5000/api`.
- Khi triển khai lên **Vercel**:
  1. Chọn **Root Directory** của project trên Vercel là `frontend`.
  2. Framework Preset: **Other** (Static Site, không cần Build Command, Output Directory là `.`).
  3. Cập nhật `API_BASE_URL` trong `frontend/assets/js/config.js` (hoặc gán `window.__TUTORHUB_CONFIG__ = { API_BASE_URL: "https://<ten-mien-api>/api" }` trước khi nạp module).
  4. Tuyệt đối **không đặt khóa bí mật (Secret)** vào bất kỳ tệp nào trong `frontend/`.
