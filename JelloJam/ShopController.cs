using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

public class PlayerPointsChanged
{
    public int NewPoints;
    public PlayerPointsChanged(int newPoints)
    {
        NewPoints = newPoints;
    }
}

public class ShopController : MonoBehaviour
{
    [Inject]
    DataManager _dataManager;
    [Inject]
    SignalBus _signalBus;

    public bool TryPurchaseBuff(BuffData data)
    {
        int cost = data.Cost;
        if (DataManager.GameData.Points >= cost)
        {
            DataManager.GameData.Points -= cost;
            _signalBus.Fire<PlayerPointsChanged>(new PlayerPointsChanged(DataManager.GameData.Points));
            data.IsUnlocked = true;
            _dataManager.SaveGameData();
            return true;
        }
        return false;
    }

    public bool TryPurchaseUpgrade(BuffUpgrade data)
    {
        BuffData buffData = DataManager.GameData.Buffs
            .FirstOrDefault(buff => buff.Upgrades.Any(upgrade => upgrade.Equals(data)));

        if (buffData == null)
        {
            Debug.LogError("BuffManager: Couldn't find matching Buff via BuffUpgrde in Game data!");
            return false;
        }

        BuffUpgrade buffUpgradeData = buffData.Upgrades
            .FirstOrDefault(upgrade => upgrade.Equals(data));

        if (buffUpgradeData.Level >= buffUpgradeData.LevelUpgrades.Count - 1)
        {
            Debug.LogWarning("BuffManager: Maximum level reached, yet trying to upgrade!");
            return false;
        }

        int upgradeCost = buffUpgradeData.LevelUpgrades[buffUpgradeData.Level + 1].Cost;

        if (DataManager.GameData.Points >= upgradeCost)
        {
            DataManager.GameData.Points -= upgradeCost;
            _signalBus.Fire<PlayerPointsChanged>(new PlayerPointsChanged(DataManager.GameData.Points));
            buffUpgradeData.Level++;
            _dataManager.SaveGameData();
            Debug.Log($"BuffManager: Player upgrading {buffData.Name}, {data.Name} has been upgraded to level {buffUpgradeData.Level}! ");
            Debug.Log($"BuffManager: from data manager: {DataManager.GameData.Buffs.FirstOrDefault(buff => buff.Upgrades.Any(upgrade => upgrade.Equals(data))).Upgrades.FirstOrDefault(upgrade => upgrade.Equals(data))}");
        }
        else
            return false;
        return true;
    }
}
