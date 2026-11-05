using EcoQuest.Core.Common;

namespace EcoQuest.Services;

public static class Messages
{
    public const string UsernameTaken = "Username sudah digunakan";
    public const string EmailTaken = "Email sudah terdaftar";
    public const string InvalidCredentials = "Kombinasi username/email dan password salah";
    public const string WrongOldPassword = "Password lama tidak sesuai";
    public const string ReviewAlreadySubmitted = "Refleksi hari ini sudah tersimpan dan tidak dapat diubah";
    public const string ReviewDateMismatch = "Hari sudah berganti. Refleksi untuk tanggal sebelumnya sudah terkunci";
    public const string WarningsNotAcknowledged = "Ada nilai yang tidak biasa. Mohon konfirmasi terlebih dahulu";
    public const string NetworkError = "Tidak dapat terhubung ke server. Periksa koneksi internet lalu coba lagi";
    public const string ValidationFailed = "Periksa kembali isian yang ditandai";
    public const string FileError = "Berkas tidak dapat dibaca atau disimpan";
    public const string Unknown = "Terjadi kesalahan tak terduga. Silakan coba lagi";

    public static string For(ErrorCode code) => code switch
    {
        ErrorCode.UsernameTaken => UsernameTaken,
        ErrorCode.EmailTaken => EmailTaken,
        ErrorCode.InvalidCredentials => InvalidCredentials,
        ErrorCode.WrongOldPassword => WrongOldPassword,
        ErrorCode.ReviewAlreadySubmitted => ReviewAlreadySubmitted,
        ErrorCode.ReviewDateMismatch => ReviewDateMismatch,
        ErrorCode.WarningsNotAcknowledged => WarningsNotAcknowledged,
        ErrorCode.NetworkError => NetworkError,
        ErrorCode.ValidationFailed => ValidationFailed,
        ErrorCode.FileError => FileError,
        _ => Unknown
    };
}
