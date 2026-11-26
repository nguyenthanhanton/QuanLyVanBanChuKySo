using Dapper;
using Microsoft.Data.SqlClient;

public sealed record TaoUyQuyenKy(Guid MaTacVu,int MaNguoiDuocUyQuyen,int SoGioHieuLuc,string LyDo);
public sealed class TacVuKyUyQuyen
{
    public Guid MaTacVu {get;set;}
    public Guid MaVanBan {get;set;}
    public int MaNguoiDuocGiao {get;set;}
    public int PhienBanDuocDuyet {get;set;}
    public string Buoc {get;set;}="";
    public string TrangThai {get;set;}="";
    public byte SoBuocKy {get;set;}
    public int MaNguoiTao {get;set;}
}
public sealed class BanGhiUyQuyen
{
    public Guid MaVanBan {get;set;}
    public int PhienBanDuocDuyet {get;set;}
}

public static class UyQuyenKyEndpoints
{
    // Chỉ tác vụ ký nháy và ký lãnh đạo; không thay người soạn hay con dấu cơ quan.
    public static async Task<bool> DangHieuLuc(System.Data.IDbConnection cn,System.Data.IDbTransaction? tx,
        Guid taskId,int agency,int actor,int owner)
        => await cn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM dbo.UyQuyenKyTacVu q
            WHERE q.MaTacVu=@taskId AND q.MaCoQuan=@agency
              AND q.MaNguoiGoc=@owner AND q.MaNguoiDuocUyQuyen=@actor
              AND q.DaThuHoi=0 AND q.HieuLucTu<=SYSUTCDATETIME()
              AND q.HieuLucDen>SYSUTCDATETIME()",
            new{taskId,agency,actor,owner},tx)>0;

    public static async Task<bool> LaNguoiXuLy(System.Data.IDbConnection cn,System.Data.IDbTransaction? tx,
        Guid taskId,int agency,int actor,int owner)
    {
        if(actor==owner || await DangHieuLuc(cn,tx,taskId,agency,actor,owner))return true;
        // Có thể hoàn tất duyệt sau giờ hết hạn nếu đã ký thật trong thời gian ủy quyền.
        return await cn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM dbo.UyQuyenKyTacVu q
            JOIN dbo.TacVuXuLy t ON t.MaTacVu=q.MaTacVu
            JOIN dbo.ChuKySo s ON s.MaVanBan=q.MaVanBan AND s.MaCoQuan=q.MaCoQuan
                AND s.SoPhienBan=t.PhienBanDuocDuyet AND s.MaNguoiKy=@actor
            WHERE q.MaTacVu=@taskId AND q.MaCoQuan=@agency AND q.MaNguoiGoc=@owner
              AND q.MaNguoiDuocUyQuyen=@actor AND q.DaThuHoi=0
              AND s.ThoiDiemKy>=q.HieuLucTu AND s.ThoiDiemKy<=q.HieuLucDen
              AND ((t.Buoc='KIEM_TRA_NOI_DUNG' AND s.LoaiChuKy='KY_NHAY')
                OR (t.Buoc='PHE_DUYET_LANH_DAO' AND s.LoaiChuKy='KY_LANH_DAO'))",
            new{taskId,agency,actor,owner},tx)>0;
    }

    private static async Task<bool> DuDieuKien(SqlConnection cn,System.Data.IDbTransaction tx,
        int agency,TacVuKyUyQuyen task,int delegateId)
    {
        if(delegateId==task.MaNguoiDuocGiao || delegateId==task.MaNguoiTao)return false;
        var allowed=await cn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(DISTINCT candidate.MaNguoiDung)
            FROM dbo.NguoiDung original
            JOIN dbo.NguoiDungVaiTro originalRole ON originalRole.MaNguoiDung=original.MaNguoiDung
                AND originalRole.MaCoQuan=original.MaCoQuan
            JOIN dbo.VaiTro role1 ON role1.MaVaiTro=originalRole.MaVaiTro
            JOIN dbo.NguoiDung candidate ON candidate.MaNguoiDung=@delegateId
                AND candidate.MaCoQuan=original.MaCoQuan AND candidate.DangHoatDong=1
            JOIN dbo.NguoiDungVaiTro candidateRole ON candidateRole.MaNguoiDung=candidate.MaNguoiDung
                AND candidateRole.MaCoQuan=candidate.MaCoQuan
            JOIN dbo.VaiTro role2 ON role2.MaVaiTro=candidateRole.MaVaiTro
            WHERE original.MaNguoiDung=@owner AND original.MaCoQuan=@agency
              AND original.MaPhongBan=candidate.MaPhongBan
              AND ((@stage='KIEM_TRA_NOI_DUNG'
                   AND role1.MaDinhDanh IN ('TRUONG_PHONG','PHO_PHONG')
                   AND role2.MaDinhDanh IN ('TRUONG_PHONG','PHO_PHONG')
                   AND (role1.MaDinhDanh<>'TRUONG_PHONG' OR role2.MaDinhDanh='PHO_PHONG'))
                OR (@stage='PHE_DUYET_LANH_DAO'
                   AND role1.MaDinhDanh IN ('LANH_DAO','PHO_LANH_DAO')
                   AND role2.MaDinhDanh IN ('LANH_DAO','PHO_LANH_DAO')
                   AND (role1.MaDinhDanh<>'LANH_DAO' OR role2.MaDinhDanh='PHO_LANH_DAO')))",
            new{agency,owner=task.MaNguoiDuocGiao,delegateId,stage=task.Buoc},tx);
        if(allowed==0)return false;
        var signed=await cn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM dbo.ChuKySo s
            JOIN dbo.BanPdfDaKy p ON p.MaBan=s.MaBanDaKy
            WHERE p.MaVanBan=@id AND p.SoPhienBan=@version AND s.MaNguoiKy=@delegateId",
            new{id=task.MaVanBan,version=task.PhienBanDuocDuyet,delegateId},tx);
        return signed==0;
    }

    public static void MapUyQuyenKy(this IEndpointRouteBuilder app)
    {
        var group=app.MapGroup("/api/v1/admin/signing-delegations").RequireAuthorization();
        // Lich su doc truc tiep tu bang 015, khong thay doi cac PDF da ky.
        group.MapGet("/history",async(HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"permissions.manage");
            if(error!=null)return error;
            using var cn=db.Open();
            var rows=await cn.QueryAsync(@"
                SELECT q.MaTacVu,q.MaVanBan,v.MaNoiBo,v.TieuDe,t.Buoc,
                       g.HoTen AS NguoiGoc,d.HoTen AS NguoiDuocUyQuyen,
                       a.HoTen AS NguoiCap,q.LyDo,q.HieuLucTu,q.HieuLucDen,
                       q.DaThuHoi,q.ThoiDiemThuHoi,s.ThoiDiemKy,
                       CASE WHEN s.MaVanBan IS NOT NULL THEN 'DA_KY'
                            WHEN q.DaThuHoi=1 THEN 'DA_THU_HOI'
                            WHEN q.HieuLucDen<=SYSUTCDATETIME() THEN 'HET_HAN'
                            WHEN t.TrangThai<>'CHO_XU_LY' THEN 'DA_KET_THUC'
                            ELSE 'DANG_HIEU_LUC' END AS TrangThaiUyQuyen
                FROM dbo.UyQuyenKyTacVu q
                JOIN dbo.TacVuXuLy t ON t.MaTacVu=q.MaTacVu AND t.MaCoQuan=q.MaCoQuan
                JOIN dbo.VanBan v ON v.MaVanBan=q.MaVanBan AND v.MaCoQuan=q.MaCoQuan
                JOIN dbo.NguoiDung g ON g.MaNguoiDung=q.MaNguoiGoc
                JOIN dbo.NguoiDung d ON d.MaNguoiDung=q.MaNguoiDuocUyQuyen
                JOIN dbo.NguoiDung a ON a.MaNguoiDung=q.MaNguoiCap
                OUTER APPLY (SELECT TOP 1 s.ThoiDiemKy,s.MaVanBan FROM dbo.ChuKySo s
                    WHERE s.MaVanBan=q.MaVanBan AND s.MaCoQuan=q.MaCoQuan
                      AND s.SoPhienBan=t.PhienBanDuocDuyet AND s.MaNguoiKy=q.MaNguoiDuocUyQuyen
                      AND ((t.Buoc='KIEM_TRA_NOI_DUNG' AND s.LoaiChuKy='KY_NHAY')
                        OR (t.Buoc='PHE_DUYET_LANH_DAO' AND s.LoaiChuKy='KY_LANH_DAO'))
                    ORDER BY s.ThoiDiemKy DESC) s
                WHERE q.MaCoQuan=@agency
                ORDER BY q.HieuLucTu DESC,q.MaTacVu",
                new{agency=actor!.MaCoQuan});
            return Results.Ok(rows);
        });
        group.MapGet("/tasks",async(HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"permissions.manage");
            if(error!=null)return error;
            using var cn=db.Open();
            var tasks=await cn.QueryAsync(@"
                SELECT t.MaTacVu,t.MaVanBan,t.Buoc,t.MaNguoiDuocGiao,
                       v.TieuDe,v.SoBuocKy,u.HoTen AS NguoiDuocGiao,
                       q.MaNguoiDuocUyQuyen,q.HieuLucDen,q.DaThuHoi
                FROM dbo.TacVuXuLy t JOIN dbo.VanBan v ON v.MaVanBan=t.MaVanBan
                JOIN dbo.NguoiDung u ON u.MaNguoiDung=t.MaNguoiDuocGiao
                LEFT JOIN dbo.UyQuyenKyTacVu q ON q.MaTacVu=t.MaTacVu
                WHERE t.MaCoQuan=@agency AND t.TrangThai='CHO_XU_LY'
                  AND v.TrangThai='CHO_DUYET'
                  AND t.PhienBanDuocDuyet=v.PhienBanHienTai
                  AND t.Buoc IN ('KIEM_TRA_NOI_DUNG','PHE_DUYET_LANH_DAO')
                ORDER BY t.ThoiDiemTao DESC",new{agency=actor!.MaCoQuan});
            return Results.Ok(tasks);
        });

        group.MapGet("/tasks/{taskId:guid}/candidates",async(Guid taskId,HttpContext http,
            PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"permissions.manage");
            if(error!=null)return error;
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction();
            var task=await cn.QuerySingleOrDefaultAsync<TacVuKyUyQuyen>(@"
                SELECT t.MaTacVu,t.MaVanBan,t.MaNguoiDuocGiao,t.PhienBanDuocDuyet,t.Buoc,t.TrangThai,
                       v.SoBuocKy,v.MaNguoiTao FROM dbo.TacVuXuLy t
                JOIN dbo.VanBan v ON v.MaVanBan=t.MaVanBan
                WHERE t.MaTacVu=@taskId AND t.MaCoQuan=@agency AND t.TrangThai='CHO_XU_LY'
                  AND t.PhienBanDuocDuyet=v.PhienBanHienTai AND v.TrangThai='CHO_DUYET'",
                new{taskId,agency=actor!.MaCoQuan},tx);
            if(task==null){tx.Rollback();return Results.NotFound();}
            var users=await cn.QueryAsync<(int MaNguoiDung,string HoTen)>(@"
                SELECT n.MaNguoiDung,n.HoTen FROM dbo.NguoiDung n
                WHERE n.MaCoQuan=@agency AND n.DangHoatDong=1 AND n.MaNguoiDung<>@owner
                ORDER BY n.HoTen",new{agency=actor.MaCoQuan,owner=task.MaNguoiDuocGiao},tx);
            var eligible=new List<object>();
            foreach(var u in users)
                if(await DuDieuKien(cn,tx,actor.MaCoQuan!.Value,task,u.MaNguoiDung))
                    eligible.Add(new{u.MaNguoiDung,u.HoTen});
            tx.Commit();return Results.Ok(eligible);
        });

        group.MapPost("",async(TaoUyQuyenKy body,HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"permissions.manage");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            if(body.SoGioHieuLuc is < 1 or > 720 ||
               string.IsNullOrWhiteSpace(body.LyDo) || body.LyDo.Trim().Length is < 10 or > 500)
                return Results.BadRequest(new{message="Thời hạn 1–720 giờ, lý do 10–500 ký tự."});
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var task=await cn.QuerySingleOrDefaultAsync<TacVuKyUyQuyen>(@"
                SELECT t.MaTacVu,t.MaVanBan,t.MaNguoiDuocGiao,t.PhienBanDuocDuyet,t.Buoc,t.TrangThai,
                       v.SoBuocKy,v.MaNguoiTao FROM dbo.TacVuXuLy t WITH(UPDLOCK,HOLDLOCK)
                JOIN dbo.VanBan v ON v.MaVanBan=t.MaVanBan
                WHERE t.MaTacVu=@taskId AND t.MaCoQuan=@agency AND t.TrangThai='CHO_XU_LY'
                  AND v.TrangThai='CHO_DUYET' AND t.PhienBanDuocDuyet=v.PhienBanHienTai
                  AND t.Buoc IN ('KIEM_TRA_NOI_DUNG','PHE_DUYET_LANH_DAO')",
                new{taskId=body.MaTacVu,agency=actor.MaCoQuan},tx);
            if(task==null){tx.Rollback();return Results.Conflict(new{message="Tác vụ không còn đang chờ ký."});}
            var signedStage=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM dbo.ChuKySo s
                WHERE s.MaVanBan=@id AND s.MaCoQuan=@agency AND s.SoPhienBan=@version
                  AND s.LoaiChuKy=@signature",
                new{id=task.MaVanBan,agency=actor.MaCoQuan,version=task.PhienBanDuocDuyet,
                    signature=task.Buoc=="KIEM_TRA_NOI_DUNG"?"KY_NHAY":"KY_LANH_DAO"},tx);
            if(signedStage>0){tx.Rollback();return Results.Conflict(new{message="Bước này đã có chữ ký PDF; không thể cấp ủy quyền sau khi ký."});}
            if(!await DuDieuKien(cn,tx,actor.MaCoQuan.Value,task,body.MaNguoiDuocUyQuyen)){
                tx.Rollback();return Results.BadRequest(new{message="Người ký thay không đủ chức vụ, không cùng phòng hoặc đã ký văn bản."});
            }
            var existing=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM dbo.UyQuyenKyTacVu WHERE MaTacVu=@taskId",
                new{taskId=body.MaTacVu},tx);
            if(existing>0){tx.Rollback();return Results.Conflict(new{message="Đã có lịch sử ủy quyền tác vụ này. Hãy thu hồi rồi dùng tác vụ khác; không ghi đè lịch sử."});}
            var until=DateTime.UtcNow.AddHours(body.SoGioHieuLuc);
            await cn.ExecuteAsync(@"
                INSERT dbo.UyQuyenKyTacVu(MaTacVu,MaVanBan,MaCoQuan,MaNguoiGoc,MaNguoiDuocUyQuyen,
                    MaNguoiCap,LyDo,HieuLucDen)
                VALUES(@taskId,@id,@agency,@owner,@delegateId,@admin,@reason,@until)",
                new{taskId=body.MaTacVu,id=task.MaVanBan,agency=actor.MaCoQuan,
                    owner=task.MaNguoiDuocGiao,delegateId=body.MaNguoiDuocUyQuyen,
                    admin=actor.MaNguoiDung,reason=body.LyDo.Trim(),until},tx);
            await cn.ExecuteAsync(@"
                INSERT dbo.LichSuVanBan(MaVanBan,MaCoQuan,MaNguoiThucHien,HanhDong,MoTa,SoPhienBan)
                VALUES(@id,@agency,@admin,'UY_QUYEN_KY',@description,@version)",
                new{id=task.MaVanBan,agency=actor.MaCoQuan,admin=actor.MaNguoiDung,
                    description=$"Ủy quyền bước {task.Buoc} từ người {task.MaNguoiDuocGiao} sang người {body.MaNguoiDuocUyQuyen} đến {until:O}; {body.LyDo.Trim()}",
                    version=task.PhienBanDuocDuyet},tx);
            tx.Commit();return Results.Ok(new{maTacVu=body.MaTacVu,hieuLucDen=until});
        });

        group.MapPost("/{taskId:guid}/revoke",async(Guid taskId,HttpContext http,
            PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"permissions.manage");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var row=await cn.QuerySingleOrDefaultAsync<BanGhiUyQuyen>(@"
                SELECT q.MaVanBan,t.PhienBanDuocDuyet FROM dbo.UyQuyenKyTacVu q WITH(UPDLOCK,HOLDLOCK)
                JOIN dbo.TacVuXuLy t ON t.MaTacVu=q.MaTacVu
                WHERE q.MaTacVu=@taskId AND q.MaCoQuan=@agency AND q.DaThuHoi=0",
                new{taskId,agency=actor.MaCoQuan},tx);
            if(row==null){tx.Rollback();return Results.NotFound();}
            var signed=await cn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(*) FROM dbo.UyQuyenKyTacVu q
                JOIN dbo.TacVuXuLy t ON t.MaTacVu=q.MaTacVu
                JOIN dbo.ChuKySo s ON s.MaVanBan=q.MaVanBan AND s.SoPhienBan=t.PhienBanDuocDuyet
                  AND s.MaNguoiKy=q.MaNguoiDuocUyQuyen
                WHERE q.MaTacVu=@taskId AND q.MaCoQuan=@agency AND
                  ((t.Buoc='KIEM_TRA_NOI_DUNG' AND s.LoaiChuKy='KY_NHAY')
                  OR (t.Buoc='PHE_DUYET_LANH_DAO' AND s.LoaiChuKy='KY_LANH_DAO'))",
                new{taskId,agency=actor.MaCoQuan},tx);
            if(signed>0){tx.Rollback();return Results.Conflict(new{message="Người được ủy quyền đã ký PDF; không thể thu hồi sau chữ ký. Hãy hoàn tất bước duyệt."});}
            await cn.ExecuteAsync(@"UPDATE dbo.UyQuyenKyTacVu SET DaThuHoi=1,ThoiDiemThuHoi=SYSUTCDATETIME()
                WHERE MaTacVu=@taskId AND MaCoQuan=@agency",new{taskId,agency=actor.MaCoQuan},tx);
            await cn.ExecuteAsync(@"
                INSERT dbo.LichSuVanBan(MaVanBan,MaCoQuan,MaNguoiThucHien,HanhDong,MoTa,SoPhienBan)
                VALUES(@id,@agency,@admin,'THU_HOI_UY_QUYEN',N'Đã thu hồi ủy quyền ký tác vụ',@version)",
                new{id=row.MaVanBan,agency=actor.MaCoQuan,admin=actor.MaNguoiDung,
                    version=row.PhienBanDuocDuyet},tx);
            tx.Commit();return Results.Ok(new{maTacVu=taskId,daThuHoi=true});
        });
    }
}
