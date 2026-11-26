export type Trang = 'tong-quan' | 'van-ban-di' | 'van-ban-den' | 'cong-viec' | 'hanh-trinh' | 'giao-nhan' | 'ho-so' | 'bao-cao' | 'quan-tri' | 'co-cau' | 'uy-quyen';
export type TrangThai = 'NHAP' | 'TRA_SUA' | 'CHO_DUYET' | 'CHO_CAP_SO' | 'DA_CAP_SO' | 'CHO_KY' | 'DA_KY' | 'DA_PHAT_HANH' | 'DANG_CHUYEN' | 'HOAN_THANH';
export type Loai = 'DI' | 'DEN';
export interface SuKien {
  id: string;
  action: string;
  title: string;
  detail: string;
  by: string;
  agency: string;
  time: string;
  state: 'done' | 'current' | 'pending' | 'warning';
  version?: number;
}
export interface ChuyenPhat {
  id: string;
  agency: string;
  contact: string;
  status: 'DA_TIEP_NHAN' | 'DANG_CHUYEN' | 'CHO_GUI' | 'DA_GUI' | 'TU_CHOI';
  sentAt?: string;
  receivedAt?: string;
  note?: string;
}
export interface VanBan {
  id: string;
  code: string;
  number?: string;
  title: string;
  excerpt: string;
  type: Loai;
  status: TrangThai;
  urgency: 'THUONG' | 'KHAN' | 'HOA_TOC';
  secrecy: 'THUONG' | 'MAT';
  agency: string;
  department: string;
  creator: string;
  assignee: string;
  createdAt: string;
  dueAt?: string;
  version: number;
  signingSteps?: number;
  to: string[];
  events: SuKien[];
  transfers: ChuyenPhat[];
}
export interface Actor {
  username: string;
  name: string;
  agency: string;
  position: string;
  capabilities: string[];
}
export const stageLabel: Record<TrangThai,string> = {
  NHAP:'Bản nháp', TRA_SUA:'Yêu cầu sửa', CHO_DUYET:'Chờ phê duyệt',
  CHO_CAP_SO:'Chờ cấp số', DA_CAP_SO:'Đã cấp số', CHO_KY:'Chờ ký số',
  DA_KY:'Đã ký số', DA_PHAT_HANH:'Đã phát hành', DANG_CHUYEN:'Đang luân chuyển', HOAN_THANH:'Hoàn thành'
};
export const transferLabel: Record<ChuyenPhat['status'],string> = {
  DA_TIEP_NHAN:'Đã tiếp nhận', DANG_CHUYEN:'Đang chuyển', CHO_GUI:'Chờ gửi', DA_GUI:'Đã gửi', TU_CHOI:'Từ chối'
};
export const formatDate = (date?: string) => date ? new Intl.DateTimeFormat('vi-VN', {
  day:'2-digit', month:'2-digit', year:'numeric', hour:'2-digit', minute:'2-digit', hour12:false
}).format(new Date(date)) : '—';
