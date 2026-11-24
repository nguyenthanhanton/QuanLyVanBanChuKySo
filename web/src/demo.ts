import type { Actor, VanBan, SuKien, ChuyenPhat } from './types';

export const demoUsers: Record<string,Actor> = {
  vanthu_a: { username:'vanthu_a',name:'Nguyễn Thị Minh',agency:'Cơ quan A',position:'Văn thư',capabilities:['documents.read','documents.manage','documents.submit','workflow.queue','registers.manage','publications.deliver','incoming.manage','deliveries.accept','dossiers.manage','reports.read'] },
  truongphong_a: { username:'truongphong_a',name:'Trần Văn Hùng',agency:'Cơ quan A',position:'Trưởng phòng',capabilities:['documents.read','workflow.queue','workflow.decide'] },
  truongvanphong_a: { username:'truongvanphong_a',name:'Lê Thu Hà',agency:'Cơ quan A',position:'Trưởng văn phòng',capabilities:['documents.read','workflow.queue','workflow.decide'] },
  lanhdao_a: { username:'lanhdao_a',name:'Phạm Văn Nam',agency:'Cơ quan A',position:'Lãnh đạo',capabilities:['documents.read','workflow.queue','workflow.decide'] },
  quantrihethong: { username:'quantrihethong',name:'Quản trị hệ thống',agency:'Toàn hệ thống',position:'Quản trị',capabilities:['agency.admins.manage','agencies.manage','audit.technical.read'] },
  vanthu_b: { username:'vanthu_b',name:'Ngô Hải Yến',agency:'Cơ quan B',position:'Văn thư',capabilities:['documents.read','incoming.manage','deliveries.accept','workflow.queue'] },
  vanthu_c: { username:'vanthu_c',name:'Phan Thanh Vân',agency:'Cơ quan C',position:'Văn thư',capabilities:['documents.read','incoming.manage','deliveries.accept','workflow.queue'] }
};

const ev = (id:string,action:string,title:string,detail:string,by:string,agency:string,time:string,state:SuKien['state']='done',version?:number):SuKien =>
  ({ id,action,title,detail,by,agency,time,state,version });
const tr = (id:string,agency:string,contact:string,status:ChuyenPhat['status'],sentAt?:string,receivedAt?:string,note?:string):ChuyenPhat =>
  ({id,agency,contact,status,sentAt,receivedAt,note});

export const demoDocuments: VanBan[] = [
  {
    id:'demo-001',code:'VB-20261124-A001',number:'128/KH-CQA',
    title:'Kế hoạch tổ chức hội nghị tổng kết công tác năm 2026',
    excerpt:'Tổ chức hội nghị tổng kết, đánh giá kết quả thực hiện nhiệm vụ năm 2026 và triển khai phương hướng năm 2027.',
    type:'DI',status:'DANG_CHUYEN',urgency:'KHAN',secrecy:'THUONG',agency:'Cơ quan A',department:'Phòng Tham mưu',
    creator:'Nguyễn Thị Minh',assignee:'Văn thư Cơ quan B',createdAt:'2026-11-20T08:25:00+07:00',dueAt:'2026-11-25T17:00:00+07:00',version:2,
    to:['Cơ quan B','Cơ quan C'],
    events:[
      ev('a1','TAO_MOI','Soạn thảo văn bản','Tạo bản dự thảo lần 1','Nguyễn Thị Minh','Cơ quan A','2026-11-20T08:25:00+07:00','done',1),
      ev('a2','TRINH_DUYET','Trình kiểm tra nội dung','Chuyển Trưởng phòng Tham mưu','Nguyễn Thị Minh','Cơ quan A','2026-11-20T09:10:00+07:00'),
      ev('a3','TRA_SUA','Trả lại để chỉnh sửa','Lý do: Bổ sung căn cứ và phụ lục dự toán','Trần Văn Hùng','Cơ quan A','2026-11-20T10:30:00+07:00','warning',1),
      ev('a4','SUA_BAN_THAO','Cập nhật phiên bản 2','Đã bổ sung phụ lục và trình lại','Nguyễn Thị Minh','Cơ quan A','2026-11-20T14:05:00+07:00','done',2),
      ev('a5','DUYET','Hoàn thành phê duyệt','Trưởng phòng → Trưởng văn phòng → Lãnh đạo','Phạm Văn Nam','Cơ quan A','2026-11-21T15:30:00+07:00'),
      ev('a6','CAP_SO','Cấp số và ngày ban hành','Số 128/KH-CQA · Ngày 22/11/2026','Nguyễn Thị Minh','Cơ quan A','2026-11-22T08:15:00+07:00'),
      ev('a7','KY_SO','Ký số hoàn tất','Đã ký nháy, ký lãnh đạo và ký cơ quan (mô phỏng)','Nguyễn Thị Minh','Cơ quan A','2026-11-22T10:40:00+07:00'),
      ev('a8','PHAT_HANH','Phát hành văn bản','Gửi đồng thời Cơ quan B và C','Nguyễn Thị Minh','Cơ quan A','2026-11-22T14:00:00+07:00'),
      ev('a9','TIEP_NHAN','Cơ quan B đã tiếp nhận','Biên nhận điện tử ngày 23/11','Ngô Hải Yến','Cơ quan B','2026-11-23T09:12:00+07:00'),
      ev('a10','DANG_CHUYEN','Đang chờ Cơ quan C tiếp nhận','Đã gửi; đang chờ phản hồi','Hệ thống','Cơ quan C','2026-11-24T08:00:00+07:00','current')
    ],
    transfers:[
      tr('delivery-b','Cơ quan B','Ngô Hải Yến','DA_TIEP_NHAN','2026-11-22T14:00:00+07:00','2026-11-23T09:12:00+07:00'),
      tr('delivery-c','Cơ quan C','Bộ phận văn thư','DANG_CHUYEN','2026-11-22T14:00:00+07:00')
    ]
  },
  {
    id:'demo-002',code:'VB-20261123-A014',title:'Công văn đề nghị phối hợp triển khai kế hoạch công tác',
    excerpt:'Đề nghị các phòng chuyên môn phối hợp triển khai nhiệm vụ trọng tâm trong tháng 12.',
    type:'DI',status:'CHO_DUYET',urgency:'THUONG',secrecy:'THUONG',agency:'Cơ quan A',department:'Phòng Tham mưu',
    creator:'Đỗ Đức Anh',assignee:'Trần Văn Hùng',createdAt:'2026-11-23T15:15:00+07:00',dueAt:'2026-11-25T16:00:00+07:00',version:1,to:['Cơ quan B'],
    events:[ev('b1','TAO_MOI','Soạn văn bản','Phiên bản 1','Đỗ Đức Anh','Cơ quan A','2026-11-23T15:15:00+07:00'),
      ev('b2','TRINH_DUYET','Chờ trưởng phòng duyệt','Trần Văn Hùng đang giữ việc','Đỗ Đức Anh','Cơ quan A','2026-11-23T16:10:00+07:00','current')],transfers:[]
  },
  {
    id:'demo-003',code:'VB-20261122-A019',title:'Quyết định phê duyệt dự toán thiết bị công nghệ thông tin',
    excerpt:'Phê duyệt dự toán mua sắm thiết bị phục vụ công tác chuyên môn.',
    type:'DI',status:'CHO_KY',urgency:'THUONG',secrecy:'THUONG',agency:'Cơ quan A',department:'Văn phòng',
    creator:'Nguyễn Thị Minh',assignee:'Phạm Văn Nam',createdAt:'2026-11-22T09:15:00+07:00',dueAt:'2026-11-26T09:00:00+07:00',version:1,to:['Cơ quan B'],
    events:[ev('c1','TAO_MOI','Soạn thảo','Phiên bản 1','Nguyễn Thị Minh','Cơ quan A','2026-11-22T09:15:00+07:00'),
      ev('c2','DUYET','Duyệt nội dung','Đã được lãnh đạo chấp thuận','Phạm Văn Nam','Cơ quan A','2026-11-23T10:00:00+07:00'),
      ev('c3','CHO_KY','Chờ chữ ký số','Chờ lãnh đạo ký và văn thư ký cơ quan','Hệ thống','Cơ quan A','2026-11-24T08:00:00+07:00','current')],transfers:[]
  },
  {
    id:'demo-004',code:'VB-20261121-A025',number:'126/BC-CQA',title:'Báo cáo kết quả thực hiện nhiệm vụ tháng 11 năm 2026',
    excerpt:'Tổng hợp kết quả công tác, thuận lợi, khó khăn và kiến nghị giải pháp.',type:'DI',status:'HOAN_THANH',urgency:'THUONG',secrecy:'THUONG',
    agency:'Cơ quan A',department:'Văn phòng',creator:'Nguyễn Thị Minh',assignee:'Đã hoàn tất',createdAt:'2026-11-21T08:00:00+07:00',version:1,to:['Cơ quan B'],
    events:[ev('d1','TAO_MOI','Soạn thảo','Phiên bản 1','Nguyễn Thị Minh','Cơ quan A','2026-11-21T08:00:00+07:00'),
      ev('d2','PHAT_HANH','Phát hành','Số 126/BC-CQA','Nguyễn Thị Minh','Cơ quan A','2026-11-21T16:00:00+07:00'),
      ev('d3','TIEP_NHAN','Đã tiếp nhận','Cơ quan B xác nhận','Ngô Hải Yến','Cơ quan B','2026-11-22T08:00:00+07:00')],
    transfers:[tr('delivery-b4','Cơ quan B','Ngô Hải Yến','DA_TIEP_NHAN','2026-11-21T16:00:00+07:00','2026-11-22T08:00:00+07:00')]
  },
  {
    id:'demo-005',code:'VB-20261124-A031',title:'Thông báo điều chỉnh lịch kiểm tra cuối năm',
    excerpt:'Thông báo cập nhật thời gian triển khai chương trình kiểm tra công tác cuối năm.',type:'DI',status:'TRA_SUA',urgency:'KHAN',secrecy:'THUONG',
    agency:'Cơ quan A',department:'Phòng Tham mưu',creator:'Đỗ Đức Anh',assignee:'Đỗ Đức Anh',createdAt:'2026-11-24T09:30:00+07:00',version:1,to:['Cơ quan B'],
    events:[ev('e1','TAO_MOI','Soạn thảo','Phiên bản 1','Đỗ Đức Anh','Cơ quan A','2026-11-24T09:30:00+07:00'),
      ev('e2','TRA_SUA','Yêu cầu chỉnh sửa','Lý do: Làm rõ thời gian triển khai','Trần Văn Hùng','Cơ quan A','2026-11-24T10:40:00+07:00','warning')],transfers:[]
  },
  {
    id:'demo-006',code:'VB-20261123-B006',number:'42/TB-CQB',title:'Thông báo tiếp nhận và triển khai văn bản phối hợp',
    excerpt:'Văn bản đến được tiếp nhận, đăng ký sổ và chuyển xử lý.',type:'DEN',status:'DANG_CHUYEN',urgency:'THUONG',secrecy:'THUONG',
    agency:'Cơ quan B',department:'Văn thư',creator:'Ngô Hải Yến',assignee:'Hoàng Quốc Bình',createdAt:'2026-11-23T09:12:00+07:00',version:1,to:[],
    events:[ev('f1','TIEP_NHAN','Tiếp nhận văn bản','Đã xác nhận bản phát hành từ Cơ quan A','Ngô Hải Yến','Cơ quan B','2026-11-23T09:12:00+07:00'),
      ev('f2','PHAN_CONG','Trình lãnh đạo phân công','Đang chờ chỉ đạo','Ngô Hải Yến','Cơ quan B','2026-11-24T08:00:00+07:00','current')],transfers:[]
  }
];
