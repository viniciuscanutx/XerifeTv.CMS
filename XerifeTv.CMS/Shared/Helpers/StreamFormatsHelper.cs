namespace XerifeTv.CMS.Shared.Helpers;

public class StreamFormatsHelper
{
    public static string[] Streaming => ["m3u8", "hls", "mpeg-dash", "rtsp"];
    public static string[] Vod => ["mp4", "m3u8", "hls", "webm", "mkv", "mov"];
}