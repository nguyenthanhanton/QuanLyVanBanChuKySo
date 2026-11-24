using Dapper;

public sealed record GuiTrinhDuyet(Guid MaYeuCau);
public sealed record DuyetTacVu(string KetQua, string? LyDo, Guid MaYeuCau);
public sealed record CapSoVanBan(Guid MaVanBan, Guid MaYeuCau, string KyHieu);
public sealed record TraLoiGiaoNhan(string? LyDo, Guid MaYeuCau);
public sealed class TacVuDuocGiao {
    public Guid MaVanBan { get; set; }
    public string Buoc { get; set; } = "";
    public int PhienBanDuocDuyet { get; set; }
    public string TrangThai { get; set; } = "";
}
public sealed class VanBanNhan {
    public Guid MaVanBan { get; set; }
    public string TieuDe { get; set; } = "";
    public string TrangThai { get; set; } = "";
}

public static class QuyTrinhEndpoints
{
    private static async Task<int?> TimNguoiTheoVaiTro(System.Data.IDbConnection cn,
        System.Data.IDbTransaction tx,int coQuan,string vaiTro)
        => await cn.QueryFirstOrDefaultAsync<int?>(@"
            SELECT TOP 1 u.MaNguoiDung FROM dbo.NguoiDung u
            JOIN dbo.NguoiDungVaiTro uv ON uv.MaNguoiDung=u.MaNguoiDung AND uv.MaCoQuan=u.MaCoQuan
            JOIN dbo.VaiTro v ON v.MaVaiTro=uv.MaVaiTro
            WHERE u.MaCoQuan=@coQuan AND u.DangHoatDong=1 AND v.MaDinhDanh=@vaiTro
            ORDER BY u.MaNguoiDung",new{coQuan,vaiTro},tx);

    private static async Task<string?> BienNhanCu(System.Data.IDbConnection cn,
        System.Data.IDbTransaction tx,int coQuan,Guid request,string command)
        => await cn.QueryFirstOrDefaultAsync<string>(@"
            SELECT KetQuaJson FROM dbo.BienNhanLenh WITH (UPDLOCK,HOLDLOCK)
            WHERE MaCoQuan=@coQuan AND MaYeuCau=@request AND LoaiLenh=@command",
            new{coQuan,request,command},tx);

    private static async Task GhiBienNhan(System.Data.IDbConnection cn,
        System.Data.IDbTransaction tx,int agency,int actor,Guid req,string command,string result)
        => await cn.ExecuteAsync(@"
            INSERT dbo.BienNhanLenh(MaCoQuan,MaNguoiThucHien,MaYeuCau,LoaiLenh,KetQuaJson)
            VALUES(@agency,@actor,@req,@command,@result)",new{agency,actor,req,command,result},tx);

    public static void MapQuyTrinh(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/workqueue",async(HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"workflow.queue");
            if(error!=null)return error;
            using var cn=db.Open();
            var jobs=await cn.QueryAsync(@"
                SELECT t.MaTacVu,t.Buoc,t.TrangThai,t.HanXuLy,t.ThoiDiemTao,
                       v.MaVanBan,v.MaNoiBo,v.TieuDe,v.DoKhan,v.DoMat
                FROM dbo.TacVuXuLy t JOIN dbo.VanBan v ON v.MaVanBan=t.MaVanBan
                WHERE t.MaCoQuan=@agency AND t.MaNguoiDuocGiao=@user AND t.TrangThai='CHO_XU_LY'
                ORDER BY t.HanXuLy,t.ThoiDiemTao",
                new{agency=actor!.MaCoQuan,user=actor.MaNguoiDung});
            return Results.Ok(jobs);
        }).RequireAuthorization();

        app.MapPost("/api/v1/documents/{id:guid}/submit",async(Guid id,GuiTrinhDuyet body,
            HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"documents.submit");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            if(body.MaYeuCau==Guid.Empty)return Results.BadRequest(new{message="MaYeuCau bắt buộc."});
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var old=await BienNhanCu(cn,tx,actor.MaCoQuan.Value,body.MaYeuCau,"TRINH_DUYET");
            if(old!=null){tx.Commit();return Results.Content(old,"application/json");}
            var doc=await cn.QuerySingleOrDefaultAsync<BanGhiVanBan>(@"
                SELECT TrangThai,PhienBanHienTai FROM dbo.VanBan WITH (UPDLOCK)
                WHERE MaVanBan=@id AND MaCoQuan=@agency",new{id,agency=actor.MaCoQuan},tx);
            if(doc==null){tx.Rollback();return Results.NotFound();}
            if(doc.TrangThai!="NHAP" && doc.TrangThai!="TRA_SUA"){
                tx.Rollback();return Results.Conflict(new{message="Văn bản không ở trạng thái có thể trình duyệt."});
            }
            var approver=await TimNguoiTheoVaiTro(cn,tx,actor.MaCoQuan.Value,"TRUONG_PHONG");
            if(approver==null){tx.Rollback();return Results.Conflict(new{message="Chưa cấu hình trưởng phòng phê duyệt."});}
            var taskId=Guid.NewGuid();
            await cn.ExecuteAsync(@"
                INSERT dbo.TacVuXuLy(MaTacVu,MaVanBan,MaCoQuan,MaNguoiDuocGiao,Buoc,HanXuLy,PhienBanDuocDuyet)
                VALUES(@taskId,@id,@agency,@approver,'KIEM_TRA_NOI_DUNG',DATEADD(HOUR,48,SYSUTCDATETIME()),@ver)",
                new{taskId,id,agency=actor.MaCoQuan,approver,ver=doc.PhienBanHienTai},tx);
            await cn.ExecuteAsync("UPDATE dbo.VanBan SET TrangThai='CHO_DUYET',ThoiDiemCapNhat=SYSUTCDATETIME() WHERE MaVanBan=@id",new{id},tx);
            await cn.ExecuteAsync(@"
                INSERT dbo.LichSuVanBan(MaVanBan,MaCoQuan,MaNguoiThucHien,HanhDong,MoTa,TrangThaiTruoc,TrangThaiSau,SoPhienBan)
                VALUES(@id,@agency,@user,'TRINH_DUYET',N'Trình trưởng phòng kiểm tra nội dung',@before,'CHO_DUYET',@ver)",
                new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,before=doc.TrangThai,ver=doc.PhienBanHienTai},tx);
            var result=System.Text.Json.JsonSerializer.Serialize(new{maVanBan=id,maTacVu=taskId,trangThai="CHO_DUYET"});
            await GhiBienNhan(cn,tx,actor.MaCoQuan.Value,actor.MaNguoiDung,body.MaYeuCau,"TRINH_DUYET",result);
            tx.Commit();return Results.Content(result,"application/json");
        }).RequireAuthorization();

        app.MapPost("/api/v1/workflow/tasks/{taskId:guid}/decisions",async(Guid taskId,DuyetTacVu body,
            HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"workflow.decide");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            if(body.KetQua is not ("DUYET" or "TRA_SUA"))return Results.BadRequest(new{message="KetQua phải là DUYET hoặc TRA_SUA."});
            if(body.KetQua=="TRA_SUA" && string.IsNullOrWhiteSpace(body.LyDo))
                return Results.BadRequest(new{message="Phải nhập lý do trả sửa."});
            using var cn=db.Open();await cn.OpenAsync();using var tx=cn.BeginTransaction();
            var task=await cn.QuerySingleOrDefaultAsync<TacVuDuocGiao>(@"
                SELECT MaVanBan,Buoc,PhienBanDuocDuyet,TrangThai
                FROM dbo.TacVuXuLy WITH(UPDLOCK)
                WHERE MaTacVu=@taskId AND MaCoQuan=@agency AND MaNguoiDuocGiao=@actor",
                new{taskId,agency=actor.MaCoQuan,actor=actor.MaNguoiDung},tx);
            if(task==null){tx.Rollback();return Results.NotFound();}
            if(task.TrangThai!="CHO_XU_LY"){
                tx.Rollback();return Results.Conflict(new{message="Công việc đã được xử lý."});
            }
            var currentVer=await cn.ExecuteScalarAsync<int>("SELECT PhienBanHienTai FROM dbo.VanBan WHERE MaVanBan=@id",
                new{id=task.MaVanBan},tx);
            if(currentVer!=task.PhienBanDuocDuyet){tx.Rollback();return Results.Conflict(new{message="Phiên bản văn bản đã thay đổi."});}
            await cn.ExecuteAsync(@"
                UPDATE dbo.TacVuXuLy SET TrangThai=@status,ThoiDiemHoanThanh=SYSUTCDATETIME()
                WHERE MaTacVu=@taskId",new{taskId,status=body.KetQua},tx);
            await cn.ExecuteAsync(@"
                INSERT dbo.QuyetDinhXuLy(MaTacVu,MaNguoiQuyetDinh,KetQua,LyDo,SoPhienBan)
                VALUES(@taskId,@actor,@result,@reason,@ver)",
                new{taskId,actor=actor.MaNguoiDung,result=body.KetQua,reason=body.LyDo,ver=currentVer},tx);
            string nextStatus;
            string description;
            if(body.KetQua=="TRA_SUA"){
                nextStatus="TRA_SUA";
                description=$"Trả sửa: {body.LyDo!.Trim()}";
            } else {
                var nextStep=task.Buoc switch{
                    "KIEM_TRA_NOI_DUNG"=>"KIEM_TRA_THE_THUC",
                    "KIEM_TRA_THE_THUC"=>"PHE_DUYET_LANH_DAO",
                    _=>null
                };
                if(nextStep=="KIEM_TRA_THE_THUC"){
                    var useOffice=await cn.ExecuteScalarAsync<bool?>(@"
                        SELECT c.CoKiemTraVanPhong FROM dbo.VanBan v
                        LEFT JOIN dbo.CauHinhQuyTrinh c ON c.MaCauHinh=v.MaCauHinh
                        WHERE v.MaVanBan=@id",new{id=task.MaVanBan},tx);
                    if(useOffice==false)nextStep="PHE_DUYET_LANH_DAO";
                }
                if(nextStep!=null){
                    var role=nextStep=="KIEM_TRA_THE_THUC"?"TRUONG_VAN_PHONG":"LANH_DAO";
                    var nextUser=await TimNguoiTheoVaiTro(cn,tx,actor.MaCoQuan.Value,role);
                    if(nextUser==null){tx.Rollback();return Results.Conflict(new{message=$"Chưa có người đảm nhiệm vai trò {role}."});}
                    await cn.ExecuteAsync(@"
                        INSERT dbo.TacVuXuLy(MaVanBan,MaCoQuan,MaNguoiDuocGiao,Buoc,HanXuLy,PhienBanDuocDuyet)
                        VALUES(@id,@agency,@nextUser,@nextStep,DATEADD(HOUR,48,SYSUTCDATETIME()),@ver)",
                        new{id=task.MaVanBan,agency=actor.MaCoQuan,nextUser,nextStep,ver=currentVer},tx);
                    nextStatus="CHO_DUYET";
                    description="Đã duyệt và chuyển bước " + nextStep;
                }else{
                    nextStatus="CHO_CAP_SO";
                    description="Lãnh đạo phê duyệt; chờ văn thư cấp số";
                }
            }
            await cn.ExecuteAsync("UPDATE dbo.VanBan SET TrangThai=@status,ThoiDiemCapNhat=SYSUTCDATETIME() WHERE MaVanBan=@id",
                new{status=nextStatus,id=task.MaVanBan},tx);
            await cn.ExecuteAsync(@"
                INSERT dbo.LichSuVanBan(MaVanBan,MaCoQuan,MaNguoiThucHien,HanhDong,MoTa,TrangThaiSau,SoPhienBan)
                VALUES(@id,@agency,@user,@action,@description,@status,@ver)",
                new{id=task.MaVanBan,agency=actor.MaCoQuan,user=actor.MaNguoiDung,
                    action=body.KetQua,description,status=nextStatus,ver=currentVer},tx);
            tx.Commit();return Results.Ok(new{maVanBan=task.MaVanBan,trangThai=nextStatus});
        }).RequireAuthorization();

        app.MapPost("/api/v1/registrations",async(CapSoVanBan body,HttpContext http,
            PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"registers.manage");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            if(body.MaYeuCau==Guid.Empty || string.IsNullOrWhiteSpace(body.KyHieu))return Results.BadRequest();
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var old=await BienNhanCu(cn,tx,actor.MaCoQuan.Value,body.MaYeuCau,"CAP_SO");
            if(old!=null){tx.Commit();return Results.Content(old,"application/json");}
            var status=await cn.QuerySingleOrDefaultAsync<string>(@"
                SELECT TrangThai FROM dbo.VanBan WITH(UPDLOCK)
                WHERE MaVanBan=@id AND MaCoQuan=@agency",
                new{id=body.MaVanBan,agency=actor.MaCoQuan},tx);
            if(status==null){tx.Rollback();return Results.NotFound();}
            if(status!="CHO_CAP_SO"){
                tx.Rollback();return Results.Conflict(new{message="Chỉ cấp số sau khi lãnh đạo phê duyệt."});
            }
            var ngayVN=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).Date;
            var nam=ngayVN.Year;
            await cn.ExecuteAsync(@"
                IF NOT EXISTS(SELECT 1 FROM dbo.SoVanBan WITH(UPDLOCK,HOLDLOCK)
                              WHERE MaCoQuan=@agency AND Nam=@nam AND LoaiSo='DI')
                    INSERT dbo.SoVanBan(MaCoQuan,Nam,LoaiSo,TenSo,SoDaCap)
                    VALUES(@agency,@nam,'DI',N'Sổ văn bản đi',0)",new{agency=actor.MaCoQuan,nam},tx);
            var number=await cn.ExecuteScalarAsync<int>(@"
                UPDATE dbo.SoVanBan WITH(UPDLOCK) SET SoDaCap=SoDaCap+1
                OUTPUT INSERTED.SoDaCap
                WHERE MaCoQuan=@agency AND Nam=@nam AND LoaiSo='DI'",
                new{agency=actor.MaCoQuan,nam},tx);
            await cn.ExecuteAsync(@"
                UPDATE dbo.VanBan SET SoPhatHanh=@number,KyHieu=@symbol,NgayBanHanh=@date,
                    TrangThai='DA_CAP_SO',ThoiDiemCapNhat=SYSUTCDATETIME()
                WHERE MaVanBan=@id AND MaCoQuan=@agency",
                new{number,symbol=body.KyHieu.Trim(),date=ngayVN,id=body.MaVanBan,agency=actor.MaCoQuan},tx);
            await cn.ExecuteAsync(@"
                INSERT dbo.LichSuVanBan(MaVanBan,MaCoQuan,MaNguoiThucHien,HanhDong,MoTa,TrangThaiSau)
                VALUES(@id,@agency,@user,'CAP_SO',@message,'DA_CAP_SO')",
                new{id=body.MaVanBan,agency=actor.MaCoQuan,user=actor.MaNguoiDung,
                    message=$"Cấp số {number}/{body.KyHieu.Trim()} năm {nam}"},tx);
            var json=System.Text.Json.JsonSerializer.Serialize(new{maVanBan=body.MaVanBan,so=number,kyHieu=body.KyHieu,ngayBanHanh=ngayVN});
            await GhiBienNhan(cn,tx,actor.MaCoQuan.Value,actor.MaNguoiDung,body.MaYeuCau,"CAP_SO",json);
            tx.Commit();return Results.Content(json,"application/json");
        }).RequireAuthorization();

        app.MapGet("/api/v1/deliveries",async(HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"publications.deliver");
            if(error!=null)return error;
            using var cn=db.Open();
            var rows=await cn.QueryAsync(@"
                SELECT l.MaLuotChuyen,l.TrangThai,l.ThoiDiemGui,l.ThoiDiemNhan,
                       l.LyDoTuChoi,v.MaNoiBo,v.TieuDe,v.MaVanBan,
                       c.TenCoQuan AS CoQuanNhan
                FROM dbo.LuotChuyenVanBan l
                JOIN dbo.BanPhatHanh p ON p.MaPhatHanh=l.MaPhatHanh
                JOIN dbo.VanBan v ON v.MaVanBan=p.MaVanBan
                JOIN dbo.CoQuan c ON c.MaCoQuan=l.MaCoQuanNhan
                WHERE l.MaCoQuanGui=@agency ORDER BY l.ThoiDiemGui DESC",
                new{agency=actor!.MaCoQuan});
            return Results.Ok(rows);
        }).RequireAuthorization();

        app.MapGet("/api/v1/incoming",async(HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"incoming.manage");
            if(error!=null)return error;
            using var cn=db.Open();
            // Khong tra ve revision, nhan xet, tac vu noi bo cua co quan gui.
            var rows=await cn.QueryAsync(@"
                SELECT d.MaVanBanDen,d.SoDen,d.TieuDe,d.TrangThai,d.ThoiDiemTiepNhan,
                       l.MaLuotChuyen,l.ThoiDiemGui,c.TenCoQuan AS CoQuanGui
                FROM dbo.VanBanDen d
                LEFT JOIN dbo.LuotChuyenVanBan l ON l.MaLuotChuyen=d.MaLuotChuyen
                LEFT JOIN dbo.CoQuan c ON c.MaCoQuan=l.MaCoQuanGui
                WHERE d.MaCoQuanNhan=@agency ORDER BY d.ThoiDiemTiepNhan DESC",
                new{agency=actor!.MaCoQuan});
            return Results.Ok(rows);
        }).RequireAuthorization();

        app.MapPost("/api/v1/deliveries/{id:guid}/accept",async(Guid id,TraLoiGiaoNhan body,
            HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"deliveries.accept");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            using var cn=db.Open();await cn.OpenAsync();using var tx=cn.BeginTransaction();
            var row=await cn.QuerySingleOrDefaultAsync<VanBanNhan>(@"
                SELECT v.MaVanBan,v.TieuDe,l.TrangThai
                FROM dbo.LuotChuyenVanBan l WITH(UPDLOCK)
                JOIN dbo.BanPhatHanh p ON p.MaPhatHanh=l.MaPhatHanh
                JOIN dbo.VanBan v ON v.MaVanBan=p.MaVanBan
                WHERE l.MaLuotChuyen=@id AND l.MaCoQuanNhan=@agency",
                new{id,agency=actor.MaCoQuan},tx);
            if(row==null){tx.Rollback();return Results.NotFound();}
            if(row.TrangThai=="DA_TIEP_NHAN"){
                tx.Commit();return Results.Ok(new{message="Văn bản đã được tiếp nhận trước đó."});
            }
            if(row.TrangThai!="DA_GUI"){
                tx.Rollback();return Results.Conflict(new{message="Chưa có bản được gửi để tiếp nhận."});
            }
            await cn.ExecuteAsync(@"
                UPDATE dbo.LuotChuyenVanBan SET TrangThai='DA_TIEP_NHAN',MaNguoiNhan=@user,
                    ThoiDiemNhan=SYSUTCDATETIME() WHERE MaLuotChuyen=@id",
                new{id,user=actor.MaNguoiDung},tx);
            await cn.ExecuteAsync(@"
                INSERT dbo.VanBanDen(MaCoQuanNhan,MaLuotChuyen,MaNguoiTiepNhan,Nam,TieuDe)
                VALUES(@agency,@id,@user,YEAR(SYSUTCDATETIME()),@title)",
                new{agency=actor.MaCoQuan,id,user=actor.MaNguoiDung,title=row.TieuDe},tx);
            // Lich su giao nhan chung co the xem o ben gui; khong tra MaVanBanDen.
            await cn.ExecuteAsync(@"
                INSERT dbo.LichSuVanBan(MaVanBan,MaCoQuan,MaNguoiThucHien,MaLuotChuyen,HanhDong,MoTa,PhamViHienThi)
                VALUES(@doc,@agency,@user,@id,'TIEP_NHAN',N'Cơ quan nhận đã tiếp nhận văn bản','GIAO_NHAN')",
                new{doc=row.MaVanBan,agency=actor.MaCoQuan,user=actor.MaNguoiDung,id},tx);
            tx.Commit();return Results.Ok(new{trangThai="DA_TIEP_NHAN"});
        }).RequireAuthorization();
    }
}
