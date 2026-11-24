using System.IO.Compression;
using System.Security.Cryptography;

// Only non-executable, non-macro document formats. Validate the file header,
// never trust the MIME type claimed by the browser.
public static class DichVuTepVanBan
{
    public const int GioiHanTep = 20;
    public const long GioiHanMoiTep = 25L * 1024 * 1024;
    public const long GioiHanTong = 100L * 1024 * 1024;

    private static readonly Dictionary<string,string> LoaiChoPhep = new(StringComparer.OrdinalIgnoreCase) {
        [".pdf"]="application/pdf",
        [".doc"]="application/msword",
        [".docx"]="application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"]="application/vnd.ms-excel",
        [".xlsx"]="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    public sealed record TapTinDaLuu(Guid MaTep,string TenTep,string LoaiNoiDung,long KichThuoc,
        string DuongDanLuuTru,string MaBamSha256);

    public static async Task<string?> KiemTra(IFormFile file)
    {
        if(file.Length is < 8 or > GioiHanMoiTep)return "Mỗi tệp phải từ 8 byte đến 25 MB.";
        var name=Path.GetFileName(file.FileName);
        var extension=Path.GetExtension(name);
        if(name.Length is < 1 or > 255 || !LoaiChoPhep.ContainsKey(extension))
            return "Chỉ hỗ trợ PDF, Word (.doc, .docx), Excel (.xls, .xlsx).";
        await using var stream=file.OpenReadStream();
        var magic=new byte[8];
        if(await stream.ReadAsync(magic)!=magic.Length)return "Tệp không hợp lệ.";
        if(extension.Equals(".pdf",StringComparison.OrdinalIgnoreCase))
            return System.Text.Encoding.ASCII.GetString(magic[..5])=="%PDF-"?null:"Tệp PDF sai định dạng.";
        if(extension.Equals(".doc",StringComparison.OrdinalIgnoreCase) ||
           extension.Equals(".xls",StringComparison.OrdinalIgnoreCase)){
            return magic.SequenceEqual(new byte[]{0xD0,0xCF,0x11,0xE0,0xA1,0xB1,0x1A,0xE1})
                ?null:"Tệp Word/Excel định dạng cũ không hợp lệ.";
        }
        if(magic[0]!=0x50 || magic[1]!=0x4B)return "Tệp Office Open XML phải có định dạng ZIP.";
        stream.Position=0;
        try {
            using var archive=new ZipArchive(stream,ZipArchiveMode.Read,true);
            if(archive.GetEntry("[Content_Types].xml")==null)return "Thiếu cấu trúc Office Open XML.";
            var required=extension.Equals(".docx",StringComparison.OrdinalIgnoreCase)
                ?"word/document.xml":"xl/workbook.xml";
            if(archive.GetEntry(required)==null)return "Nội dung không khớp phần mở rộng Word/Excel.";
        }catch(InvalidDataException){return "Tệp Office ZIP bị hỏng.";}
        return null;
    }

    public static async Task<TapTinDaLuu> Luu(IFormFile file,string folder)
    {
        Directory.CreateDirectory(folder);
        var id=Guid.NewGuid();
        var name=Path.GetFileName(file.FileName);
        var extension=Path.GetExtension(name).ToLowerInvariant();
        var path=Path.Combine(folder,id.ToString("N")+extension);
        await using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            await file.CopyToAsync(output);
        await using var input=File.OpenRead(path);
        var hash=Convert.ToHexString(await SHA256.HashDataAsync(input));
        return new TapTinDaLuu(id,name,LoaiChoPhep[extension],file.Length,path,hash);
    }

    public static async Task<string?> KiemTraDanhSach(IReadOnlyList<IFormFile> files)
    {
        if(files.Count>GioiHanTep || files.Sum(f=>f.Length)>GioiHanTong)
            return "Tối đa 20 tệp, tổng dung lượng 100 MB.";
        foreach(var f in files){
            var issue=await KiemTra(f);
            if(issue!=null)return $"{Path.GetFileName(f.FileName)}: {issue}";
        }
        return null;
    }
}
