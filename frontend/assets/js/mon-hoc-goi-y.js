/**
 * mon-hoc-goi-y.js — Bộ máy phân nhóm, tìm kiếm gần đúng không dấu & viết tắt
 * cho toàn bộ danh mục Môn học trên TutorHub (160+ môn học).
 */

export const THU_TU_NHOM_MON_HOC = [
  'Toán & Khoa học Tự nhiên',
  'Ngoại ngữ & Chứng chỉ',
  'Công nghệ & Lập trình',
  'Ngữ văn, Xã hội & Tiểu học',
  'Kinh tế & Kinh doanh',
  'Thiết kế, Mỹ thuật & Kiến trúc',
  'Âm nhạc & Biểu diễn',
  'Thể thao & Cờ trí tuệ',
  'Kỹ năng mềm & Đời sống',
];

const MON_PHO_BIEN_UU_TIEN = [
  'Toán',
  'Tiếng Anh',
  'Vật lý',
  'Hóa học',
  'Ngữ văn',
  'Tin học',
  'Luyện thi IELTS',
  'Tiếng Anh giao tiếp',
  'Tiếng Trung',
  'Tiếng Nhật',
  'Tiếng Hàn',
  'Lập trình Python',
  'Sinh học',
  'Lịch sử',
  'Địa lý',
  'Piano',
  'Guitar',
  'Cờ vua',
  'Thiết kế đồ họa (Photoshop / Illustrator)',
  'Excel nâng cao & VBA',
  'Luyện thi TOEIC',
  'Tiếng Đức',
  'Tiếng Pháp',
  'Bơi lội',
  'Yoga & Thiền',
  'Luyện chữ đẹp',
];

const TU_DONG_NGHIA_THEO_TEN = {
  toan: ['math', 'toan hoc', 'dai so', 'hinh hoc', 'cap 1', 'cap 2', 'cap 3'],
  'toan tu duy': ['soroban', 'toan thong minh', 'logic'],
  'toan cao cap': ['calculus', 'dai hoc', 'c1', 'c2'],
  'xac suat thong ke': ['xstk', 'statistics', 'probability'],
  'vat ly': ['ly', 'li', 'vat li', 'physics'],
  'hoa hoc': ['hoa', 'chemistry'],
  'sinh hoc': ['sinh', 'biology'],
  'khoa hoc tu nhien': ['khtn', 'science'],
  'on thi danh gia nang luc': ['dgnl', 'hsa', 'tsa', 'v-act'],
  'luyen thi vao lop 10': ['thi vao 10', 'tuyen sinh 10', 'cap 3'],
  'ngu van': ['van', 'van hoc', 'lam van', 'literature'],
  'lich su': ['su', 'history'],
  'dia ly': ['dia', 'dia li', 'geography'],
  'giao duc cong dan': ['gdcd'],
  'giao duc kinh te và phap luat': ['ktpl', 'gdktpl'],
  'giao duc kinh te va phap luat': ['ktpl', 'gdktpl'],
  'tieng viet tieu hoc': ['tap doc', 'chinh ta', 'cap 1'],
  'tien tieu hoc (vao lop 1)': ['hanh trang vao lop 1', 'mam non', 'lop 1'],
  'tieng anh': ['english', 'anh van', 'av', 'eng', 'ta'],
  'tieng anh giao tiep': ['giao tiep', 'speaking', 'english communication'],
  'tieng anh thieu nhi': ['english kids', 'tre em', 'tieu hoc'],
  'luyen thi ielts': ['ielts', 'tieng anh', 'writing', 'speaking', 'du hoc'],
  'luyen thi toeic': ['toeic', 'tieng anh'],
  'luyen thi toefl': ['toefl', 'tieng anh'],
  'luyen thi sat': ['sat', 'du hoc my'],
  'luyen thi cambridge (starters / movers / flyers)': ['cambridge', 'starters', 'movers', 'flyers'],
  'luyen thi b1 / b2 vstep': ['vstep', 'b1', 'b2', 'chung chi anh'],
  'tieng trung': ['hoa ngu', 'tieng hoa', 'chinese', 'trung quoc', 'tq'],
  'luyen thi hsk': ['hsk', 'tieng trung', 'hoa ngu'],
  'tieng nhat': ['japanese', 'nhat ngu', 'n5', 'n4', 'n3', 'n2', 'n1'],
  'luyen thi jlpt': ['jlpt', 'tieng nhat', 'n5', 'n4', 'n3', 'n2', 'n1'],
  'tieng han': ['korean', 'han ngu', 'han quoc'],
  'luyen thi topik': ['topik', 'tieng han'],
  'tieng phap': ['french', 'phap ngu'],
  'luyen thi delf / dalf': ['delf', 'dalf', 'tieng phap'],
  'tieng duc': ['german', 'goethe', 'b1 duc', 'du hoc nghe duc'],
  'tieng tay ban nha': ['spanish', 'tbn'],
  'tieng nga': ['russian'],
  'tieng y': ['italian', 'italia'],
  'tieng thai': ['thai lan'],
  'tieng viet cho nguoi nuoc ngoai': ['vietnamese for foreigners', 'tv'],
  'tin hoc': ['it', 'cntt', 'may tinh', 'computer'],
  'tin hoc van phong (word / powerpoint)': ['word', 'ppt', 'powerpoint', 'office', 'van phong'],
  'excel nang cao & vba': ['excel', 'vba', 'bang tinh', 'pivot', 'van phong'],
  'luyen thi mos / ic3': ['mos', 'ic3', 'tin hoc quoc te'],
  'lap trinh python': ['py', 'python', 'code', 'coding', 'lap trinh'],
  'lap trinh c / c++': ['cpp', 'c++', 'code', 'tin hoc tre', 'hsg tin'],
  'lap trinh c#': ['csharp', 'c#', 'lap trinh'],
  'lap trinh java': ['java', 'spring', 'oop', 'code'],
  'lap trinh javascript / typescript': ['js', 'ts', 'javascript', 'typescript', 'code'],
  'lap trinh web (html / css / js)': ['web', 'html', 'css', 'js', 'website'],
  'lap trinh frontend (react / vue)': ['frontend', 'fe', 'react', 'vue', 'web'],
  'lap trinh backend': ['backend', 'be', 'server'],
  'lap trinh php / laravel': ['php', 'laravel', 'web'],
  'lap trinh di dong (flutter / android / ios)': ['mobile', 'app', 'flutter', 'android', 'ios', 'swift', 'kotlin'],
  'co so du lieu & sql': ['sql', 'csdl', 'database', 'db', 'mysql'],
  'cau truc du lieu & giai thuat': ['dsa', 'ctdl', 'giai thuat', 'leetcode', 'algorithm'],
  'tri tue nhan tao & machine learning': ['ai', 'ml', 'deep learning', 'tri tue nhan tao'],
  'khoa hoc du lieu (data science)': ['ds', 'data', 'phan tich du lieu'],
  'phan tich du lieu (power bi / tableau)': ['da', 'data analyst', 'power bi', 'tableau', 'bi'],
  'an toan thong tin & mang may tinh': ['cyber', 'security', 'network', 'mang', 'ccna'],
  'dien toan dam may & devops': ['cloud', 'aws', 'docker', 'devops'],
  'lap trinh scratch cho tre em': ['scratch', 'stem', 'lap trinh tre em'],
  'robotics & arduino': ['robot', 'arduino', 'iot', 'stem', 'dien tu'],
  'lap trinh game (unity / godot)': ['game', 'unity', 'godot', 'lam game'],
  'kiem thu phan mem (qa / tester)': ['qa', 'qc', 'tester', 'testing'],
  'he dieu hanh & linux': ['linux', 'ubuntu', 'os'],
  'kinh te vi mo & vi mo': ['kinh te', 'economics', 'ktvm'],
  'ke toan tai chinh': ['ke toan', 'kt', 'accounting', 'misa'],
  'ke toan doanh nghiep & thue': ['ke toan', 'thue', 'kt', 'bao cao tai chinh'],
  'digital marketing': ['mkt', 'marketing', 'truyen thong', 'quang cao'],
  'seo & quang cao truc tuyen': ['seo', 'fb ads', 'google ads', 'tiktok ads', 'chay quang cao'],
  'thuong mai dien tu': ['tmdt', 'shopee', 'tiktok shop', 'ecommerce'],
  'chung khoan & tai chinh ca nhan': ['dau tu', 'chung khoan', 'co phieu', 'tai chinh'],
  'xuat nhap khau & logistics': ['xnk', 'logistics', 'chuoi cung ung'],
  'phan tich kinh doanh (business analyst)': ['ba', 'business analyst'],
  'my thuat & ve co ban': ['ve', 'hoi hoa', 'drawing', 'art', 'my thuat'],
  've luyen thi khoi h / v': ['khoi h', 'khoi v', 'hinh hoa', 'trang tri mau', 'kien truc'],
  've manga / anime / digital art': ['manga', 'anime', 'procreate', 've may', 'digital'],
  'thiet ke do hoa (photoshop / illustrator)': ['ps', 'pts', 'photoshop', 'ai', 'illustrator', 'design', 'do hoa'],
  'thiet ke canva & an pham truyen thong': ['canva', 'poster', 'slide', 'thiet ke'],
  'thiet ke ui / ux (figma)': ['ui', 'ux', 'uiux', 'figma', 'giao dien'],
  'dung phim & bien tap video (premiere / capcut)': ['edit video', 'capcut', 'premiere', 'pr', 'tiktok', 'youtube', 'dung phim'],
  'ky xao & motion graphics (after effects)': ['ae', 'after effects', 'motion', 'vfx'],
  'do hoa 3d (blender / 3ds max)': ['3d', 'blender', '3ds max', 'maya', 'render'],
  'autocad & thiet ke ky thuat': ['cad', 'autocad', 'co khi', 'ban ve'],
  'revit & kien truc': ['revit', 'bim', 'kien truc', 'xay dung'],
  'nhiep anh': ['chup anh', 'photography', 'camera', 'lightroom'],
  piano: ['dan piano', 'nhac', 'am nhac', 'phim'],
  guitar: ['dan guitar', 'ghita', 'ghi ta', 'acoustic', 'classic', 'nhac'],
  'organ / keyboard': ['organ', 'keyboard', 'dan organ', 'nhac'],
  violin: ['vi cam', 'dan violin', 'nhac'],
  ukulele: ['dan ukulele', 'uku', 'nhac'],
  'trong & bo go': ['drum', 'cajon', 'trong', 'nhac'],
  'sao truc & nhac cu dan toc': ['sao', 'dan tranh', 'dan bau', 'nhac cu'],
  'thanh nhac & luyen giong': ['hat', 'luyen hat', 'vocal', 'ca hat', 'nhac'],
  'khieu vu & nhay hien dai': ['dance', 'nhay', 'hiphop', 'kpop', 'zumba', 'ballet'],
  'co vua': ['chess', 'co'],
  'co tuong': ['co'],
  'co vay': ['go', 'co'],
  'boi loi': ['boi', 'swimming'],
  'cau long': ['badminton', 'the thao'],
  'bong da': ['football', 'soccer', 'da banh'],
  'bong ro': ['basketball'],
  'bong ban': ['ping pong', 'table tennis'],
  'tennis & pickleball': ['tennis', 'pickleball', 'quan vot'],
  'yoga & thien': ['yoga', 'thien', 'meditation', 'suc khoe'],
  'vo thuat & tu ve': ['karate', 'taekwondo', 'boxing', 'kickboxing', 'vovinam', 'muay thai', 'vo'],
  'the hinh & huan luyen ca nhan (pt)': ['gym', 'pt', 'fitness', 'giam can', 'tap gym'],
  'ky nang thuyet trinh & mc': ['mc', 'thuyet trinh', 'noi truoc dam dong', 'giong noi'],
  'ky nang giao tiep & dam phan': ['giao tiep', 'ky nang mem', 'soft skills'],
  'ghi nho & so do tu duy (mindmap)': ['mindmap', 'sieu tri nho'],
  'nau an & lam banh': ['nau an', 'lam banh', 'baking', 'cooking', 'bep'],
  'pha che (barista / bartender)': ['barista', 'bartender', 'cafe', 'tra sua', 'pha che'],
  'trang diem ca nhan': ['makeup', 'make up', 'lam dep'],
  'rubik & tro choi tri tue': ['rubik', 'rubic'],
  'y hoc co so & giai phau': ['y khoa', 'giai phau', 'duoc', 'dieu duong'],
};

export function boDauTiengViet(str = '') {
  return String(str || '')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .toLowerCase()
    .trim();
}

export function layThongTinBoSungMonHoc(mon) {
  const ten = typeof mon === 'string' ? mon : mon?.ten || '';
  const k = boDauTiengViet(ten);

  let nhom = 'Kỹ năng mềm & Đời sống';
  let badge = '✨';

  if (
    /^(toan|dai so|giai tich|xac suat|vat ly|hoa hoc|sinh hoc|khoa hoc tu nhien|thien van|on thi toan|on thi danh gia|luyen thi vao lop 10|boi duong hoc sinh gioi|y hoc)/.test(k)
  ) {
    nhom = 'Toán & Khoa học Tự nhiên';
    if (k.includes('toan') || k.includes('dai so') || k.includes('giai tich') || k.includes('xac suat')) badge = '√x';
    else if (k.includes('vat ly')) badge = '⚡';
    else if (k.includes('hoa')) badge = '🧪';
    else if (k.includes('sinh') || k.includes('y hoc')) badge = '🧬';
    else if (k.includes('thien van')) badge = '🪐';
    else badge = '📐';
  } else if (
    /(tieng anh|tieng trung|tieng nhat|tieng han|tieng phap|tieng duc|tieng tay ban nha|tieng nga|tieng y|tieng thai|tieng a rap|nguoi nuoc ngoai|phat am|ngu phap|\b(?:ielts|toeic|toefl|sat|gmat|gre|cambridge|ket|pet|fce|vstep|hsk|jlpt|topik|delf|dalf)\b)/.test(k)
  ) {
    nhom = 'Ngoại ngữ & Chứng chỉ';
    if (k.includes('trung') || k.includes('hsk')) badge = 'ZH';
    else if (k.includes('nhat') || k.includes('jlpt')) badge = 'JP';
    else if (k.includes('han') || k.includes('topik')) badge = 'KR';
    else if (k.includes('phap') || k.includes('delf')) badge = 'FR';
    else if (k.includes('duc')) badge = 'DE';
    else if (k.includes('tay ban nha')) badge = 'ES';
    else if (k.includes('nga')) badge = 'RU';
    else if (k.includes('tieng viet')) badge = 'VI';
    else badge = 'EN';
  } else if (
    /(tin hoc|excel|mos|lap trinh|co so du lieu|cau truc du lieu|tri tue nhan tao|khoa hoc du lieu|khoa hoc may tinh|phan tich du lieu|an toan thong tin|dien toan dam may|scratch|robotics|kiem thu phan mem|linux)/.test(k)
  ) {
    nhom = 'Công nghệ & Lập trình';
    if (k.includes('excel') || k.includes('power bi')) badge = '📊';
    else if (k.includes('tri tue nhan tao') || k.includes('robotics')) badge = '🤖';
    else if (k.includes('game')) badge = '🎮';
    else badge = '💻';
  } else if (
    /(ngu van|lich su|dia ly|giao duc|khoa hoc xa hoi|triet hoc|tam ly|xa hoi hoc|tieng viet tieu hoc|luyen chu|tap doc|bao bai|tien tieu hoc|lam van)/.test(k)
  ) {
    nhom = 'Ngữ văn, Xã hội & Tiểu học';
    if (k.includes('su')) badge = '🏛️';
    else if (k.includes('dia')) badge = '🌏';
    else if (k.includes('van') || k.includes('chu') || k.includes('doc')) badge = '✍️';
    else badge = '📚';
  } else if (
    /(kinh te|ke toan|kiem toan|tai chinh|quan tri|marketing|seo|thuong mai|chung khoan|xuat nhap khau|luat kinh te|business analyst)/.test(k)
  ) {
    nhom = 'Kinh tế & Kinh doanh';
    badge = '📈';
  } else if (
    /(my thuat|ve |thiet ke|dung phim|ky xao|do hoa|autocad|revit|nhiep anh|thu phap)/.test(k)
  ) {
    nhom = 'Thiết kế, Mỹ thuật & Kiến trúc';
    if (k.includes('dung phim') || k.includes('ky xao')) badge = '🎬';
    else if (k.includes('nhiep anh')) badge = '📷';
    else badge = '🎨';
  } else if (
    /(piano|guitar|organ|violin|ukulele|cello|trong|sao truc|thanh nhac|nhac ly|hoa am|khieu vu|dien xuat)/.test(k)
  ) {
    nhom = 'Âm nhạc & Biểu diễn';
    if (k.includes('khieu vu')) badge = '💃';
    else if (k.includes('thanh nhac')) badge = '🎤';
    else badge = '🎵';
  } else if (
    /(co vua|co tuong|co vay|boi loi|cau long|bong da|bong ro|bong ban|tennis|yoga|pilates|vo thuat|the hinh|chay bo)/.test(k)
  ) {
    nhom = 'Thể thao & Cờ trí tuệ';
    if (k.startsWith('co ')) badge = '♟️';
    else if (k.includes('yoga') || k.includes('pilates')) badge = '🧘';
    else if (k.includes('boi')) badge = '🏊';
    else badge = '⚽';
  } else {
    nhom = 'Kỹ năng mềm & Đời sống';
    if (k.includes('nau an') || k.includes('pha che')) badge = '🍳';
    else if (k.includes('rubik')) badge = '🧩';
    else badge = '⭐';
  }

  const tuDongNghia = TU_DONG_NGHIA_THEO_TEN[k] || [];
  return { nhom, badge, tuDongNghia };
}

export function timKiemMonHocThongMinh(dsMonHocApi = [], tuKhoa = '', gioiHan = 14) {
  const ds = Array.isArray(dsMonHocApi) ? dsMonHocApi : [];
  const q = boDauTiengViet(tuKhoa);

  if (!q) {
    const mapByKey = new Map(ds.map((m) => [boDauTiengViet(m.ten), m]));
    const ketQua = [];
    const daCo = new Set();

    for (const tenUuTien of MON_PHO_BIEN_UU_TIEN) {
      const hit = mapByKey.get(boDauTiengViet(tenUuTien));
      if (hit && !daCo.has(hit.id)) {
        daCo.add(hit.id);
        ketQua.push({ ...hit, ...layThongTinBoSungMonHoc(hit) });
        if (ketQua.length >= gioiHan) break;
      }
    }

    for (const m of ds) {
      if (ketQua.length >= gioiHan) break;
      if (!daCo.has(m.id)) {
        daCo.add(m.id);
        ketQua.push({ ...m, ...layThongTinBoSungMonHoc(m) });
      }
    }
    return ketQua;
  }

  const cacTu = q.split(/\s+/).filter(Boolean);
  const scored = [];

  for (const m of ds) {
    const meta = layThongTinBoSungMonHoc(m);
    const tenNorm = boDauTiengViet(m.ten);
    const nhomNorm = boDauTiengViet(meta.nhom);
    const aliasNorms = meta.tuDongNghia.map(boDauTiengViet);
    const words = tenNorm.split(/[\s/()&,-]+/).filter(Boolean);

    let score = 0;

    if (tenNorm === q) score = 1000;
    else if (tenNorm.startsWith(q)) score = 850 - Math.min(tenNorm.length, 60);
    else if (words.some((w) => w.startsWith(q))) score = 720 - Math.min(tenNorm.length, 60);
    else if (tenNorm.includes(q)) score = 580 - tenNorm.indexOf(q);
    else if (aliasNorms.some((a) => a === q)) score = 680;
    else if (aliasNorms.some((a) => a.startsWith(q))) score = 540;
    else if (aliasNorms.some((a) => a.includes(q))) score = 460;
    else if (cacTu.length > 1 && cacTu.every((t) => tenNorm.includes(t) || aliasNorms.some((a) => a.includes(t)))) {
      score = 420;
    } else if (nhomNorm.includes(q)) {
      score = 260;
    } else if (q.length >= 2) {
      // Khớp viết tắt chữ cái đầu (VD: "kt" -> "Kế toán", "lt" -> "Lập trình")
      const vietTatDau = words.map((w) => w[0]).join('');
      if (vietTatDau.startsWith(q)) score = 380;
      else if (vietTatDau.includes(q)) score = 300;
    }

    if (score > 0) {
      // Thưởng nhẹ cho các môn phổ biến
      const idxPb = MON_PHO_BIEN_UU_TIEN.findIndex((p) => boDauTiengViet(p) === tenNorm);
      if (idxPb >= 0) score += 45 - idxPb;
      scored.push({ ...m, ...meta, _score: score });
    }
  }

  scored.sort((a, b) => b._score - a._score || a.ten.localeCompare(b.ten, 'vi'));
  return gioiHan > 0 ? scored.slice(0, gioiHan) : scored;
}

export function highlightKhopMonHoc(ten = '', tuKhoa = '', escapeFn = (s) => s) {
  const q = boDauTiengViet(tuKhoa);
  if (!q || !ten) return escapeFn(ten);

  const chars = [...ten];
  const normChars = [];
  const origIndexMap = [];

  chars.forEach((ch, idx) => {
    const norm = boDauTiengViet(ch);
    for (const nc of norm) {
      normChars.push(nc);
      origIndexMap.push(idx);
    }
  });

  const normStr = normChars.join('');
  const pos = normStr.indexOf(q);
  if (pos < 0) return escapeFn(ten);

  const startIdx = origIndexMap[pos];
  const endIdx = origIndexMap[pos + q.length - 1] + 1;
  const truoc = chars.slice(0, startIdx).join('');
  const khop = chars.slice(startIdx, endIdx).join('');
  const sau = chars.slice(endIdx).join('');

  return `${escapeFn(truoc)}<mark>${escapeFn(khop)}</mark>${escapeFn(sau)}`;
}

export function layDanhSachMonNoiBat(dsMonHocApi = [], gioiHan = 24) {
  return timKiemMonHocThongMinh(dsMonHocApi, '', gioiHan);
}

export function taoOptionsNhomMonHocHtml(
  dsMonHocApi = [],
  selectedId = '',
  nhanTatCa = 'Tất cả môn học',
  escapeFn = (s) => s
) {
  const nhomMap = new Map(THU_TU_NHOM_MON_HOC.map((n) => [n, []]));
  for (const m of dsMonHocApi || []) {
    const { nhom } = layThongTinBoSungMonHoc(m);
    if (!nhomMap.has(nhom)) nhomMap.set(nhom, []);
    nhomMap.get(nhom).push(m);
  }

  const head = nhanTatCa !== null ? `<option value="">${escapeFn(nhanTatCa)}</option>` : '';
  const groups = [];
  for (const [tenNhom, list] of nhomMap.entries()) {
    if (!list.length) continue;
    list.sort((a, b) => a.ten.localeCompare(b.ten, 'vi'));
    const opts = list
      .map(
        (m) =>
          `<option value="${escapeFn(m.id)}" ${m.id === selectedId ? 'selected' : ''}>${escapeFn(m.ten)}</option>`
      )
      .join('');
    groups.push(`<optgroup label="${escapeFn(tenNhom)}">${opts}</optgroup>`);
  }

  return head + groups.join('');
}
