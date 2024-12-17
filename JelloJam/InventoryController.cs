using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

public interface IInventoryController
{
    public void AddItem(BuffData buff);
}

public class InventoryItemAddedSignal
{
    public InventoryItem _InventoryItem;

    public InventoryItemAddedSignal(InventoryItem inventoryItem)
    {
        _InventoryItem = inventoryItem;
    }
}

public class InventoryContentChanged
{
    public List<InventoryItem> _inventoryItems;

    public int _maxCapacity;

    public InventoryContentChanged(List<InventoryItem> inventoryItems, int maxCapacity)
    {
        _inventoryItems = inventoryItems;
        _maxCapacity = maxCapacity;
    }
}

public class InventoryController : MonoBehaviour, IInventoryController
{
    int InventoryCapacity = 1;

    [SerializeField]
    string BUFF_NAME = "Gameplay";
    [SerializeField]
    string CAPACITY_UPGRADE_NAME = "Backpack Capacity";

    [SerializeField]
    Transform InventoryContent;
    [SerializeField]
    GameObject SimmilarBuffInUseWarning;

    [Inject]
    InventoryItem.Factory _Factory;

    [Inject]
    SignalBus _signalBus;

    List<InventoryItem> inventoryItems = new List<InventoryItem>();

    private void Start()
    {
        _signalBus.Subscribe<GameStartedSignal>(HandleGameStarted);
        _signalBus.Subscribe<GameEndedSignal>(HandleGameEnded);
        _signalBus.Subscribe<InventoryItemUsedSignal>(HandleInventoryItemUsed);
    }
    
    void HandleGameEnded(GameEndedSignal args)
    {
        if(args.IsFinalScore)
        {
            RemoveInventoryItems();
            _signalBus.Fire<InventoryContentChanged>(new InventoryContentChanged(inventoryItems, InventoryCapacity));
        }
    }

    void HandleInventoryItemUsed(InventoryItemUsedSignal args)
    {
        inventoryItems.Remove(args._item);
        Destroy(args._item.gameObject);
        _signalBus.Fire<InventoryContentChanged>(new InventoryContentChanged(inventoryItems, InventoryCapacity));
    }

    void HandleGameStarted(GameStartedSignal args)
    {
        var GeneralBuff = DataManager.GameData.GetBuff(BUFF_NAME).GetUpgrade(CAPACITY_UPGRADE_NAME);
        InventoryCapacity = GeneralBuff.CurrentLevelUpgrdeData().Value;
        RemoveInventoryItems();
        _signalBus.Fire<InventoryContentChanged>(new InventoryContentChanged(inventoryItems, InventoryCapacity));
    }

    [SerializeField]
    string BuffName;

    [Button]
    void TestAddItem()
    {
        //AddItem(DataManager.GameData.Buffs.Where(b => b.IsUnlocked && b.UsableInBackpack).FirstOrDefault());
        AddItem(DataManager.GameData.GetBuff(BuffName));
    }
    public void AddItem(BuffData buff)
    {
        if(inventoryItems.Count < InventoryCapacity)
        {
            InventoryItem item = _Factory.Create(buff, InventoryContent, SimmilarBuffInUseWarning);
            inventoryItems.Add(item);
            _signalBus.Fire<InventoryContentChanged>(new InventoryContentChanged(inventoryItems, InventoryCapacity));
        }
    }

    void RemoveInventoryItems()
    {
        for (int i = 0; i < inventoryItems.Count; i++)
        {
            Destroy(inventoryItems[i].gameObject);
        }
        inventoryItems = new List<InventoryItem>();
    }


}
