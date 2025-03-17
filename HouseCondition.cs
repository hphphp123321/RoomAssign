namespace RoomAssign;

public class HouseCondition(string communityName, int buildingNo, int floorNo, int maxPrice, int leastArea)
{
    public string CommunityName { get; set; } = communityName;
    public int BuildingNo { get; set; } = buildingNo;
    public int FloorNo { get; set; } = floorNo;
    public int MaxPrice { get; set; } = maxPrice;
    public int LeastArea { get; set; } = leastArea;

    public override string ToString()
    {
        return $"{CommunityName} (幢号:{BuildingNo}, 层号:{FloorNo}, 价格:{MaxPrice}, 面积:{LeastArea})";
    }
}

// 枚举类型，用于选择运行模式
public enum OperationMode
{
    Click, // 模拟点击方式
    Http // 发包方式
}

public enum DriverType
{
    Chrome,
    Edge
}
