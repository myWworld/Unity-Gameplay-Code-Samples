
public interface IInventoryAdapter
{
    // 특정 아이템의 개수를 반환
    int GetItemCount(string itemName);

    // 특정 아이템을 지정한 개수만큼 소모
    void ConsumeItem(string itemName, int count);
}
