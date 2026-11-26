using Dapper;
using Microsoft.Data.SqlClient;

public sealed record TaoPhongBan(string MaDinhDanh,string TenPhongBan,int ThuTuHienThi,int? MaPhongBanCha);
public sealed record SuaPhongBan(string TenPhongBan,int ThuTuHienThi,int? MaPhongBanCha);
public sealed record TrangThaiPhongBan(bool DangHoatDong);

public static class QuanTriPhongBanEndpoints
{
    private sealed class PhongBanRow
    {
        public int MaPhongBan { get; set; }
        public string MaDinhDanh { get; set; }="";
        public bool DangHoatDong { get; set; }
    }

    private static bool HopLe(string name,int order) => name.Length is >= 2 and <= 200 && order is >= 0 and <= 9999;

    private static async Task<bool> ChaHopLe(SqlConnection cn,SqlTransaction tx,int agency,
        int? parent,int? self)
    {
        if(parent==null)return true;
        if(parent==self)return false;
        var found=await cn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM dbo.PhongBan WITH(UPDLOCK,HOLDLOCK)
            WHERE MaPhongBan=@parent AND MaCoQuan=@agency AND DangHoatDong=1",
            new{parent,agency},tx);
        if(found==0)return false;
        if(self==null)return true;
        // Ngăn vòng lặp A -> B -> ... -> A, kể cả nhiều cấp.
        var loop=await cn.ExecuteScalarAsync<int>(@"
            WITH Ancestors AS (
                SELECT MaPhongBan,MaPhongBanCha FROM dbo.PhongBan
                WHERE MaPhongBan=@parent AND MaCoQuan=@agency
                UNION ALL
                SELECT p.MaPhongBan,p.MaPhongBanCha FROM dbo.PhongBan p
                JOIN Ancestors a ON p.MaPhongBan=a.MaPhongBanCha
                WHERE p.MaCoQuan=@agency
            )
            SELECT COUNT(*) FROM Ancestors WHERE MaPhongBan=@self
            OPTION(MAXRECURSION 100)",new{parent,self,agency},tx);
        return loop==0;
    }

    public static void MapQuanTriPhongBan(this IEndpointRouteBuilder app)
    {
        var group=app.MapGroup("/api/v1/admin/departments").RequireAuthorization();
        group.MapGet("",async(HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"departments.manage");
            if(error!=null)return error;
            using var cn=db.Open();
            var rows=await cn.QueryAsync(@"
                SELECT p.MaPhongBan,p.MaDinhDanh,p.TenPhongBan,p.ThuTuHienThi,
                       p.MaPhongBanCha,p.DangHoatDong,cha.TenPhongBan AS TenPhongBanCha,
                       (SELECT COUNT(*) FROM dbo.NguoiDung n WHERE n.MaPhongBan=p.MaPhongBan AND n.MaCoQuan=@agency) AS SoNguoiDung,
                       (SELECT COUNT(*) FROM dbo.PhongBan c WHERE c.MaPhongBanCha=p.MaPhongBan AND c.MaCoQuan=@agency) AS SoPhongBanCon
                FROM dbo.PhongBan p LEFT JOIN dbo.PhongBan cha ON cha.MaPhongBan=p.MaPhongBanCha
                WHERE p.MaCoQuan=@agency ORDER BY p.ThuTuHienThi,p.TenPhongBan",
                new{agency=actor!.MaCoQuan});
            return Results.Ok(rows);
        });

        group.MapPost("",async(TaoPhongBan body,HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"departments.manage");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            var name=body.TenPhongBan?.Trim()??"";
            var code=body.MaDinhDanh?.Trim().ToUpperInvariant()??"";
            if(!HopLe(name,body.ThuTuHienThi) || code.Length is < 2 or > 40 ||
               !System.Text.RegularExpressions.Regex.IsMatch(code,"^[A-Z0-9_]+$") ||
               code is "LANH_DAO" or "VAN_PHONG" or "VAN_THU")
                return Results.BadRequest(new{message="Tên phòng (2–200 ký tự), thứ tự (0–9999) hoặc mã phòng không hợp lệ."});
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            if(!await ChaHopLe(cn,tx,actor.MaCoQuan.Value,body.MaPhongBanCha,null)){
                tx.Rollback();return Results.BadRequest(new{message="Phòng cấp trên phải đang hoạt động và thuộc cùng cơ quan."});
            }
            try{
                var id=await cn.ExecuteScalarAsync<int>(@"
                    INSERT dbo.PhongBan(MaCoQuan,MaDinhDanh,TenPhongBan,ThuTuHienThi,MaPhongBanCha)
                    VALUES(@agency,@code,@name,@order,@parent);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new{agency=actor.MaCoQuan,code,name,order=body.ThuTuHienThi,parent=body.MaPhongBanCha},tx);
                await GhiNhatKy(cn,tx,actor.MaCoQuan.Value,actor.MaNguoiDung,"TAO_PHONG_BAN",$"Tạo phòng {code} ({id}): {name}");
                tx.Commit();return Results.Created($"/api/v1/admin/departments/{id}",new{maPhongBan=id});
            }catch(SqlException ex)when(ex.Number is 2601 or 2627){
                tx.Rollback();return Results.Conflict(new{message="Mã phòng ban đã tồn tại trong cơ quan."});
            }
        });

        group.MapPut("/{id:int}",async(int id,SuaPhongBan body,HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"departments.manage");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            var name=body.TenPhongBan?.Trim()??"";
            if(!HopLe(name,body.ThuTuHienThi))return Results.BadRequest(new{message="Tên phòng hoặc thứ tự không hợp lệ."});
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var current=await cn.QuerySingleOrDefaultAsync<PhongBanRow>(@"
                SELECT MaPhongBan,MaDinhDanh,DangHoatDong FROM dbo.PhongBan WITH(UPDLOCK,HOLDLOCK)
                WHERE MaPhongBan=@id AND MaCoQuan=@agency",new{id,agency=actor.MaCoQuan},tx);
            if(current==null){tx.Rollback();return Results.NotFound();}
            if(!current.DangHoatDong){tx.Rollback();return Results.Conflict(new{message="Phòng đã ngừng hoạt động; mở lại trước khi chỉnh sửa."});}
            if(current.MaDinhDanh=="LANH_DAO" && body.MaPhongBanCha!=null){
                tx.Rollback();return Results.BadRequest(new{message="Ban lãnh đạo không được trực thuộc phòng khác."});
            }
            if(!await ChaHopLe(cn,tx,actor.MaCoQuan.Value,body.MaPhongBanCha,id)){
                tx.Rollback();return Results.BadRequest(new{message="Phòng cấp trên không hợp lệ hoặc tạo vòng lặp cơ cấu."});
            }
            await cn.ExecuteAsync(@"
                UPDATE dbo.PhongBan SET TenPhongBan=@name,ThuTuHienThi=@order,MaPhongBanCha=@parent
                WHERE MaPhongBan=@id AND MaCoQuan=@agency",
                new{id,agency=actor.MaCoQuan,name,order=body.ThuTuHienThi,parent=body.MaPhongBanCha},tx);
            await GhiNhatKy(cn,tx,actor.MaCoQuan.Value,actor.MaNguoiDung,"SUA_PHONG_BAN",$"Sửa phòng {id}: {name}");
            tx.Commit();return Results.Ok(new{maPhongBan=id});
        });

        group.MapPut("/{id:int}/status",async(int id,TrangThaiPhongBan body,HttpContext http,PhanQuyen permission,Database db)=>{
            var(actor,error)=await permission.YeuCau(http,"departments.manage");
            if(error!=null)return error;
            if(actor!.MaCoQuan is null)return Results.Forbid();
            using var cn=db.Open();await cn.OpenAsync();
            using var tx=cn.BeginTransaction(System.Data.IsolationLevel.Serializable);
            var current=await cn.QuerySingleOrDefaultAsync<PhongBanRow>(@"
                SELECT MaPhongBan,MaDinhDanh,DangHoatDong FROM dbo.PhongBan WITH(UPDLOCK,HOLDLOCK)
                WHERE MaPhongBan=@id AND MaCoQuan=@agency",new{id,agency=actor.MaCoQuan},tx);
            if(current==null){tx.Rollback();return Results.NotFound();}
            if(current.DangHoatDong==body.DangHoatDong){tx.Commit();return Results.Ok(new{maPhongBan=id,dangHoatDong=body.DangHoatDong});}
            if(!body.DangHoatDong){
                if(current.MaDinhDanh is "LANH_DAO" or "VAN_PHONG" or "VAN_THU"){
                    tx.Rollback();return Results.Conflict(new{message="Không thể ngừng hoạt động phòng hệ thống; quy trình văn bản đang sử dụng mã phòng này."});
                }
                var assigned=await cn.ExecuteScalarAsync<int>(@"
                    SELECT (SELECT COUNT(*) FROM dbo.NguoiDung WHERE MaCoQuan=@agency AND MaPhongBan=@id)
                         + (SELECT COUNT(*) FROM dbo.PhongBan WHERE MaCoQuan=@agency AND MaPhongBanCha=@id)",
                    new{id,agency=actor.MaCoQuan},tx);
                if(assigned>0){tx.Rollback();return Results.Conflict(new{message="Cần chuyển hết tài khoản và phòng ban trực thuộc trước khi ngừng hoạt động."});}
            }else{
                var parentActive=await cn.ExecuteScalarAsync<int>(@"
                    SELECT COUNT(*) FROM dbo.PhongBan p
                    WHERE p.MaPhongBan=@id AND p.MaCoQuan=@agency AND
                      (p.MaPhongBanCha IS NULL OR EXISTS(SELECT 1 FROM dbo.PhongBan c
                        WHERE c.MaPhongBan=p.MaPhongBanCha AND c.MaCoQuan=@agency AND c.DangHoatDong=1))",
                    new{id,agency=actor.MaCoQuan},tx);
                if(parentActive==0){tx.Rollback();return Results.Conflict(new{message="Phòng cấp trên phải đang hoạt động trước."});}
            }
            await cn.ExecuteAsync("UPDATE dbo.PhongBan SET DangHoatDong=@enabled WHERE MaPhongBan=@id AND MaCoQuan=@agency",
                new{id,agency=actor.MaCoQuan,enabled=body.DangHoatDong},tx);
            await GhiNhatKy(cn,tx,actor.MaCoQuan.Value,actor.MaNguoiDung,
                body.DangHoatDong?"MO_PHONG_BAN":"NGUNG_PHONG_BAN",$"Đổi trạng thái phòng {id}: {body.DangHoatDong}");
            tx.Commit();return Results.Ok(new{maPhongBan=id,dangHoatDong=body.DangHoatDong});
        });
    }

    private static Task GhiNhatKy(SqlConnection cn,SqlTransaction tx,int agency,int user,string action,string description)
        => cn.ExecuteAsync(@"
            INSERT dbo.NhatKyHeThong(MaCoQuan,MaNguoiDung,LoaiNhatKy,HanhDong,MoTa)
            VALUES(@agency,@user,'NGHIEP_VU',@action,@description)",new{agency,user,action,description},tx);
}
