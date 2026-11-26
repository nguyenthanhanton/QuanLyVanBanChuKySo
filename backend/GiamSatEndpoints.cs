using System.IO.Compression;
using System.Text;
using System.Xml;
using Dapper;

public sealed record DanhDauThongBao(string LoaiThongBao,string MaThamChieu);

public static class GiamSatEndpoints
{
    private const string TacVuSql=@"
        SELECT t.MaTacVu,t.MaVanBan,v.MaNoiBo,
               CASE WHEN v.DoMat='THUONG' OR t.MaNguoiDuocGiao=@user
                    THEN v.TieuDe ELSE N'Văn bản hạn chế' END AS TieuDe,
               t.Buoc,t.TrangThai,t.HanXuLy,t.ThoiDiemTao,t.ThoiDiemHoanThanh,
               u.HoTen AS NguoiPhuTrach,p.TenPhongBan,
               d.HoTen AS NguoiKyThay,
               CASE WHEN t.TrangThai<>'CHO_XU_LY' THEN 'HOAN_THANH'
                    WHEN t.HanXuLy IS NULL THEN 'CHUA_DAT_HAN'
                    WHEN t.HanXuLy<SYSUTCDATETIME() THEN 'QUA_HAN'
                    WHEN t.HanXuLy<=DATEADD(HOUR,24,SYSUTCDATETIME()) THEN 'SAP_DEN_HAN'
                    ELSE 'TRONG_HAN' END AS TinhTrangHan,
               CASE WHEN t.TrangThai='CHO_XU_LY' THEN DATEDIFF(MINUTE,t.ThoiDiemTao,SYSUTCDATETIME())
                    ELSE DATEDIFF(MINUTE,t.ThoiDiemTao,t.ThoiDiemHoanThanh) END AS SoPhutXuLy
        FROM dbo.TacVuXuLy t
        JOIN dbo.VanBan v ON v.MaVanBan=t.MaVanBan AND v.MaCoQuan=t.MaCoQuan
        JOIN dbo.NguoiDung u ON u.MaNguoiDung=t.MaNguoiDuocGiao
        LEFT JOIN dbo.PhongBan p ON p.MaPhongBan=u.MaPhongBan AND p.MaCoQuan=u.MaCoQuan
        OUTER APPLY (SELECT TOP 1 n.HoTen FROM dbo.UyQuyenKyTacVu q
            JOIN dbo.NguoiDung n ON n.MaNguoiDung=q.MaNguoiDuocUyQuyen
            WHERE q.MaTacVu=t.MaTacVu AND q.MaCoQuan=@agency AND q.DaThuHoi=0
              AND q.HieuLucTu<=SYSUTCDATETIME() AND q.HieuLucDen>SYSUTCDATETIME()) d
        WHERE t.MaCoQuan=@agency
          AND (t.MaNguoiDuocGiao=@user OR EXISTS(
              SELECT 1 FROM dbo.UyQuyenKyTacVu q WHERE q.MaTacVu=t.MaTacVu
                AND q.MaCoQuan=@agency AND q.MaNguoiDuocUyQuyen=@user AND q.DaThuHoi=0
                AND q.HieuLucTu<=SYSUTCDATETIME() AND q.HieuLucDen>SYSUTCDATETIME())
              OR (@manager=1 AND v.DoMat='THUONG'))";

    public static void MapGiamSat(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/monitor/tasks",async(HttpContext http,PhanQuyen permission,Database db)=>{
            var actor=await permission.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            var caps=await permission.DanhSachQuyen(actor);
            if(!caps.Contains("workflow.queue")&&!caps.Contains("reports.read"))return Results.Forbid();
            using var cn=db.Open();
            var rows=await cn.QueryAsync(TacVuSql+" ORDER BY t.ThoiDiemTao DESC",
                new{agency=actor.MaCoQuan,user=actor.MaNguoiDung,manager=caps.Contains("reports.read")?1:0});
            return Results.Ok(rows);
        }).RequireAuthorization();

        app.MapGet("/api/v1/monitor/reports",async(HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"reports.read");
            if(error!=null)return error;
            using var cn=db.Open();
            // Chỉ thống kê văn bản THUONG; không tiết lộ metadata văn bản mật cho người xem báo cáo.
            var rows=(await cn.QueryAsync(@"
                SELECT u.HoTen AS NguoiXuLy,ISNULL(p.TenPhongBan,N'Chưa phân phòng') AS PhongBan,
                       COUNT(*) AS Tong, SUM(CASE WHEN t.TrangThai='CHO_XU_LY' THEN 1 ELSE 0 END) AS DangCho,
                       SUM(CASE WHEN t.TrangThai<>'CHO_XU_LY' THEN 1 ELSE 0 END) AS DaXuLy,
                       SUM(CASE WHEN t.TrangThai='CHO_XU_LY' AND t.HanXuLy<SYSUTCDATETIME()
                                THEN 1 ELSE 0 END) AS QuaHan,
                       SUM(CASE WHEN t.TrangThai<>'CHO_XU_LY' AND t.HanXuLy IS NOT NULL
                                   AND t.ThoiDiemHoanThanh<=t.HanXuLy THEN 1 ELSE 0 END) AS DungHan,
                       AVG(CASE WHEN t.ThoiDiemHoanThanh IS NOT NULL THEN
                         CONVERT(FLOAT,DATEDIFF(MINUTE,t.ThoiDiemTao,t.ThoiDiemHoanThanh)) END) AS PhutTrungBinh
                FROM dbo.TacVuXuLy t JOIN dbo.VanBan v ON v.MaVanBan=t.MaVanBan
                JOIN dbo.NguoiDung u ON u.MaNguoiDung=t.MaNguoiDuocGiao
                LEFT JOIN dbo.PhongBan p ON p.MaPhongBan=u.MaPhongBan AND p.MaCoQuan=u.MaCoQuan
                WHERE t.MaCoQuan=@agency AND v.DoMat='THUONG'
                GROUP BY u.MaNguoiDung,u.HoTen,p.TenPhongBan ORDER BY p.TenPhongBan,u.HoTen",
                new{agency=actor!.MaCoQuan})).ToArray();
            return Results.Ok(rows);
        }).RequireAuthorization();

        app.MapGet("/api/v1/monitor/reports/excel",async(HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"reports.read");
            if(error!=null)return error;
            using var cn=db.Open();
            var rows=(await cn.QueryAsync(@"
                SELECT ISNULL(p.TenPhongBan,N'Chưa phân phòng') AS PhongBan,
                       u.HoTen AS NguoiXuLy,COUNT(*) AS Tong,
                       SUM(CASE WHEN t.TrangThai='CHO_XU_LY' THEN 1 ELSE 0 END) AS DangCho,
                       SUM(CASE WHEN t.TrangThai<>'CHO_XU_LY' THEN 1 ELSE 0 END) AS DaXuLy,
                       SUM(CASE WHEN t.TrangThai='CHO_XU_LY' AND t.HanXuLy<SYSUTCDATETIME()
                           THEN 1 ELSE 0 END) AS QuaHan
                FROM dbo.TacVuXuLy t JOIN dbo.VanBan v ON v.MaVanBan=t.MaVanBan
                JOIN dbo.NguoiDung u ON u.MaNguoiDung=t.MaNguoiDuocGiao
                LEFT JOIN dbo.PhongBan p ON p.MaPhongBan=u.MaPhongBan AND p.MaCoQuan=u.MaCoQuan
                WHERE t.MaCoQuan=@agency AND v.DoMat='THUONG'
                GROUP BY p.TenPhongBan,u.HoTen ORDER BY p.TenPhongBan,u.HoTen",
                new{agency=actor!.MaCoQuan})).ToArray();
            var data=new List<string[]>{new[]{"Phòng ban","Người xử lý","Tổng tác vụ","Đang chờ","Đã xử lý","Quá hạn"}};
            foreach(var row in rows)data.Add(new string[]{
                Convert.ToString((object)row.PhongBan)??"",
                Convert.ToString((object)row.NguoiXuLy)??"",
                Convert.ToString((object)row.Tong)??"0",
                Convert.ToString((object)row.DangCho)??"0",
                Convert.ToString((object)row.DaXuLy)??"0",
                Convert.ToString((object)row.QuaHan)??"0"});
            return Results.File(TaoXlsx(data),"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "bao-cao-giam-sat-065.xlsx");
        }).RequireAuthorization();

        app.MapGet("/api/v1/monitor/notifications",async(HttpContext http,PhanQuyen permission,Database db)=>{
            var actor=await permission.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            using var cn=db.Open();
            var rows=await cn.QueryAsync(@"
                WITH N AS (
                  SELECT CONVERT(NVARCHAR(80),t.MaTacVu) AS MaThamChieu,CAST('TAC_VU' AS VARCHAR(35)) AS LoaiThongBao,
                         t.MaVanBan,v.MaNoiBo,
                         CAST(CASE WHEN t.MaNguoiDuocGiao=@user THEN N'Công việc được giao'
                                   ELSE N'Công việc được ủy quyền ký thay' END AS NVARCHAR(160)) AS TieuDe,
                         t.ThoiDiemTao AS ThoiDiem,
                         CAST(CASE WHEN t.HanXuLy<SYSUTCDATETIME() THEN N'QUA_HAN'
                                   WHEN t.HanXuLy<=DATEADD(HOUR,24,SYSUTCDATETIME()) THEN N'SAP_DEN_HAN'
                                   ELSE N'MOI' END AS VARCHAR(20)) AS MucDo
                  FROM dbo.TacVuXuLy t JOIN dbo.VanBan v ON v.MaVanBan=t.MaVanBan
                  WHERE t.MaCoQuan=@agency AND t.TrangThai='CHO_XU_LY'
                    AND (t.MaNguoiDuocGiao=@user OR EXISTS(SELECT 1 FROM dbo.UyQuyenKyTacVu q
                      WHERE q.MaTacVu=t.MaTacVu AND q.MaCoQuan=@agency AND q.MaNguoiDuocUyQuyen=@user
                        AND q.DaThuHoi=0 AND q.HieuLucTu<=SYSUTCDATETIME() AND q.HieuLucDen>SYSUTCDATETIME()))
                  UNION ALL
                  SELECT CONVERT(NVARCHAR(80),l.MaLichSu),'TRA_SUA',l.MaVanBan,v.MaNoiBo,
                         N'Văn bản bị trả sửa',l.ThoiDiem,'MOI'
                  FROM dbo.LichSuVanBan l JOIN dbo.VanBan v ON v.MaVanBan=l.MaVanBan
                  WHERE l.MaCoQuan=@agency AND l.HanhDong='TRA_SUA' AND v.MaNguoiTao=@user
                  UNION ALL
                  SELECT CONVERT(NVARCHAR(80),q.MaTacVu),'UY_QUYEN',q.MaVanBan,v.MaNoiBo,
                         N'Được cấp ủy quyền ký thay',q.HieuLucTu,
                         CASE WHEN q.DaThuHoi=1 THEN 'DA_THU_HOI'
                              WHEN q.HieuLucDen<SYSUTCDATETIME() THEN 'HET_HAN'
                              WHEN q.HieuLucDen<=DATEADD(HOUR,24,SYSUTCDATETIME()) THEN 'SAP_DEN_HAN'
                              ELSE 'MOI' END
                  FROM dbo.UyQuyenKyTacVu q JOIN dbo.VanBan v ON v.MaVanBan=q.MaVanBan
                  WHERE q.MaCoQuan=@agency AND q.MaNguoiDuocUyQuyen=@user
                  UNION ALL
                  SELECT CONVERT(NVARCHAR(80),q.MaTacVu),'THU_HOI',q.MaVanBan,v.MaNoiBo,
                         N'Ủy quyền ký thay đã bị thu hồi',q.ThoiDiemThuHoi,'DA_THU_HOI'
                  FROM dbo.UyQuyenKyTacVu q JOIN dbo.VanBan v ON v.MaVanBan=q.MaVanBan
                  WHERE q.MaCoQuan=@agency AND q.MaNguoiDuocUyQuyen=@user AND q.DaThuHoi=1
                    AND q.ThoiDiemThuHoi IS NOT NULL
                  UNION ALL
                  SELECT CONVERT(NVARCHAR(80),l.MaLichSu),'SU_KIEN',l.MaVanBan,v.MaNoiBo,
                         CASE WHEN l.HanhDong='PHAT_HANH' THEN N'Văn bản đã phát hành'
                              ELSE N'Văn bản đã được tiếp nhận' END,l.ThoiDiem,'MOI'
                  FROM dbo.LichSuVanBan l JOIN dbo.VanBan v ON v.MaVanBan=l.MaVanBan
                  WHERE l.MaCoQuan=@agency AND l.HanhDong IN ('PHAT_HANH','TIEP_NHAN')
                    AND (v.MaNguoiTao=@user OR (v.DoMat='THUONG' AND EXISTS(
                      SELECT 1 FROM dbo.NguoiDungVaiTro uv JOIN dbo.VaiTro r ON r.MaVaiTro=uv.MaVaiTro
                      WHERE uv.MaNguoiDung=@user AND uv.MaCoQuan=@agency AND r.MaDinhDanh='VAN_THU')))
                )
                SELECT TOP 100 N.*,CASE WHEN r.MaThamChieu IS NULL THEN CAST(0 AS BIT)
                       ELSE CAST(1 AS BIT) END AS DaDoc
                FROM N LEFT JOIN dbo.ThongBaoDaDoc r ON r.MaCoQuan=@agency AND r.MaNguoiDung=@user
                   AND r.LoaiThongBao=N.LoaiThongBao AND r.MaThamChieu=N.MaThamChieu
                ORDER BY N.ThoiDiem DESC",
                new{agency=actor.MaCoQuan,user=actor.MaNguoiDung});
            return Results.Ok(rows);
        }).RequireAuthorization();

        app.MapPost("/api/v1/monitor/notifications/read",async(DanhDauThongBao body,HttpContext http,
            PhanQuyen permission,Database db)=>{
            var actor=await permission.NguoiHienTai(http);
            if(actor?.MaCoQuan is null)return Results.Unauthorized();
            if(string.IsNullOrWhiteSpace(body.MaThamChieu) || body.MaThamChieu.Length>80 ||
               !new[]{"TAC_VU","TRA_SUA","UY_QUYEN","THU_HOI","SU_KIEN"}.Contains(body.LoaiThongBao))
                return Results.BadRequest();
            using var cn=db.Open();
            var valid=await cn.ExecuteScalarAsync<int>(@"
                SELECT CASE WHEN @type='TAC_VU' AND EXISTS(
                  SELECT 1 FROM dbo.TacVuXuLy t WHERE t.MaTacVu=TRY_CONVERT(UNIQUEIDENTIFIER,@id) AND t.MaCoQuan=@agency
                    AND t.TrangThai='CHO_XU_LY' AND (t.MaNguoiDuocGiao=@user OR EXISTS(
                      SELECT 1 FROM dbo.UyQuyenKyTacVu q WHERE q.MaTacVu=t.MaTacVu AND q.MaCoQuan=@agency
                        AND q.MaNguoiDuocUyQuyen=@user AND q.DaThuHoi=0
                        AND q.HieuLucTu<=SYSUTCDATETIME() AND q.HieuLucDen>SYSUTCDATETIME()))) THEN 1
                  WHEN @type='UY_QUYEN' AND EXISTS(SELECT 1 FROM dbo.UyQuyenKyTacVu q
                    WHERE q.MaTacVu=TRY_CONVERT(UNIQUEIDENTIFIER,@id) AND q.MaCoQuan=@agency AND q.MaNguoiDuocUyQuyen=@user) THEN 1
                  WHEN @type='THU_HOI' AND EXISTS(SELECT 1 FROM dbo.UyQuyenKyTacVu q
                    WHERE q.MaTacVu=TRY_CONVERT(UNIQUEIDENTIFIER,@id) AND q.MaCoQuan=@agency
                    AND q.MaNguoiDuocUyQuyen=@user AND q.DaThuHoi=1) THEN 1
                  WHEN @type='TRA_SUA' AND EXISTS(SELECT 1 FROM dbo.LichSuVanBan l
                    JOIN dbo.VanBan v ON v.MaVanBan=l.MaVanBan WHERE l.MaLichSu=TRY_CONVERT(BIGINT,@id)
                    AND l.MaCoQuan=@agency AND l.HanhDong='TRA_SUA' AND v.MaNguoiTao=@user) THEN 1
                  WHEN @type='SU_KIEN' AND EXISTS(SELECT 1 FROM dbo.LichSuVanBan l
                    JOIN dbo.VanBan v ON v.MaVanBan=l.MaVanBan WHERE l.MaLichSu=TRY_CONVERT(BIGINT,@id)
                    AND l.MaCoQuan=@agency AND l.HanhDong IN ('PHAT_HANH','TIEP_NHAN')
                    AND (v.MaNguoiTao=@user OR (v.DoMat='THUONG' AND EXISTS(
                      SELECT 1 FROM dbo.NguoiDungVaiTro uv JOIN dbo.VaiTro r ON r.MaVaiTro=uv.MaVaiTro
                      WHERE uv.MaNguoiDung=@user AND uv.MaCoQuan=@agency AND r.MaDinhDanh='VAN_THU')))) THEN 1
                  ELSE 0 END",
                new{id=body.MaThamChieu,type=body.LoaiThongBao,agency=actor.MaCoQuan,user=actor.MaNguoiDung});
            if(valid!=1)return Results.NotFound();
            await cn.ExecuteAsync(@"
                IF NOT EXISTS(SELECT 1 FROM dbo.ThongBaoDaDoc WHERE MaNguoiDung=@user AND MaCoQuan=@agency
                    AND LoaiThongBao=@type AND MaThamChieu=@id)
                  INSERT dbo.ThongBaoDaDoc(MaNguoiDung,MaCoQuan,LoaiThongBao,MaThamChieu)
                  VALUES(@user,@agency,@type,@id)",
                new{id=body.MaThamChieu,type=body.LoaiThongBao,agency=actor.MaCoQuan,user=actor.MaNguoiDung});
            return Results.Ok(new{daDoc=true});
        }).RequireAuthorization();
    }

    // Ghi XLSX Open XML tối giản, không dùng phụ thuộc hoặc macro bên ngoài.
    private static byte[] TaoXlsx(IReadOnlyList<string[]> rows)
    {
        using var buffer=new MemoryStream();
        using(var zip=new ZipArchive(buffer,ZipArchiveMode.Create,true)){
            void Add(string path,string value){using var stream=zip.CreateEntry(path).Open();
                using var writer=new StreamWriter(stream,new UTF8Encoding(false));writer.Write(value);}
            Add("[Content_Types].xml","<?xml version=\"1.0\" encoding=\"UTF-8\"?>"+
                "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"+
                "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"+
                "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"+
                "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"+
                "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
            Add("_rels/.rels","<?xml version=\"1.0\" encoding=\"UTF-8\"?>"+
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"+
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            Add("xl/workbook.xml","<?xml version=\"1.0\" encoding=\"UTF-8\"?>"+
                "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"+
                "<sheets><sheet name=\"Bao cao\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
            Add("xl/_rels/workbook.xml.rels","<?xml version=\"1.0\" encoding=\"UTF-8\"?>"+
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"+
                "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>");
            var builder=new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            for(var i=0;i<rows.Count;i++){
                builder.Append("<row r=\"").Append(i+1).Append("\">");
                foreach(var value in rows[i])builder.Append("<c t=\"inlineStr\"><is><t>")
                    .Append(System.Security.SecurityElement.Escape(value)).Append("</t></is></c>");
                builder.Append("</row>");
            }
            builder.Append("</sheetData></worksheet>");Add("xl/worksheets/sheet1.xml",builder.ToString());
        }
        return buffer.ToArray();
    }
}
