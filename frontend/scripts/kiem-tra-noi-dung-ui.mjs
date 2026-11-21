#!/usr/bin/env node
/**
 * Công cụ kiểm tra tự động (CLI / CI Gate) quét toàn bộ giao diện web trong `frontend/`
 * để phát hiện mọi nội dung không phù hợp hiển thị lên web người dùng cuối:
 * - Lộ công nghệ/kiến trúc nội bộ (.NET 10, EF Core, SQL Server, SSMS, Clean Architecture, BCNF/4NF, Class v4...)
 * - Lộ mã tài liệu/tiến độ dev (BR-*, UC_*, Module 1..9, Seed Data, TODO...)
 * - Lộ tên lớp/bảng CSDL & thuộc tính UML (GiaSuMonHoc, TaiKhoanNganHang, /daXacMinh, kho Private...)
 * - Lộ mã Enum thô chưa Việt hóa (HOC_SINH, CHO_DUYET, DA_XOA, TRUC_TUYEN...)
 * - Ngôn từ không phù hợp môi trường giáo dục.
 *
 * Cách chạy: `node frontend/scripts/kiem-tra-noi-dung-ui.mjs`
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { kiemTraVanBanHienThiWeb } from '../assets/js/kiem-tra-noi-dung-ui.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const frontendDir = path.resolve(__dirname, '..');

function layDsTepHtml(dir) {
  const kq = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const fullPath = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name === 'node_modules' || entry.name === 'scripts') continue;
      kq.push(...layDsTepHtml(fullPath));
    } else if (entry.name.endsWith('.html')) {
      kq.push(fullPath);
    }
  }
  return kq;
}

function trichXuatVanBanHienThiTuHtml(html) {
  // Bỏ comment HTML, thẻ <script> và <style>
  const khongScript = html
    .replace(/<!--[\s\S]*?-->/g, ' ')
    .replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, ' ')
    .replace(/<style\b[^>]*>[\s\S]*?<\/style>/gi, ' ');

  // Thu thập giá trị các thuộc tính hiển thị tới người dùng: placeholder, title, alt
  const dsThuocTinh = [];
  const attrRegex = /\b(?:placeholder|title|alt)\s*=\s*"([^"]*)"/gi;
  let m;
  while ((m = attrRegex.exec(khongScript)) !== null) {
    dsThuocTinh.push(m[1]);
  }

  // Bỏ thẻ HTML để lấy phần text hiển thị
  const textThuan = khongScript
    .replace(/<[^>]+>/g, ' ')
    .replace(/&amp;/g, '&')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/\s+/g, ' ')
    .trim();

  return `${textThuan}\n${dsThuocTinh.join('\n')}`;
}

const dsHtml = layDsTepHtml(frontendDir);
let tongViPham = 0;

console.log(`🔍 Đang kiểm tra nội dung hiển thị web trên ${dsHtml.length} trang HTML...`);

for (const filePath of dsHtml) {
  const relPath = path.relative(frontendDir, filePath).replace(/\\/g, '/');
  const noiDungHtml = fs.readFileSync(filePath, 'utf8');
  const vanBanHienThi = trichXuatVanBanHienThiTuHtml(noiDungHtml);
  const ketQua = kiemTraVanBanHienThiWeb(vanBanHienThi, relPath);

  if (!ketQua.hopLe) {
    tongViPham += ketQua.dsViPham.length;
    console.error(`\n❌ PHÁT HIỆN VI PHẠM tại frontend/${relPath}:`);
    for (const vp of ketQua.dsViPham) {
      console.error(`   - [${vp.nhomQuyTac} / ${vp.maQuyTac}] "${vp.doanViPham}" → ${vp.lyDo}`);
    }
  } else {
    console.log(`  ✅ frontend/${relPath} — Sạch (0 vi phạm)`);
  }
}

if (tongViPham > 0) {
  console.error(`\n⛔ Kiểm tra thất bại: phát hiện tổng cộng ${tongViPham} nội dung không phù hợp hiển thị trên web.`);
  process.exit(1);
}

console.log('\n✅ HOÀN TẤT: Toàn bộ giao diện web đạt chuẩn hiển thị người dùng cuối (0 vi phạm).');
