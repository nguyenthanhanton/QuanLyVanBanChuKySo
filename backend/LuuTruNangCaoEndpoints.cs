using Dapper;

public sealed record DeNghiBaoQuan(int? SoNamBaoQuan,string LyDo);
public sealed record DeNghiKhaiThac(string LyDo);
public sealed record DuyetBaoQuan(bool ChapThuan,string? GhiChu);
public sealed record DuyetKhaiThac(bool ChapThuan,int? SoGio,string? GhiChu);
public sealed class ThoiHanDuyet { public Guid MaHoSo {get;set;} public int? SoNamDeXuat {get;set;} }
public sealed class TepKiemTra { public Guid MaTep {get;set;} public string DuongDanLuuTru {get;set;}="";
    public string MaBamSha256 {get;set;}=""; public string TenTep {get;set;}=""; }

public static class LuuTruNangCaoEndpoints
{
    private static async Task<(NguoiThucHien? Actor,bool Clerk,bool Admin,IResult? Error)> Quyen(
        HttpContext http,PhanQuyen auth,Database db)
    {
        var(actor,error)=await auth.YeuCau(http,"dossiers.manage");
        if(error!=null)return(null,false,false,error);
        if(actor!.MaCoQuan==null)return(null,false,false,Results.Forbid());
        var capabilities=await auth.DanhSachQuyen(actor);
        using var cn=db.Open();
        var clerk=await cn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM dbo.NguoiDungVaiTro r
            JOIN dbo.VaiTro v ON v.MaVaiTro=r.MaVaiTro WHERE r.MaNguoiDung=@user
            AND r.MaCoQuan=@agency AND v.MaDinhDanh='VAN_THU'",
            new{user=actor.MaNguoiDung,agency=actor.MaCoQuan})>0;
        return(actor,clerk,capabilities.Contains("users.manage"),null);
    }

    private const string DaNhan=@"EXISTS(SELECT 1 FROM dbo.LuotNopLuuHoSo l
        WHERE l.MaHoSo=h.MaHoSo AND l.MaCoQuan=h.MaCoQuan AND l.TrangThai='DA_TIEP_NHAN')";

    public static void MapLuuTruNangCao(this IEndpointRouteBuilder app)
    {
        var route=app.MapGroup("/api/v1/archive").RequireAuthorization();

        route.MapGet("/search",async(HttpContext http,PhanQuyen auth,Database db,string? timKiem,
            int? nam,int? maPhongBan,string? loai,string? nguoiTao,string? nguoiNhan,string? tinhTrang)=>{
            var(actor,clerk,admin,error)=await Quyen(http,auth,db);if(error!=null)return error;
            using var cn=db.Open();
            var rows=await cn.QueryAsync(@"
                SELECT h.MaHoSo,h.MaHoSoNoiBo,h.TenHoSo,h.LoaiHoSo,h.NamHoSo,h.MaPhongBan,
                    p.TenPhongBan,h.SoNamBaoQuan,h.GhiChu,h.DaDong,l.MaBienNhan,l.ThoiDiemXuLy AS NgayTiepNhan,
                    n.HoTen AS NguoiLap,v.HoTen AS NguoiNhan,
                    CASE WHEN h.SoNamBaoQuan IS NULL THEN NULL
                         ELSE DATEADD(YEAR,h.SoNamBaoQuan,CAST(l.ThoiDiemXuLy AS DATE)) END AS NgayHetHan,
                    CASE WHEN h.SoNamBaoQuan IS NULL THEN 'VINH_VIEN'
                         WHEN DATEADD(YEAR,h.SoNamBaoQuan,l.ThoiDiemXuLy)<SYSUTCDATETIME() THEN 'HET_HAN'
                         WHEN DATEADD(YEAR,h.SoNamBaoQuan,l.ThoiDiemXuLy)<=DATEADD(DAY,90,SYSUTCDATETIME()) THEN 'SAP_HET_HAN'
                         ELSE 'CON_HAN' END AS TinhTrang,
                    (SELECT COUNT(*) FROM dbo.HoSoLuuTruVanBan hv WHERE hv.MaHoSo=h.MaHoSo) AS SoVanBan
                FROM dbo.HoSoLuuTru h JOIN dbo.LuotNopLuuHoSo l ON l.MaHoSo=h.MaHoSo
                    AND l.MaCoQuan=h.MaCoQuan AND l.TrangThai='DA_TIEP_NHAN'
                JOIN dbo.NguoiDung n ON n.MaNguoiDung=h.MaNguoiTao
                JOIN dbo.NguoiDung v ON v.MaNguoiDung=l.MaNguoiXuLy
                LEFT JOIN dbo.PhongBan p ON p.MaPhongBan=h.MaPhongBan AND p.MaCoQuan=h.MaCoQuan
                WHERE h.MaCoQuan=@agency AND (@clerk=1 OR @admin=1 OR h.MaNguoiTao=@user
                    OR h.MaPhongBan=(SELECT MaPhongBan FROM dbo.NguoiDung WHERE MaNguoiDung=@user))
                  AND (@timKiem IS NULL OR h.MaHoSoNoiBo LIKE '%'+@timKiem+'%' OR h.TenHoSo LIKE '%'+@timKiem+'%')
                  AND (@nam IS NULL OR h.NamHoSo=@nam) AND (@maPhongBan IS NULL OR h.MaPhongBan=@maPhongBan)
                  AND (@loai IS NULL OR h.LoaiHoSo LIKE '%'+@loai+'%')
                  AND (@nguoiTao IS NULL OR n.HoTen LIKE '%'+@nguoiTao+'%')
                  AND (@nguoiNhan IS NULL OR v.HoTen LIKE '%'+@nguoiNhan+'%')
                ORDER BY l.ThoiDiemXuLy DESC",
                new{agency=actor!.MaCoQuan,user=actor.MaNguoiDung,clerk=clerk?1:0,admin=admin?1:0,
                    timKiem,nam,maPhongBan,loai,nguoiTao,nguoiNhan});
            var data=rows.ToArray();
            if(tinhTrang!=null)data=data.Where(d=>string.Equals(Convert.ToString(d.TinhTrang),tinhTrang,StringComparison.Ordinal)).ToArray();
            return Results.Ok(data);
        });

        route.MapGet("/stats",async(HttpContext http,PhanQuyen auth,Database db)=>{
            var(actor,clerk,admin,error)=await Quyen(http,auth,db);if(error!=null)return error;
            using var cn=db.Open();
            var rows=(await cn.QueryAsync(@"
                SELECT h.NamHoSo,ISNULL(p.TenPhongBan,N'Toàn cơ quan') AS PhongBan,
                    COUNT(*) AS Tong, SUM(CASE WHEN h.SoNamBaoQuan IS NULL THEN 1 ELSE 0 END) AS VinhVien,
                    SUM(CASE WHEN h.SoNamBaoQuan IS NOT NULL
                      AND DATEADD(YEAR,h.SoNamBaoQuan,l.ThoiDiemXuLy)<SYSUTCDATETIME() THEN 1 ELSE 0 END) AS HetHan,
                    SUM(CASE WHEN h.SoNamBaoQuan IS NOT NULL
                      AND DATEADD(YEAR,h.SoNamBaoQuan,l.ThoiDiemXuLy)>=SYSUTCDATETIME()
                      AND DATEADD(YEAR,h.SoNamBaoQuan,l.ThoiDiemXuLy)<=DATEADD(DAY,90,SYSUTCDATETIME())
                      THEN 1 ELSE 0 END) AS SapHetHan
                FROM dbo.HoSoLuuTru h JOIN dbo.LuotNopLuuHoSo l ON l.MaHoSo=h.MaHoSo
                    AND l.MaCoQuan=h.MaCoQuan AND l.TrangThai='DA_TIEP_NHAN'
                LEFT JOIN dbo.PhongBan p ON p.MaPhongBan=h.MaPhongBan AND p.MaCoQuan=h.MaCoQuan
                WHERE h.MaCoQuan=@agency AND (@clerk=1 OR @admin=1 OR h.MaNguoiTao=@user
                    OR h.MaPhongBan=(SELECT MaPhongBan FROM dbo.NguoiDung WHERE MaNguoiDung=@user))
                GROUP BY h.NamHoSo,p.TenPhongBan ORDER BY h.NamHoSo DESC,p.TenPhongBan",
                new{agency=actor!.MaCoQuan,user=actor.MaNguoiDung,clerk=clerk?1:0,admin=admin?1:0})).ToArray();
            return Results.Ok(rows);
        });

        route.MapPost("/{id:guid}/retention",async(Guid id,DeNghiBaoQuan body,HttpContext http,PhanQuyen auth,Database db)=>{
            var(actor,_,admin,error)=await Quyen(http,auth,db);if(error!=null)return error;
            if(body.SoNamBaoQuan is <1 or >200 || string.IsNullOrWhiteSpace(body.LyDo)||body.LyDo.Length>1000)
                return Results.BadRequest(new{message="Nhập thời hạn 1–200 năm (để trống nếu vĩnh viễn) và lý do 1–1.000 ký tự."});
            using var cn=db.Open();await cn.OpenAsync();using var tx=cn.BeginTransaction();
            var exists=await cn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM dbo.HoSoLuuTru h WITH(UPDLOCK,HOLDLOCK)
                WHERE h.MaHoSo=@id AND h.MaCoQuan=@agency AND (h.MaNguoiTao=@user OR @admin=1) AND "+DaNhan,
                new{id,agency=actor!.MaCoQuan,user=actor.MaNguoiDung,admin=admin?1:0},tx);
            if(exists==0)return Results.NotFound();
            if(await cn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM dbo.DeXuatBaoQuanHoSo
                WHERE MaHoSo=@id AND TrangThai='CHO_DUYET'",new{id},tx)>0)
                return Results.Conflict(new{message="Đã có đề xuất đang chờ duyệt."});
            await cn.ExecuteAsync(@"INSERT dbo.DeXuatBaoQuanHoSo(MaHoSo,MaCoQuan,MaNguoiDeXuat,SoNamDeXuat,LyDo)
                VALUES(@id,@agency,@user,@years,@reason)",new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,
                    years=body.SoNamBaoQuan,reason=body.LyDo.Trim()},tx);
            tx.Commit();return Results.Ok(new{message="Đã gửi đề xuất bảo quản."});
        });

        route.MapGet("/retention",async(HttpContext http,PhanQuyen auth,Database db)=>{
            var(actor,clerk,admin,error)=await Quyen(http,auth,db);if(error!=null)return error;
            using var cn=db.Open();
            return Results.Ok(await cn.QueryAsync(@"SELECT x.MaDeXuat,x.MaHoSo,x.SoNamDeXuat,x.LyDo,x.TrangThai,
                x.ThoiDiemTao,x.ThoiDiemDuyet,x.GhiChuDuyet,h.MaHoSoNoiBo,h.TenHoSo,
                n.HoTen AS NguoiDeXuat,d.HoTen AS NguoiDuyet
                FROM dbo.DeXuatBaoQuanHoSo x JOIN dbo.HoSoLuuTru h ON h.MaHoSo=x.MaHoSo
                JOIN dbo.NguoiDung n ON n.MaNguoiDung=x.MaNguoiDeXuat
                LEFT JOIN dbo.NguoiDung d ON d.MaNguoiDung=x.MaNguoiDuyet
                WHERE x.MaCoQuan=@agency AND (@clerk=1 OR @admin=1 OR x.MaNguoiDeXuat=@user)
                ORDER BY x.MaDeXuat DESC",new{agency=actor!.MaCoQuan,user=actor.MaNguoiDung,
                    clerk=clerk?1:0,admin=admin?1:0}));
        });

        route.MapPost("/retention/{request:long}/decision",async(long request,DuyetBaoQuan body,
            HttpContext http,PhanQuyen auth,Database db)=>{
            var(actor,clerk,_,error)=await Quyen(http,auth,db);if(error!=null)return error;
            if(!clerk)return Results.Forbid();
            if(body.GhiChu?.Length>1000)return Results.BadRequest(new{message="Ghi chú tối đa 1.000 ký tự."});
            using var cn=db.Open();await cn.OpenAsync();using var tx=cn.BeginTransaction();
            var r=await cn.QuerySingleOrDefaultAsync<ThoiHanDuyet>(@"
                SELECT MaHoSo,SoNamDeXuat FROM dbo.DeXuatBaoQuanHoSo WITH(UPDLOCK,HOLDLOCK)
                WHERE MaDeXuat=@request AND MaCoQuan=@agency AND TrangThai='CHO_DUYET'",
                new{request,agency=actor!.MaCoQuan},tx);
            if(r==null)return Results.Conflict(new{message="Đề xuất đã xử lý hoặc không tồn tại."});
            if(body.ChapThuan)await cn.ExecuteAsync(@"UPDATE dbo.HoSoLuuTru SET SoNamBaoQuan=@years,
                ThoiDiemCapNhat=SYSUTCDATETIME() WHERE MaHoSo=@id AND MaCoQuan=@agency",
                new{years=r.SoNamDeXuat,id=r.MaHoSo,agency=actor.MaCoQuan},tx);
            await cn.ExecuteAsync(@"UPDATE dbo.DeXuatBaoQuanHoSo SET TrangThai=@status,MaNguoiDuyet=@user,
                ThoiDiemDuyet=SYSUTCDATETIME(),GhiChuDuyet=@note WHERE MaDeXuat=@request",
                new{status=body.ChapThuan?"DA_DUYET":"TU_CHOI",user=actor.MaNguoiDung,note=body.GhiChu,request},tx);
            await Ghi(cn,tx,r.MaHoSo,actor,body.ChapThuan?"GIA_HAN":"TU_CHOI_HAN","Xử lý đề xuất thời hạn bảo quản.");
            tx.Commit();return Results.Ok(new{message="Đã ghi nhận quyết định."});
        });

        route.MapPost("/{id:guid}/access",async(Guid id,DeNghiKhaiThac body,HttpContext http,PhanQuyen auth,Database db)=>{
            var(actor,_,_,error)=await Quyen(http,auth,db);if(error!=null)return error;
            if(string.IsNullOrWhiteSpace(body.LyDo)||body.LyDo.Length>1000)
                return Results.BadRequest(new{message="Nhập lý do khai thác từ 1–1.000 ký tự."});
            using var cn=db.Open();await cn.OpenAsync();using var tx=cn.BeginTransaction();
            var count=await cn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM dbo.HoSoLuuTru h WITH(UPDLOCK,HOLDLOCK)
                WHERE h.MaHoSo=@id AND h.MaCoQuan=@agency AND "+DaNhan,
                new{id,agency=actor!.MaCoQuan},tx);
            if(count==0)return Results.NotFound();
            var already=await cn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM dbo.YeuCauKhaiThacHoSo
                WHERE MaHoSo=@id AND MaNguoiYeuCau=@user AND (TrangThai='CHO_DUYET'
                  OR (TrangThai='DA_DUYET' AND HieuLucDen>SYSUTCDATETIME()))",
                new{id,user=actor.MaNguoiDung},tx);
            if(already!=0)return Results.Conflict(new{message="Bạn đã có yêu cầu chờ duyệt hoặc đang có hiệu lực."});
            await cn.ExecuteAsync(@"INSERT dbo.YeuCauKhaiThacHoSo(MaHoSo,MaCoQuan,MaNguoiYeuCau,LyDo)
                VALUES(@id,@agency,@user,@reason)",new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,
                    reason=body.LyDo.Trim()},tx);
            tx.Commit();return Results.Ok(new{message="Đã gửi yêu cầu khai thác."});
        });

        route.MapGet("/access",async(HttpContext http,PhanQuyen auth,Database db)=>{
            var(actor,clerk,admin,error)=await Quyen(http,auth,db);if(error!=null)return error;
            using var cn=db.Open();
            return Results.Ok(await cn.QueryAsync(@"SELECT q.MaYeuCau,q.MaHoSo,q.TrangThai,q.LyDo,
                q.ThoiDiemTao,q.ThoiDiemDuyet,q.HieuLucDen,q.GhiChuDuyet,
                h.MaHoSoNoiBo,h.TenHoSo,n.HoTen AS NguoiYeuCau,v.HoTen AS NguoiDuyet,
                q.MaNguoiYeuCau AS MaNguoiYeuCau
                FROM dbo.YeuCauKhaiThacHoSo q JOIN dbo.HoSoLuuTru h ON h.MaHoSo=q.MaHoSo
                JOIN dbo.NguoiDung n ON n.MaNguoiDung=q.MaNguoiYeuCau
                LEFT JOIN dbo.NguoiDung v ON v.MaNguoiDung=q.MaNguoiDuyet
                WHERE q.MaCoQuan=@agency AND (@clerk=1 OR @admin=1 OR q.MaNguoiYeuCau=@user)
                ORDER BY q.MaYeuCau DESC",new{agency=actor!.MaCoQuan,user=actor.MaNguoiDung,
                    clerk=clerk?1:0,admin=admin?1:0}));
        });

        route.MapPost("/access/{request:long}/decision",async(long request,DuyetKhaiThac body,
            HttpContext http,PhanQuyen auth,Database db)=>{
            var(actor,clerk,_,error)=await Quyen(http,auth,db);if(error!=null)return error;
            if(!clerk)return Results.Forbid();
            if(body.ChapThuan && (body.SoGio is null or <1 or >72))
                return Results.BadRequest(new{message="Quyền khai thác tối đa 72 giờ."});
            if(body.GhiChu?.Length>1000)return Results.BadRequest(new{message="Ghi chú tối đa 1.000 ký tự."});
            using var cn=db.Open();await cn.OpenAsync();using var tx=cn.BeginTransaction();
            var id=await cn.ExecuteScalarAsync<Guid?>(@"SELECT MaHoSo FROM dbo.YeuCauKhaiThacHoSo
                WITH(UPDLOCK,HOLDLOCK) WHERE MaYeuCau=@request AND MaCoQuan=@agency AND TrangThai='CHO_DUYET'",
                new{request,agency=actor!.MaCoQuan},tx);
            if(id==null)return Results.Conflict(new{message="Yêu cầu đã xử lý hoặc không tồn tại."});
            await cn.ExecuteAsync(@"UPDATE dbo.YeuCauKhaiThacHoSo SET TrangThai=@status,
                MaNguoiDuyet=@user,ThoiDiemDuyet=SYSUTCDATETIME(),
                HieuLucDen=CASE WHEN @approved=1 THEN DATEADD(HOUR,@hours,SYSUTCDATETIME()) ELSE NULL END,
                GhiChuDuyet=@note WHERE MaYeuCau=@request",
                new{status=body.ChapThuan?"DA_DUYET":"TU_CHOI",user=actor.MaNguoiDung,
                    approved=body.ChapThuan?1:0,hours=body.SoGio??0,note=body.GhiChu,request},tx);
            await Ghi(cn,tx,id.Value,actor,body.ChapThuan?"DUYET_KHAI_THAC":"TU_CHOI_KHAI_THAC",
                "Văn thư xử lý yêu cầu khai thác hồ sơ.");
            tx.Commit();return Results.Ok(new{message="Đã xử lý yêu cầu khai thác."});
        });

        route.MapPost("/access/{request:long}/revoke",async(long request,HttpContext http,
            PhanQuyen auth,Database db)=>{
            var(actor,clerk,_,error)=await Quyen(http,auth,db);if(error!=null)return error;
            if(!clerk)return Results.Forbid();
            using var cn=db.Open();await cn.OpenAsync();using var tx=cn.BeginTransaction();
            var id=await cn.ExecuteScalarAsync<Guid?>(@"SELECT MaHoSo FROM dbo.YeuCauKhaiThacHoSo
                WITH(UPDLOCK,HOLDLOCK) WHERE MaYeuCau=@request AND MaCoQuan=@agency AND TrangThai='DA_DUYET'",
                new{request,agency=actor!.MaCoQuan},tx);
            if(id==null)return Results.Conflict(new{message="Quyền đã thu hồi hoặc chưa được duyệt."});
            await cn.ExecuteAsync(@"UPDATE dbo.YeuCauKhaiThacHoSo SET TrangThai='THU_HOI',
                HieuLucDen=SYSUTCDATETIME(),MaNguoiDuyet=@user,ThoiDiemDuyet=SYSUTCDATETIME()
                WHERE MaYeuCau=@request",new{user=actor.MaNguoiDung,request},tx);
            await Ghi(cn,tx,id.Value,actor,"THU_HOI_KHAI_THAC","Thu hồi quyền khai thác hồ sơ.");
            tx.Commit();return Results.Ok(new{message="Đã thu hồi quyền."});
        });

        // A separate, checked route serves archive files; existing document endpoints remain unchanged.
        route.MapGet("/access/{request:long}/documents",async(long request,HttpContext http,
            PhanQuyen auth,Database db)=>{
            var(actor,_,_,error)=await Quyen(http,auth,db);if(error!=null)return error;
            using var cn=db.Open();
            var id=await HieuLuc(cn,request,actor!);if(id==null)return Results.Forbid();
            var docs=await cn.QueryAsync(@"SELECT v.MaVanBan,v.MaNoiBo,v.TieuDe,v.TrangThai
                FROM dbo.HoSoLuuTruVanBan hv JOIN dbo.VanBan v ON v.MaVanBan=hv.MaVanBan
                WHERE hv.MaHoSo=@id AND hv.MaCoQuan=@agency AND v.MaCoQuan=@agency
                  AND v.DoMat='THUONG' AND v.TrangThai IN ('DA_PHAT_HANH','DANG_CHUYEN','HOAN_THANH')",
                new{id,agency=actor!.MaCoQuan});
            await GhiKhaiThac(cn,request,actor,null,"XEM_DANH_SACH");
            return Results.Ok(docs);
        });

        route.MapGet("/access/{request:long}/files/{documentId:guid}",async(long request,Guid documentId,
            HttpContext http,PhanQuyen auth,Database db)=>{
            var(actor,_,_,error)=await Quyen(http,auth,db);if(error!=null)return error;
            using var cn=db.Open();
            var id=await HieuLuc(cn,request,actor!);if(id==null)return Results.Forbid();
            var allowed=await cn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM dbo.HoSoLuuTruVanBan hv
                JOIN dbo.VanBan v ON v.MaVanBan=hv.MaVanBan
                WHERE hv.MaHoSo=@id AND hv.MaVanBan=@documentId AND hv.MaCoQuan=@agency
                  AND v.MaCoQuan=@agency AND v.DoMat='THUONG'
                  AND v.TrangThai IN ('DA_PHAT_HANH','DANG_CHUYEN','HOAN_THANH')",
                new{id,documentId,agency=actor!.MaCoQuan});
            if(allowed!=1)return Results.NotFound();
            var file=await cn.QuerySingleOrDefaultAsync<BanPdf>(@"SELECT TOP 1 MaBan,DuongDanLuuTru,MaBamSha256
                FROM dbo.BanPdfDaKy WHERE MaVanBan=@documentId AND MaCoQuan=@agency
                ORDER BY ThuTuKy DESC",new{documentId,agency=actor.MaCoQuan});
            if(file==null||!File.Exists(file.DuongDanLuuTru))return Results.NotFound();
            if(!string.Equals(await DichVuKyPdf.Bam(file.DuongDanLuuTru),file.MaBamSha256,StringComparison.OrdinalIgnoreCase))
                return Results.Conflict(new{message="Tệp PDF đã thay đổi so với mã băm lưu trữ."});
            await GhiKhaiThac(cn,request,actor,documentId,"TAI_PDF");
            return Results.File(file.DuongDanLuuTru,"application/pdf",$"ho-so-{documentId:N}.pdf");
        });

        route.MapGet("/access/{request:long}/attachments/{documentId:guid}",async(long request,Guid documentId,
            HttpContext http,PhanQuyen auth,Database db)=>{
            var(actor,_,_,error)=await Quyen(http,auth,db);if(error!=null)return error;
            using var cn=db.Open();
            var dossier=await HieuLuc(cn,request,actor!);if(dossier==null)return Results.Forbid();
            if(!await TrongHoSo(cn,dossier.Value,documentId,actor!))return Results.NotFound();
            var rows=await cn.QueryAsync(@"SELECT f.MaTep,f.TenTep,f.LoaiNoiDung,f.KichThuoc
                FROM dbo.TepDinhKem f JOIN dbo.PhienBanVanBan pb ON pb.MaPhienBan=f.MaPhienBan
                JOIN dbo.VanBan v ON v.MaVanBan=pb.MaVanBan
                WHERE v.MaVanBan=@documentId AND v.MaCoQuan=@agency
                  AND pb.SoPhienBan=v.PhienBanHienTai AND f.LaVanBanChinh=0 AND f.MaCoQuan=@agency",
                new{documentId,agency=actor!.MaCoQuan});
            await GhiKhaiThac(cn,request,actor,documentId,"XEM_TEP_DINH_KEM");
            return Results.Ok(rows);
        });

        route.MapGet("/access/{request:long}/attachments/{documentId:guid}/{fileId:guid}",async(
            long request,Guid documentId,Guid fileId,HttpContext http,PhanQuyen auth,Database db)=>{
            var(actor,_,_,error)=await Quyen(http,auth,db);if(error!=null)return error;
            using var cn=db.Open();
            var dossier=await HieuLuc(cn,request,actor!);if(dossier==null)return Results.Forbid();
            if(!await TrongHoSo(cn,dossier.Value,documentId,actor!))return Results.NotFound();
            var file=await cn.QuerySingleOrDefaultAsync<TepKiemTra>(@"SELECT f.MaTep,f.TenTep,f.DuongDanLuuTru,f.MaBamSha256
                FROM dbo.TepDinhKem f JOIN dbo.PhienBanVanBan pb ON pb.MaPhienBan=f.MaPhienBan
                JOIN dbo.VanBan v ON v.MaVanBan=pb.MaVanBan
                WHERE v.MaVanBan=@documentId AND v.MaCoQuan=@agency
                  AND pb.SoPhienBan=v.PhienBanHienTai AND f.MaTep=@fileId
                  AND f.LaVanBanChinh=0 AND f.MaCoQuan=@agency",
                new{documentId,fileId,agency=actor!.MaCoQuan});
            if(file==null||!File.Exists(file.DuongDanLuuTru))return Results.NotFound();
            if(!string.Equals(await DichVuKyPdf.Bam(file.DuongDanLuuTru),file.MaBamSha256,StringComparison.OrdinalIgnoreCase))
                return Results.Conflict(new{message="Tệp đính kèm không còn đúng mã băm SHA-256."});
            await GhiKhaiThac(cn,request,actor,documentId,"TAI_TEP_DINH_KEM");
            return Results.File(file.DuongDanLuuTru,"application/octet-stream",file.TenTep);
        });

        route.MapGet("/{id:guid}/integrity",async(Guid id,HttpContext http,PhanQuyen auth,
            Database db,DichVuKyPdf signer)=>{
            var(actor,clerk,admin,error)=await Quyen(http,auth,db);if(error!=null)return error;
            using var cn=db.Open();
            var can=await cn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM dbo.HoSoLuuTru h
                WHERE h.MaHoSo=@id AND h.MaCoQuan=@agency AND (h.MaNguoiTao=@user OR @clerk=1 OR @admin=1)
                  AND "+DaNhan,new{id,agency=actor!.MaCoQuan,user=actor.MaNguoiDung,clerk=clerk?1:0,admin=admin?1:0});
            if(can==0)return Results.NotFound();
            var files=await cn.QueryAsync<BanPdf>(@"SELECT p.MaBan,p.DuongDanLuuTru,p.MaBamSha256
                FROM dbo.HoSoLuuTruVanBan hv JOIN dbo.VanBan v ON v.MaVanBan=hv.MaVanBan
                JOIN dbo.BanPdfDaKy p ON p.MaVanBan=v.MaVanBan AND p.MaCoQuan=v.MaCoQuan
                WHERE hv.MaHoSo=@id AND hv.MaCoQuan=@agency AND v.DoMat='THUONG'
                  AND p.ThuTuKy=(SELECT MAX(p2.ThuTuKy) FROM dbo.BanPdfDaKy p2
                     WHERE p2.MaVanBan=p.MaVanBan AND p2.MaCoQuan=p.MaCoQuan)",new{id,agency=actor.MaCoQuan});
            var documents=await cn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM dbo.HoSoLuuTruVanBan hv
                WHERE hv.MaHoSo=@id AND hv.MaCoQuan=@agency",new{id,agency=actor.MaCoQuan});
            var attachments=await cn.QueryAsync<TepKiemTra>(@"
                SELECT f.MaTep,f.DuongDanLuuTru,f.MaBamSha256 FROM dbo.HoSoLuuTruVanBan hv
                JOIN dbo.VanBan v ON v.MaVanBan=hv.MaVanBan
                JOIN dbo.PhienBanVanBan pb ON pb.MaVanBan=v.MaVanBan AND pb.SoPhienBan=v.PhienBanHienTai
                JOIN dbo.TepDinhKem f ON f.MaPhienBan=pb.MaPhienBan AND f.MaCoQuan=v.MaCoQuan
                WHERE hv.MaHoSo=@id AND hv.MaCoQuan=@agency AND v.DoMat='THUONG' AND f.LaVanBanChinh=0",
                new{id,agency=actor.MaCoQuan});
            var checks=new List<object>();var ok=0;var bad=0;
            foreach(var file in files){
                var exists=File.Exists(file.DuongDanLuuTru);
                var hash=exists&&string.Equals(await DichVuKyPdf.Bam(file.DuongDanLuuTru),file.MaBamSha256,StringComparison.OrdinalIgnoreCase);
                bool? signature=null;
                if(hash){try{var signatures=signer.XacMinh(file.DuongDanLuuTru);
                    signature=signatures.Count>0&&signatures.All(s=>s.ToanVenMatMa);
                }catch{signature=false;}}
                if(hash&&signature==true)ok++;else bad++;
                checks.Add(new{file.MaBan,CoTep=exists,MaBamDung=hash,ChuKyToanVen=signature});
            }
            foreach(var file in attachments){
                var exists=File.Exists(file.DuongDanLuuTru);
                var hash=exists&&string.Equals(await DichVuKyPdf.Bam(file.DuongDanLuuTru),file.MaBamSha256,StringComparison.OrdinalIgnoreCase);
                if(hash)ok++;else bad++;
                checks.Add(new{MaBan=file.MaTep,CoTep=exists,MaBamDung=hash,ChuKyToanVen=(bool?)null});
            }
            var missing=Math.Max(0,documents-files.Count());
            bad+=missing;
            return Results.Ok(new{Tong=ok+bad,HopLe=ok,CanKiemTra=bad,ThieuPdfDaKy=missing,KetQua=checks,
                GhiChu="Kiểm tra mã băm và toàn vẹn mật mã, không xác nhận pháp lý của CA."});
        });

        route.MapGet("/audit",async(HttpContext http,PhanQuyen auth,Database db)=>{
            var(actor,clerk,admin,error)=await Quyen(http,auth,db);if(error!=null)return error;
            using var cn=db.Open();
            return Results.Ok(await cn.QueryAsync(@"SELECT k.MaNhatKy,k.MaYeuCau,k.MaVanBan,k.HanhDong,
                k.ThoiDiem,n.HoTen AS NguoiThucHien,h.MaHoSoNoiBo
                FROM dbo.NhatKyKhaiThacHoSo k JOIN dbo.YeuCauKhaiThacHoSo q ON q.MaYeuCau=k.MaYeuCau
                JOIN dbo.HoSoLuuTru h ON h.MaHoSo=q.MaHoSo
                JOIN dbo.NguoiDung n ON n.MaNguoiDung=k.MaNguoiDung
                WHERE k.MaCoQuan=@agency AND (@clerk=1 OR @admin=1 OR q.MaNguoiYeuCau=@user)
                ORDER BY k.ThoiDiem DESC",new{agency=actor!.MaCoQuan,user=actor.MaNguoiDung,
                    clerk=clerk?1:0,admin=admin?1:0}));
        });
    }

    private static Task<int> Ghi(System.Data.IDbConnection cn,System.Data.IDbTransaction tx,Guid id,
        NguoiThucHien actor,string action,string message)=>cn.ExecuteAsync(@"
        INSERT dbo.LichSuHoSo(MaHoSo,MaCoQuan,MaNguoiThucHien,HanhDong,MoTa)
        VALUES(@id,@agency,@user,@action,@message)",new{id,agency=actor.MaCoQuan,user=actor.MaNguoiDung,action,message},tx);

    private static async Task<Guid?> HieuLuc(System.Data.IDbConnection cn,long request,NguoiThucHien actor)=>
        await cn.ExecuteScalarAsync<Guid?>(@"SELECT q.MaHoSo FROM dbo.YeuCauKhaiThacHoSo q
            WHERE q.MaYeuCau=@request AND q.MaCoQuan=@agency AND q.MaNguoiYeuCau=@user
              AND q.TrangThai='DA_DUYET' AND q.HieuLucDen>SYSUTCDATETIME()",
            new{request,agency=actor.MaCoQuan,user=actor.MaNguoiDung});

    private static Task<int> GhiKhaiThac(System.Data.IDbConnection cn,long request,
        NguoiThucHien actor,Guid? document,string action)=>cn.ExecuteAsync(@"
        INSERT dbo.NhatKyKhaiThacHoSo(MaYeuCau,MaCoQuan,MaNguoiDung,MaVanBan,HanhDong)
        VALUES(@request,@agency,@user,@document,@action)",new{request,agency=actor.MaCoQuan,
            user=actor.MaNguoiDung,document,action});

    private static async Task<bool> TrongHoSo(System.Data.IDbConnection cn,Guid dossier,Guid document,
        NguoiThucHien actor)=>await cn.ExecuteScalarAsync<int>(@"
        SELECT COUNT(*) FROM dbo.HoSoLuuTruVanBan hv JOIN dbo.VanBan v ON v.MaVanBan=hv.MaVanBan
        WHERE hv.MaHoSo=@dossier AND hv.MaVanBan=@document AND hv.MaCoQuan=@agency
          AND v.MaCoQuan=@agency AND v.DoMat='THUONG'
          AND v.TrangThai IN ('DA_PHAT_HANH','DANG_CHUYEN','HOAN_THANH')",
        new{dossier,document,agency=actor.MaCoQuan})>0;
}
