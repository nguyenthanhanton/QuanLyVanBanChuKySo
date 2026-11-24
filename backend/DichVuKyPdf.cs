using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using iText.Bouncycastle.Crypto;
using iText.Bouncycastle.X509;
using iText.Commons.Bouncycastle.Cert;
using iText.Commons.Bouncycastle.Crypto;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Font;
using iText.IO.Font;
using iText.Signatures;
using Org.BouncyCastle.Pkcs;

// PAdES/CMS embedded inside PDF, not a drawn image of a signature.
// PDF signed sequentially with append mode, preserving prior signed revisions.
public sealed class DichVuKyPdf(IConfiguration config)
{
    private byte[] KhoaMaHoa()
    {
        var raw = config["Security:EncryptionKeyBase64"];
        if (string.IsNullOrWhiteSpace(raw)) throw new InvalidOperationException("Chưa cấu hình Security__EncryptionKeyBase64.");
        var bytes = Convert.FromBase64String(raw);
        if (bytes.Length != 32) throw new InvalidOperationException("Khóa mã hóa kho chứng thư phải dài 32 byte.");
        return bytes;
    }

    private string MatKhauPfx() => config["Security:PfxPassword"]
        ?? throw new InvalidOperationException("Chưa cấu hình Security__PfxPassword.");

    public string MaKhoa() => config["Security:ActiveKeyId"]
        ?? throw new InvalidOperationException("Chưa cấu hình Security__ActiveKeyId.");

    private string ThuMucKhoa() => config["Signing:KeyStorePath"]
        ?? throw new InvalidOperationException("Chưa cấu hình Signing__KeyStorePath.");

    private static byte[] AAD(Guid certId, int agency, int? owner)
        => Encoding.UTF8.GetBytes($"{certId:N}|{agency}|{owner?.ToString() ?? "CO_QUAN"}");

    public sealed record ChungThuMoi(byte[] ChungThuCongKhai, string SoSerial, DateTime NgayHieuLuc,
        DateTime NgayHetHan, string DuongDanKhoaMaHoa);

    // Internal demo certificate, clearly NOT a CA-issued legally trusted signature.
    public ChungThuMoi TaoChungThuNoiBo(Guid certId, int agency, int? owner, string name)
    {
        using var rsa = RSA.Create(3072);
        var req = new CertificateRequest($"CN={name.Replace(',', ' ')}", rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        var now = DateTimeOffset.UtcNow;
        using var cert = req.CreateSelfSigned(now.AddMinutes(-5), now.AddDays(365));
        return LuuChungThu(certId, agency, owner, cert);
    }

    public ChungThuMoi NhapChungThu(Guid certId, int agency, int? owner, byte[] pkcs12, string password)
    {
        using var cert = new X509Certificate2(pkcs12, password,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
        if (!cert.HasPrivateKey) throw new InvalidDataException("Chứng thư PFX không có khóa bí mật.");
        var now = DateTime.UtcNow;
        if (cert.NotBefore.ToUniversalTime()>now || cert.NotAfter.ToUniversalTime()<=now)
            throw new InvalidDataException("Chứng thư chưa có hiệu lực hoặc đã hết hạn.");
        return LuuChungThu(certId, agency, owner, cert);
    }

    private ChungThuMoi LuuChungThu(Guid certId, int agency, int? owner, X509Certificate2 cert)
    {
        var path = Path.Combine(ThuMucKhoa(),agency.ToString(),certId.ToString("N")+".key");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var plaintext = cert.Export(X509ContentType.Pkcs12, MatKhauPfx());
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        try {
            using var aes = new AesGcm(KhoaMaHoa(),16);
            aes.Encrypt(nonce,plaintext,ciphertext,tag,AAD(certId,agency,owner));
            using(var fs=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                fs.Write(nonce);fs.Write(tag);fs.Write(ciphertext);fs.Flush(true);
            }
            return new ChungThuMoi(cert.RawData,cert.SerialNumber,cert.NotBefore.ToUniversalTime(),
                cert.NotAfter.ToUniversalTime(),path);
        } finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    private byte[] DocPfx(Guid certId, int agency, int? owner, string path)
    {
        var blob = File.ReadAllBytes(path);
        if (blob.Length<29) throw new InvalidDataException("Kho chứng thư bị hỏng.");
        var plaintext=new byte[blob.Length-28];
        using var aes=new AesGcm(KhoaMaHoa(),16);
        aes.Decrypt(blob.AsSpan(0,12),blob.AsSpan(28),blob.AsSpan(12,16),plaintext,
            AAD(certId,agency,owner));
        return plaintext;
    }

    public void ChuanBi(string source,string destination,string number)
    {
        // Materialize issue number on first page BEFORE any signature. Never rewrite afterwards.
        var fontPath=config["Signing:FontPath"];
        if(string.IsNullOrWhiteSpace(fontPath) || !File.Exists(fontPath))
            throw new InvalidOperationException("Cần Signing__FontPath trỏ tới phông TTF/OTF hỗ trợ tiếng Việt.");
        using(var original=new PdfDocument(new PdfReader(source))){
            if(new SignatureUtil(original).GetSignatureNames().Count>0)
                throw new InvalidDataException("PDF nguồn đã có chữ ký; không thể sửa chân trang sau ký.");
        }
        using var pdf=new PdfDocument(new PdfReader(source),new PdfWriter(destination));
        if(pdf.GetNumberOfPages()<1)throw new InvalidDataException("PDF không có trang.");
        var font=PdfFontFactory.CreateFont(fontPath,PdfEncodings.IDENTITY_H);
        var canvas=new PdfCanvas(pdf.GetFirstPage());
        canvas.BeginText().SetFontAndSize(font,9).MoveText(40,22)
            .ShowText($"Bản đăng ký phát hành — Số: {number}").EndText();
    }

    public sealed record KetQuaKy(string TenTruongKy,byte[] DuLieuCms,byte[] ChungThuCongKhai);

    public KetQuaKy Ky(string source,string destination,string field,Guid certId,int agency,
        int? owner,string keyPath,string label)
    {
        var pkcs12=DocPfx(certId,agency,owner,keyPath);
        try {
            var store=new Pkcs12StoreBuilder().Build();
            using(var memory=new MemoryStream(pkcs12,false))store.Load(memory,MatKhauPfx().ToCharArray());
            string? alias=null;
            foreach(string candidate in store.Aliases)
                if(store.IsKeyEntry(candidate)){alias=candidate;break;}
            if(alias==null)throw new InvalidDataException("Không thấy khóa ký trong chứng thư.");
            IPrivateKey privateKey=new PrivateKeyBC(store.GetKey(alias).Key);
            IX509Certificate[] chain=store.GetCertificateChain(alias)
                .Select(item=>(IX509Certificate)new X509CertificateBC(item.Certificate)).ToArray();
            var signer=new PdfSigner(new PdfReader(source),new FileStream(destination,FileMode.CreateNew),
                new StampingProperties().UseAppendMode());
            signer.GetSignerProperties().SetFieldName(field);
            // Invisible cryptographic PDF signature. Visual marks require separate design.
            IExternalSignature external=new PrivateKeySignature(privateKey,"SHA-256");
            signer.SignDetached(external,chain,null,null,null,0,PdfSigner.CryptoStandard.CMS);
            using var pdf=new PdfDocument(new PdfReader(destination));
            var util=new SignatureUtil(pdf);
            if(!util.GetSignatureNames().Contains(field) ||
               !util.ReadSignatureData(field).VerifySignatureIntegrityAndAuthenticity())
                throw new CryptographicException("Chữ ký mới không xác minh được.");
            var signature=util.GetSignatureDictionary(field);
            var cms=signature.GetAsString(PdfName.Contents).GetValueBytes();
            return new KetQuaKy(field,cms,chain[0].GetEncoded());
        } finally {CryptographicOperations.ZeroMemory(pkcs12);}
    }

    public sealed record TinhTrangChuKy(string TenTruongKy,bool ToanVenMatMa,
        bool BaoPhuToanBoBanHienTai);

    public IReadOnlyList<TinhTrangChuKy> XacMinh(string path)
    {
        using var pdf=new PdfDocument(new PdfReader(path));
        var util=new SignatureUtil(pdf);
        return util.GetSignatureNames().Select(name=>new TinhTrangChuKy(name,
            util.ReadSignatureData(name).VerifySignatureIntegrityAndAuthenticity(),
            util.SignatureCoversWholeDocument(name))).ToArray();
    }

    public static async Task<string> Bam(string path)
    {
        await using var stream=File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }
}
