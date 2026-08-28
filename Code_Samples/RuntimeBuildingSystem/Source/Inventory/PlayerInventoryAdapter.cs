using System;
using System.Collections.Generic;
using MalbersAnimations.InventorySystem;
using Project.Gameplay.Inventory;
using Project.Gameplay.Items;
using UnityEngine;

public class PlayerInventoryAdapter : MonoBehaviour, IInventoryAdapter
{
    [Header("Unified Inventory")]
    [SerializeField] private bool useUnifiedInventory = true;
    [SerializeField] private PlayerInventoryStore unifiedInventoryStore = null;
    [SerializeField] private ItemDatabase itemDatabase = null;

    [Header("Legacy Inventory Fallback")]
    [SerializeField] private Inventory resourceInventorySlot = null;
    [SerializeField] private Inventory weaponInventorySlot = null;

    private Project.UI.Inventory.PlayerUnifiedInventoryController unifiedInventoryController;

    private void Awake()
    {
        ResolveUnifiedInventoryStore();
        ResolveItemDatabase();
    }

    public int GetItemCount(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            return 0;
        }

        if (useUnifiedInventory && TryGetUnifiedItemCount(itemName, out int unifiedCount))
        {
            return unifiedCount;
        }

        return GetLegacyItemCount(itemName);
    }

    public void ConsumeItem(string itemName, int count)
    {
        if (string.IsNullOrWhiteSpace(itemName) || count <= 0)
        {
            return;
        }

        if (useUnifiedInventory && TryConsumeUnifiedItem(itemName, count))
        {
            return;
        }

        ConsumeLegacyItem(itemName, count);
    }

    public bool HasRequirements(IReadOnlyDictionary<string, int> requirements)
    {
        if (requirements == null)
        {
            return true;
        }

        foreach (KeyValuePair<string, int> requirement in requirements)
        {
            if (requirement.Value <= 0)
            {
                continue;
            }

            if (GetItemCount(requirement.Key) < requirement.Value)
            {
                return false;
            }
        }

        return true;
    }

    public bool HasRequirements(IReadOnlyList<ResourceRequirement> requirements)
    {
        if (requirements == null)
        {
            return true;
        }

        for (int i = 0; i < requirements.Count; i++)
        {
            ResourceRequirement requirement = requirements[i];
            if (requirement.count <= 0)
            {
                continue;
            }

            if (GetItemCount(requirement.itemName) < requirement.count)
            {
                return false;
            }
        }

        return true;
    }

    public bool TryConsumeRequirements(IReadOnlyDictionary<string, int> requirements)
    {
        if (!HasRequirements(requirements))
        {
            return false;
        }

        if (requirements == null)
        {
            return true;
        }

        foreach (KeyValuePair<string, int> requirement in requirements)
        {
            if (requirement.Value > 0)
            {
                ConsumeItem(requirement.Key, requirement.Value);
            }
        }

        return true;
    }

    public bool TryConsumeRequirements(IReadOnlyList<ResourceRequirement> requirements)
    {
        if (!HasRequirements(requirements))
        {
            return false;
        }

        if (requirements == null)
        {
            return true;
        }

        for (int i = 0; i < requirements.Count; i++)
        {
            ResourceRequirement requirement = requirements[i];
            if (requirement.count > 0)
            {
                ConsumeItem(requirement.itemName, requirement.count);
            }
        }

        return true;
    }

    private bool TryGetUnifiedItemCount(string itemName, out int count)
    {
        count = 0;
        ResolveUnifiedInventoryStore();
        if (unifiedInventoryStore == null)
        {
            return false;
        }

        count = CountUnifiedSlotsByName(itemName);
        if (count > 0)
        {
            return true;
        }

        if (TryResolveUnifiedItem(itemName, out Item item))
        {
            count = unifiedInventoryStore.GetItemQuantity(item);
            return true;
        }

        return false;
    }

    private bool TryConsumeUnifiedItem(string itemName, int count)
    {
        ResolveUnifiedInventoryStore();
        if (unifiedInventoryStore == null)
        {
            return false;
        }

        if (TryResolveUnifiedItem(itemName, out Item item))
        {
            return unifiedInventoryStore.TryConsumeItem(item, count);
        }

        if (CountUnifiedSlotsByName(itemName) < count)
        {
            return false;
        }

        int remaining = count;
        for (int i = 0; i < PlayerInventoryStore.SlotCount && remaining > 0; i++)
        {
            PlayerInventorySlotData slot = unifiedInventoryStore.GetSlot(i);
            if (slot == null || slot.IsEmpty || !MatchesItemName(slot.Item.item, itemName))
            {
                continue;
            }

            int removed = Mathf.Min(slot.Quantity, remaining);
            if (unifiedInventoryStore.RemoveFromSlot(i, removed))
            {
                remaining -= removed;
            }
        }

        return remaining <= 0;
    }

    private bool TryResolveUnifiedItem(string itemName, out Item item)
    {
        item = ResolveItemFromUnifiedSlots(itemName);
        if (item != null)
        {
            return true;
        }

        item = ResolveItemFromDatabase(itemName);
        return item != null;
    }

    private Item ResolveItemFromUnifiedSlots(string itemName)
    {
        ResolveUnifiedInventoryStore();
        if (unifiedInventoryStore == null)
        {
            return null;
        }

        for (int i = 0; i < PlayerInventoryStore.SlotCount; i++)
        {
            PlayerInventorySlotData slot = unifiedInventoryStore.GetSlot(i);
            Item item = slot != null && !slot.IsEmpty ? slot.Item.item : null;
            if (MatchesItemName(item, itemName))
            {
                return item;
            }
        }

        return null;
    }

    private Item ResolveItemFromDatabase(string itemName)
    {
        ResolveItemDatabase();
        if (itemDatabase == null || itemDatabase.ManagedItems == null)
        {
            return null;
        }

        for (int i = 0; i < itemDatabase.ManagedItems.Count; i++)
        {
            ItemDatabase.Entry entry = itemDatabase.ManagedItems[i];
            Item item = entry?.SourceItem;
            if (MatchesItemName(item, itemName) ||
                string.Equals(entry?.ItemName, itemName, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    private int CountUnifiedSlotsByName(string itemName)
    {
        ResolveUnifiedInventoryStore();
        if (unifiedInventoryStore == null)
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < PlayerInventoryStore.SlotCount; i++)
        {
            PlayerInventorySlotData slot = unifiedInventoryStore.GetSlot(i);
            if (slot != null && !slot.IsEmpty && MatchesItemName(slot.Item.item, itemName))
            {
                total += slot.Quantity;
            }
        }

        return total;
    }

    private int GetLegacyItemCount(string itemName)
    {
        int resourceCount = resourceInventorySlot != null ? resourceInventorySlot.GetItemQuantity(itemName) : 0;
        int weaponCount = weaponInventorySlot != null ? weaponInventorySlot.GetItemQuantity(itemName) : 0;
        return resourceCount + weaponCount;
    }

    private void ConsumeLegacyItem(string itemName, int count)
    {
        int remaining = ConsumeLegacyInventory(resourceInventorySlot, itemName, count);
        if (remaining > 0)
        {
            ConsumeLegacyInventory(weaponInventorySlot, itemName, remaining);
        }
    }

    private int ConsumeLegacyInventory(Inventory inventory, string itemName, int count)
    {
        if (inventory == null || count <= 0)
        {
            return count;
        }

        int remaining = count;
        while (remaining > 0)
        {
            Item.ItemInstance item = inventory.FindItemInInventory(itemName);
            if (item == null)
            {
                break;
            }

            int available = Mathf.Max(1, inventory.GetItemQuantity(itemName));
            int removed = Mathf.Min(available, remaining);
            inventory.RemoveItem(item, removed);
            remaining -= removed;
        }

        return remaining;
    }

    private void ResolveUnifiedInventoryStore()
    {
        if (!useUnifiedInventory)
        {
            return;
        }

        PlayerInventoryStore controllerStore = ResolveUnifiedInventoryControllerStore();
        if (controllerStore != null)
        {
            unifiedInventoryStore = controllerStore;
            return;
        }

        Transform searchRoot = transform.root;
        if (IsPreferredLocalStore(unifiedInventoryStore, searchRoot))
        {
            return;
        }

        PlayerInventoryStore localStore = ResolveBestStore(
            searchRoot != null
                ? searchRoot.GetComponentsInChildren<PlayerInventoryStore>(true)
                : Array.Empty<PlayerInventoryStore>(),
            searchRoot);
        if (localStore != null)
        {
            unifiedInventoryStore = localStore;
            return;
        }

        controllerStore = ResolveGlobalUnifiedInventoryControllerStore();
        if (controllerStore != null)
        {
            unifiedInventoryStore = controllerStore;
            return;
        }

        if (unifiedInventoryStore == null)
        {
            PlayerInventoryStore[] stores =
                FindObjectsByType<PlayerInventoryStore>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
            unifiedInventoryStore = ResolveBestStore(stores, preferredRoot: null);
        }
    }

    private PlayerInventoryStore ResolveUnifiedInventoryControllerStore()
    {
        Transform searchRoot = transform.root;
        if (searchRoot != null &&
            (unifiedInventoryController == null || unifiedInventoryController.transform.root != searchRoot))
        {
            Project.UI.Inventory.PlayerUnifiedInventoryController localController =
                searchRoot.GetComponentInChildren<Project.UI.Inventory.PlayerUnifiedInventoryController>(true);
            if (localController != null)
            {
                unifiedInventoryController = localController;
            }
        }

        if (unifiedInventoryController != null &&
            (searchRoot == null || unifiedInventoryController.transform.root == searchRoot))
        {
            return unifiedInventoryController.Store;
        }

        return null;
    }

    private PlayerInventoryStore ResolveGlobalUnifiedInventoryControllerStore()
    {
        Project.UI.Inventory.PlayerUnifiedInventoryController[] controllers =
            FindObjectsByType<Project.UI.Inventory.PlayerUnifiedInventoryController>(FindObjectsInactive.Include,  FindObjectsSortMode.InstanceID);

        for (int i = 0; i < controllers.Length; i++)
        {
            Project.UI.Inventory.PlayerUnifiedInventoryController controller = controllers[i];
            if (controller != null && controller.Store != null)
            {
                unifiedInventoryController = controller;
                return controller.Store;
            }
        }

        return null;
    }

    private static bool IsPreferredLocalStore(PlayerInventoryStore store, Transform preferredRoot)
    {
        return store != null &&
               preferredRoot != null &&
               store.transform.root == preferredRoot &&
               (string.Equals(store.name, "Player", StringComparison.Ordinal) || HasPlayerTag(store));
    }

    private static PlayerInventoryStore ResolveBestStore(
        PlayerInventoryStore[] stores,
        Transform preferredRoot)
    {
        PlayerInventoryStore best = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < stores.Length; i++)
        {
            PlayerInventoryStore candidate = stores[i];
            if (candidate == null)
            {
                continue;
            }

            int score = ScoreStoreCandidate(candidate, preferredRoot);
            if (best == null || score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    private static int ScoreStoreCandidate(PlayerInventoryStore candidate, Transform preferredRoot)
    {
        Transform candidateTransform = candidate.transform;
        int score = preferredRoot != null && candidateTransform.root == preferredRoot ? 1000 : 0;
        if (string.Equals(candidateTransform.name, "Player", StringComparison.Ordinal))
        {
            score += 500;
        }

        if (HasPlayerTag(candidate))
        {
            score += 250;
        }

        if (candidate.GetComponent<InventoryMaster>() != null)
        {
            score += 100;
        }

        if (candidate.gameObject.activeInHierarchy)
        {
            score += 25;
        }

        return score;
    }

    private static bool HasPlayerTag(Component candidate)
    {
        try
        {
            return candidate != null && candidate.CompareTag("Player");
        }
        catch (UnityException)
        {
            // Keep the adapter usable in isolated tests or projects without a Player tag.
            return false;
        }
    }

    private void ResolveItemDatabase()
    {
        if (itemDatabase != null)
        {
            return;
        }

        Project.UI.Inventory.PlayerBuildingCatalogSettings settings =
            Resources.Load<Project.UI.Inventory.PlayerBuildingCatalogSettings>("PlayerBuildingCatalogSettings");
        if (settings != null)
        {
            itemDatabase = settings.ItemDatabase;
        }
    }

    private static bool MatchesItemName(Item item, string itemName)
    {
        return item != null &&
               !string.IsNullOrWhiteSpace(itemName) &&
               string.Equals(item.itemName, itemName, StringComparison.OrdinalIgnoreCase);
    }
}
