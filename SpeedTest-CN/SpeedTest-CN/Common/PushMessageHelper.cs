using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace SpeedTest_CN.Common;

public class PushMessageHelper(IConfiguration configuration)
{
    /// <summary>
    /// 消息推送
    /// </summary>
    /// <param name="title">推送标题</param>
    /// <param name="message">推送信息</param>
    /// <param name="icon">推送图标</param>
    /// <param name="customIconUrl">自定义图标地址</param>
    public void Push(string title, string message, PushIcon icon = PushIcon.Default, string customIconUrl = null)
    {
        try
        {
            var iconUrl = GetIconUrl(icon, customIconUrl);
            var barkUrl = configuration.GetSection("BarkUrl").Get<string>();
            var gotifyUrl = configuration.GetSection("PushMessageUrl").Get<string>();
            if (!string.IsNullOrEmpty(barkUrl))
            {
                var barkclient = new HttpClient();
                var data = JsonConvert.SerializeObject(new
                {
                    body = message,
                    title = title,
                    badge = 1,
                    icon = iconUrl,
                    group = ""
                });
                var byteContent = new ByteArrayContent(Encoding.UTF8.GetBytes(data));
                byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                barkclient.PostAsync(barkUrl, byteContent);
            }

            if (!string.IsNullOrEmpty(barkUrl))
            {
                using var gotifyclient = new HttpClient();
                using var formData = new MultipartFormDataContent();
                formData.Add(new StringContent(title), "title");
                formData.Add(new StringContent(message), "message");
                formData.Add(new StringContent(configuration["PushMessagePriority"]!), "priority");

                var response = gotifyclient.PostAsync(gotifyUrl, formData).Result;
                response.EnsureSuccessStatusCode();
                var responseBody = response.Content.ReadAsStringAsync().Result;
                Console.WriteLine("Response: " + responseBody);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }

    private string GetIconUrl(PushIcon icon, string? customUrl = null)
    {
        return icon switch
        {
            PushIcon.Default => "https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/png/netcam-studio.png",
            PushIcon.Camera => "https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/png/netcam-studio.png",
            PushIcon.Zentao => "https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/png/flowise.png",
            PushIcon.Note => "https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/png/pritunl.png",
            PushIcon.OverTime => "https://cdn.jsdelivr.net/gh/homarr-labs/dashboard-icons/png/leantime.png",
            _ => ""
        };
    }

    public enum PushIcon
    {
        Default, // 默认图标
        Camera, // 摄像头
        Zentao,
        Note,
        OverTime
    }
}