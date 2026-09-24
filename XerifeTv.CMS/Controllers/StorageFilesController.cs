using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XerifeTv.CMS.Modules.Abstractions.Interfaces;
using XerifeTv.CMS.Modules.Common;

namespace XerifeTv.CMS.Controllers;

[Authorize]
public class StorageFilesController(
  IStorageFilesService _service,
  ILogger<StorageFilesController> _logger) : Controller
{
    [HttpPost]
	[Authorize(Roles = "admin, common")]
	public async Task<JsonResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return Json(Result<string>.Failure(new Error("400", "Arquivo ausente")));

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        Stream uploadStream;
        string uploadFileName = file.FileName;

        // SRT não é suportado pelo <track> do player - converte pra WebVTT no upload.
        if (extension == ".srt")
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var srt = ReadTextRobust(ms.ToArray());
            var vtt = ConvertSrtToVtt(srt);

            uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(vtt));
            uploadFileName = Path.ChangeExtension(file.FileName, ".vtt");
        }
        else
        {
            uploadStream = file.OpenReadStream();
        }

        // O Supabase Storage rejeita keys com espaços/colchetes/etc. Sanitiza o nome e
        // adiciona um sufixo único (evita colisão/sobrescrita de arquivos de mesmo nome).
        uploadFileName = SanitizeFileName(uploadFileName);

        var response = await _service.UploadFileAsync(uploadStream, uploadFileName, "subtitles");
        await uploadStream.DisposeAsync();

        _logger.LogInformation(
          response.IsSuccess ? $"Upload file {response.Data} success" : "Error uploading file");

        return Json(response);
    }

    // Mantém só caracteres seguros pro Supabase Storage (a-z, 0-9, ponto, hífen, underscore)
    // e acrescenta um sufixo curto único.
    private static string SanitizeFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var name = Path.GetFileNameWithoutExtension(fileName);

        var safe = Regex.Replace(name, @"[^a-zA-Z0-9._-]+", "-").Trim('-', '.');
        if (string.IsNullOrWhiteSpace(safe)) safe = "subtitle";
        if (safe.Length > 80) safe = safe[..80];

        var unique = Guid.NewGuid().ToString("N")[..8];
        return $"{safe}-{unique}{extension}";
    }

    // SRT arquivos costumam vir em Windows-1252/Latin1; tenta UTF-8 e cai pra Latin1 se
    // aparecer caractere de substituição (evita acentos quebrados).
    private static string ReadTextRobust(byte[] bytes)
    {
        var utf8 = new UTF8Encoding(false, false).GetString(bytes);
        return utf8.Contains('�') ? Encoding.Latin1.GetString(bytes) : utf8;
    }

    private static string ConvertSrtToVtt(string srt)
    {
        var normalized = srt.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        var lines = normalized.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            // vírgula -> ponto só nas linhas de tempo (00:00:20,000 --> 00:00:24,400)
            if (lines[i].Contains("-->"))
                lines[i] = lines[i].Replace(',', '.');
        }

        return "WEBVTT\n\n" + string.Join("\n", lines);
    }
}
