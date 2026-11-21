/**
 * ban-do-viet-nam.js — Sử dụng nền bản đồ chuẩn Google Maps Tiếng Việt (hl=vi, gl=VN) nguyên bản,
 * không can thiệp đa giác hay lớp phủ làm biến dạng bản đồ.
 */

export const GOOGLE_MAPS_VN_TILE_URL =
  'https://mt{s}.google.com/vt/lyrs=m&hl=vi&gl=VN&x={x}&y={y}&z={z}';

export const GOOGLE_MAPS_VN_TILE_OPTIONS = {
  subdomains: ['0', '1', '2', '3'],
  minZoom: 4,
  maxZoom: 19,
  attribution: '&copy; Google Maps',
};

export function ganChuQuyenBienDaoVietNam(map) {
  if (!map || typeof window.L === 'undefined' || map._daGanChuQuyenVn) return;
  map._daGanChuQuyenVn = true;
  const L = window.L;

  map.eachLayer((layer) => {
    if (layer instanceof L.TileLayer) {
      map.removeLayer(layer);
    }
  });

  L.tileLayer(GOOGLE_MAPS_VN_TILE_URL, GOOGLE_MAPS_VN_TILE_OPTIONS).addTo(map);
}
