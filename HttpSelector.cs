using System.Net.Http;
using System.Text.RegularExpressions;

namespace RoomAssign;

public class HttpSelector(
    string applyerName,
    List<HouseCondition> communityList,
    string startTime,
    CancellationToken cancellationToken,
    string cookie,
    int clickIntervalMs = 200) : ISelector
{
    private void WaitUntilStartTime(string startTime, CancellationToken cancellationToken)
    {
        var start = DateTime.ParseExact(startTime, "yyyy-MM-dd HH:mm:ss", null);

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var now = DateTime.Now;
            var remaining = start - now;
            if (remaining.TotalSeconds < 2)
            {
                Console.WriteLine($"当前时间 {now:yyyy-MM-dd HH:mm:ss}，开始抢！");
                break;
            }

            Console.WriteLine($"当前时间 {now:yyyy-MM-dd HH:mm:ss}，距离选房开始还有 {remaining}");
            Thread.Sleep(1000);
        }
    }

    /// <summary>
    /// 通过 GET 请求获取申请人ID，匹配 applyerName 对应的行
    /// </summary>
    private async Task<string?> GetApplyerId(HttpClient client, string applyerName)
    {
        try
        {
            var response = await client.GetAsync("https://ent.qpgzf.cn/RoomAssign/Index");
            var html = await response.Content.ReadAsStringAsync();
            // 正则匹配：通过 ondblclick 中的 show 函数参数获取申请人ID，同时匹配包含申请人名称的 <td>
            var pattern =
                $"<tr\\s+ondblclick=\"javascript:show\\('([^']+)'\\);\">[\\s\\S]*?<td>\\s*{Regex.Escape(applyerName)}\\s*</td>";
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var applyerId = match.Groups[1].Value;
                Console.WriteLine($"成功获取申请人ID: {applyerId}");
                return applyerId;
            }
            else
            {
                Console.WriteLine($"未能从页面中找到申请人 \"{applyerName}\" 对应的 ID");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("获取申请人ID异常：" + ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 通过 POST 请求匹配房源信息，返回匹配到的房间ID
    /// 逻辑参考你提供的 Python get_room_id 函数
    /// </summary>
    private async Task<string?> GetRoomId(HttpClient client, string applyIds, string communityName, string roomType)
    {
        // 此 URL 与参数根据实际情况调整
        string searchRoomUrl = "https://ent.qpgzf.cn/RoomAssign/SelectRoom";
        var data = new Dictionary<string, string>
        {
            { "ApplyIDs", applyIds },
            { "IsApplyTalent", "0" },
            { "type", "1" },
            { "SearchEntity._PageSize", "500" },
            { "SearchEntity._PageIndex", "1" }
        };
        try
        {
            var content = new FormUrlEncodedContent(data);
            var response = await client.PostAsync(searchRoomUrl, content);
            var html = await response.Content.ReadAsStringAsync();

            // 正则匹配：查找包含指定社区名称和房型的房源行，提取 selectRooms 的第一个参数即房间ID
            var pattern =
                $@"<tr>[\s\S]*?selectRooms\('([^']*)',[\s\S]*?{Regex.Escape(communityName)}[\s\S]*?{Regex.Escape(roomType)}[\s\S]*?</tr>";
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var roomId = match.Groups[1].Value;
                Console.WriteLine($"获取到 {communityName} 最末房间ID：{roomId}");
                return roomId;
            }

            Console.WriteLine("未查询到匹配的房源信息...");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine("获取房间ID异常：" + ex.Message);
            return null;
        }
    }

    /// <summary>
    /// 发包方式请求选房接口
    /// </summary>
    private async Task SelectRoomAsync(HttpClient client, string applyerId, string roomId)
    {
        var selectRoomUrl = "https://ent.qpgzf.cn/RoomAssign/AjaxSelectRoom";
        var data = new Dictionary<string, string>
        {
            { "ApplyIDs", applyerId },
            { "roomID", roomId }
        };
        var content = new FormUrlEncodedContent(data);
        Console.WriteLine("开始请求选房...");
        try
        {
            var response = await client.PostAsync(selectRoomUrl, content);
            var result = await response.Content.ReadAsStringAsync();
            Console.WriteLine(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine("发包请求错误：" + ex.Message);
        }
    }

    public async Task RunAsync()
    {
        if (string.IsNullOrWhiteSpace(cookie))
        {
            Console.WriteLine("在发包模式下，Cookie 必填");
            return;
        }
        
        using var client = new HttpClient();
        // 将 Cookie 添加到请求头
        client.DefaultRequestHeaders.Add("Cookie", cookie);

        // 1. 通过 GET 请求获取 https://ent.qpgzf.cn/RoomAssign/Index 页面并解析申请人ID
        var applyerId = await GetApplyerId(client, applyerName);
        if (applyerId == null)
        {
            Console.WriteLine("无法获取申请人ID，退出");
            return;
        }

        // 2. 等待至选房开始时间
        WaitUntilStartTime(startTime, cancellationToken);
        Console.WriteLine("发包选房开始！");

        // 3. 通过发包匹配获取房间ID
        // 这里以 communityList 的第一个条件为示例
        var condition = communityList[0];
        var roomType = "一居室"; // TODO 先固定为一居室，后续可根据需求调整
        var roomId = await GetRoomId(client, applyerId, condition.CommunityName, roomType);
        if (roomId == null)
        {
            Console.WriteLine("无法获取匹配的房间ID，退出");
            return;
        }

        // TODO 定义发包时间窗口，例如选房开始后 10 秒内不断发包
        var endTime = DateTime.ParseExact(startTime, "yyyy-MM-dd HH:mm:ss", null).AddSeconds(10);
        while (DateTime.Now < endTime)
        {
            if (cancellationToken.IsCancellationRequested)
                break;
            await SelectRoomAsync(client, applyerId, roomId);
            await Task.Delay(10, cancellationToken);
        }

        Console.WriteLine("发包流程完成");
    }

    public void Stop()
    {
        // 取消任务
    }
}