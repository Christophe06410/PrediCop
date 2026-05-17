namespace PrediCop.Mobile.Services;

public class MediaUploadService(HttpClient http)
{
    public void SetAuthToken(string token) =>
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

    public async Task<bool> PickAndUploadAsync(
        Guid missionId,
        string? cameraDeviceId = null,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        FileResult? picked;
        try
        {
            picked = await MediaPicker.PickVideoAsync();
        }
        catch (FeatureNotSupportedException)
        {
            throw new InvalidOperationException("La sélection de vidéo n'est pas disponible sur cet appareil.");
        }

        if (picked is null) return false; // user cancelled

        return await UploadFileAsync(picked, missionId, cameraDeviceId, "video/mp4", progress, ct);
    }

    public async Task<bool> PickAndUploadPhotoAsync(
        Guid missionId,
        string? cameraDeviceId = null,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        FileResult? picked;
        try
        {
            picked = await MediaPicker.PickPhotoAsync();
        }
        catch (FeatureNotSupportedException)
        {
            throw new InvalidOperationException("La sélection de photo n'est pas disponible sur cet appareil.");
        }

        if (picked is null) return false; // user cancelled

        return await UploadFileAsync(picked, missionId, cameraDeviceId, "image/jpeg", progress, ct);
    }

    public async Task<bool> CaptureAndUploadPhotoAsync(
        Guid missionId,
        string? cameraDeviceId = null,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        FileResult? picked;
        try
        {
            picked = await MediaPicker.CapturePhotoAsync();
        }
        catch (FeatureNotSupportedException)
        {
            throw new InvalidOperationException("L'appareil photo n'est pas disponible sur cet appareil.");
        }

        if (picked is null) return false; // user cancelled

        return await UploadFileAsync(picked, missionId, cameraDeviceId, "image/jpeg", progress, ct);
    }

    private async Task<bool> UploadFileAsync(
        FileResult picked,
        Guid missionId,
        string? cameraDeviceId,
        string defaultContentType,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        await using var stream = await picked.OpenReadAsync();

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(missionId.ToString()), "MissionId");
        content.Add(new StringContent(DateTime.UtcNow.ToString("O")), "RecordedAt");
        if (cameraDeviceId is not null)
            content.Add(new StringContent(cameraDeviceId), "CameraDeviceId");

        var fileContent = new ProgressStreamContent(stream, progress);
        fileContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(picked.ContentType ?? defaultContentType);
        content.Add(fileContent, "File", picked.FileName);

        var response = await http.PostAsync("api/media", content, ct);
        return response.IsSuccessStatusCode;
    }
}

internal class ProgressStreamContent(Stream inner, IProgress<double>? progress) : HttpContent
{
    protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
    {
        var buffer = new byte[81920];
        long total = inner.CanSeek ? inner.Length : -1;
        long sent = 0;
        int read;
        while ((read = await inner.ReadAsync(buffer)) > 0)
        {
            await stream.WriteAsync(buffer.AsMemory(0, read));
            sent += read;
            if (progress != null && total > 0)
                progress.Report((double)sent / total);
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        if (inner.CanSeek) { length = inner.Length; return true; }
        length = -1; return false;
    }
}
