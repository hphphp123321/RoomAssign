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