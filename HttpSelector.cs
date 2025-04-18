using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace RoomAssign
{
    public class HttpSelector : ISelector
    {
        private readonly string _applierName;
        private readonly List<HouseCondition> _conditions;
        private readonly DateTime _startTime;
        private readonly CancellationToken _cancellationToken;
        private readonly string _cookie;
        private readonly int _requestIntervalMs;
        private const int SelectionWindowSeconds = 10;

        public HttpSelector(
            string applierName,
            List<HouseCondition> communityList,
            string startTime,
            string cookie,
            int requestIntervalMs,
            CancellationToken cancellationToken)
        {
            _applierName = applierName;
            _conditions = communityList ?? throw new ArgumentNullException(nameof(communityList));
            if (!DateTime.TryParseExact(startTime, "yyyy-MM-dd HH:mm:ss", null,
                    System.Globalization.DateTimeStyles.None, out _startTime))
                throw new ArgumentException("开始时间格式必须为 yyyy-MM-dd HH:mm:ss", nameof(startTime));
            _cancellationToken = cancellationToken;
            _cookie = cookie ?? throw new ArgumentNullException(nameof(cookie));
            _requestIntervalMs = requestIntervalMs;
        }

        public async Task RunAsync()
        {
            using var client = CreateHttpClient(_cookie);

            var applierId = await GetApplierIdAsync(client);
            if (applierId == null)
                return;

            WaitForStartTime();
            Console.WriteLine("发包选房开始！");

            var anySuccess = false;
            foreach (var condition in _conditions)
            {
                Console.WriteLine($"尝试志愿: {condition}");
                var roomId = await FindMatchingRoomIdAsync(client, applierId, condition);
                if (roomId == null)
                {
                    Console.WriteLine($"未找到符合条件的房源: {condition.CommunityName}");
                    continue;
                }

                Console.WriteLine($"找到房源ID: {roomId}，开始发包");
                var success = await TrySelectRoomAsync(client, applierId, roomId);
                if (success)
                {
                    Console.WriteLine($"志愿 {condition.CommunityName} 发包选房成功！");
                    anySuccess = true;
                    break;
                }
                
                Console.WriteLine($"志愿 {condition.CommunityName} 发包未成功，继续下一个志愿");
            }

            if (!anySuccess)
                Console.WriteLine("所有志愿均未选中，流程结束");
        }

        public void Stop()
        {
            // 用于取消运行中的 Task
        }

        private HttpClient CreateHttpClient(string cookie)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Cookie", $"SYS_USER_COOKIE_KEY={cookie}");
            return client;
        }

        private async Task<string?> GetApplierIdAsync(HttpClient client)
        {
            try
            {
                var html = await client.GetStringAsync("https://ent.qpgzf.cn/RoomAssign/Index", _cancellationToken);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                // 查找 input[name=applierName] 元素
                var node = doc.DocumentNode.SelectSingleNode($"//input[@name='{_applierName}']");
                if (node?.Attributes["value"]?.Value is { } id && !string.IsNullOrEmpty(id))
                {
                    Console.WriteLine($"成功获取申请人ID: {id}");
                    return id;
                }

                Console.WriteLine($"未找到申请人 {_applierName} 对应的 ID");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取申请人ID异常: {ex.Message}");
                return null;
            }
        }

        private void WaitForStartTime()
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var diff = _startTime - now;
                if (diff.TotalSeconds <= 1)
                {
                    Console.WriteLine($"当前时间 {now:yyyy-MM-dd HH:mm:ss}，开始抢！");
                    return;
                }

                Console.WriteLine($"当前时间 {now:yyyy-MM-dd HH:mm:ss}，距离选房开始还有 {diff}");
                Thread.Sleep(1000);
            }
        }

        private async Task<string?> FindMatchingRoomIdAsync(HttpClient client, string applyerId,
            HouseCondition condition)
        {
            const string url = "https://ent.qpgzf.cn/RoomAssign/SelectRoom";
            var data = new Dictionary<string, string>
            {
                ["ApplyIDs"] = applyerId,
                ["IsApplyTalent"] = "0",
                ["type"] = "1",
                ["SearchEntity._PageSize"] = "500",
                ["SearchEntity._PageIndex"] = "1",
                ["SearchEntity._CommonSearchCondition"] = condition.CommunityName
            };
            try
            {
                var response = await client.PostAsync(url, new FormUrlEncodedContent(data), _cancellationToken);
                var html = await response.Content.ReadAsStringAsync(_cancellationToken);

                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                var rows = doc.DocumentNode.SelectNodes("//table[@id='common-table']/tbody/tr");
                if (rows == null)
                    return null;

                foreach (var row in rows)
                {
                    var cells = row.SelectNodes("./td");
                    if (cells == null || cells.Count < 9)
                        continue;

                    // 解析各列
                    var commName = cells[1].InnerText.Trim();
                    if (commName != condition.CommunityName)
                        continue;

                    if (!int.TryParse(cells[2].InnerText.Trim(), out var buildingNo) ||
                        !HouseCondition.FilterEqual(buildingNo, condition.BuildingNo))
                        continue;

                    var floorText = cells[3].InnerText.Trim();
                    if (!int.TryParse(floorText.Length >= 2 ? floorText[..2] : floorText,
                            out var floorNo) ||
                        !HouseCondition.FilterFloor(floorNo, condition.FloorRange))
                        continue;

                    if (!double.TryParse(cells[5].InnerText.Trim(), out var price) ||
                        !HouseCondition.FilterPrice(price, condition.MaxPrice))
                        continue;

                    if (!double.TryParse(cells[7].InnerText.Trim(), out var area) ||
                        !HouseCondition.FilterArea(area, condition.LeastArea))
                        continue;
                    
                    // 提取 onclick 中的 roomId
                    var actionNode = row.SelectSingleNode(".//a[contains(@onclick, 'selectRooms')]");
                    var onclick = actionNode?.GetAttributeValue("onclick", string.Empty);
                    if (string.IsNullOrEmpty(onclick))
                        continue;

                    const string token = "selectRooms('";
                    var start = onclick.IndexOf(token, StringComparison.Ordinal) + token.Length;
                    var end = onclick.IndexOf('\'', start);
                    if (start < token.Length || end < 0)
                        continue;

                    var roomId = onclick[start..end];
                    Console.WriteLine($"获取到 {condition.CommunityName} 房型 {condition.HouseType} 幢号 {buildingNo} " +
                                      $"楼层 {floorNo}的房源ID: {roomId}");
                    return roomId;
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取房间ID异常：{ex.Message}");
                return null;
            }
        }

        private async Task<bool> TrySelectRoomAsync(HttpClient client, string applyerId, string roomId)
        {
            const string url = "https://ent.qpgzf.cn/RoomAssign/AjaxSelectRoom";
            var endTime = _startTime.AddSeconds(SelectionWindowSeconds);

            while (DateTime.Now < endTime && !_cancellationToken.IsCancellationRequested)
            {
                var success = await SelectOnceAsync(client, applyerId, roomId, url);
                if (success)
                    return true;
                await Task.Delay(_requestIntervalMs, _cancellationToken);
            }

            return false;
        }

        private async Task<bool> SelectOnceAsync(HttpClient client, string applyerId, string roomId, string url)
        {
            var data = new Dictionary<string, string> { ["ApplyIDs"] = applyerId, ["roomID"] = roomId };
            try
            {
                var response = await client.PostAsync(url, new FormUrlEncodedContent(data), _cancellationToken);
                var result = await response.Content.ReadAsStringAsync(_cancellationToken);
                Console.WriteLine(result);
                return result.Contains("成功");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发包请求错误：{ex.Message}");
                return false;
            }
        }

        
    }
}